using System.Text;
using System.Threading.RateLimiting;
using GranBrain.Api.Endpoints;
using GranBrain.Core.Configuration;
using GranBrain.Core.Interfaces;
using GranBrain.Core.Security;
using GranBrain.Infrastructure.Agents;
using GranBrain.Infrastructure.Vault;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var options = builder.Configuration.GetSection(GranBrainOptions.SectionName).Get<GranBrainOptions>() ?? new GranBrainOptions();
builder.Services.AddSingleton(options);
builder.Services.AddSingleton(sp => new VaultPathGuard(sp.GetRequiredService<GranBrainOptions>().VaultPath));
builder.Services.AddSingleton<VaultInitializer>();
builder.Services.AddSingleton<IVaultService, ObsidianVaultService>();
builder.Services.AddSingleton<ICliProcessRunner, CliProcessRunner>();
builder.Services.AddSingleton<IAgentOrchestrator, AgentOrchestratorService>();

builder.Services.AddCors(cors =>
{
    cors.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(jwt =>
    {
        jwt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = options.Auth.Issuer,
            ValidAudience = options.Auth.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Auth.JwtSecret)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(limiter =>
{
    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    limiter.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { code = "rate_limit_exceeded", message = "Muitas tentativas de login. Aguarde antes de tentar novamente." },
            cancellationToken: token);
    };

    limiter.AddPolicy("login-policy", httpContext =>
    {
        var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "local-client";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, options.Auth.MaxLoginAttemptsPerWindow),
            Window = TimeSpan.FromSeconds(Math.Max(5, options.Auth.LoginWindowSeconds)),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
});

var app = builder.Build();

app.UseExceptionHandler(errApp =>
{
    errApp.Run(async context =>
    {
        var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        if (ex is VaultPathViolationException violation)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { code = violation.ErrorCode, message = violation.Message });
            return;
        }

        if (ex is VaultEntityNotFoundException notFound)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { code = notFound.ErrorCode, message = notFound.Message });
            return;
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new { code = "internal_error", message = ex?.Message ?? "Unexpected error." });
    });
});

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "GranBrain.Api" }));
app.MapAuthEndpoints();
app.MapVaultEndpoints();
app.MapAgentEndpoints();

app.Run();

public partial class Program;
