using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using GranBrain.Api.Endpoints;
using GranBrain.Core.Configuration;
using GranBrain.Core.Interfaces;
using GranBrain.Core.Models;
using GranBrain.Core.Security;
using GranBrain.Infrastructure.Agents;
using GranBrain.Infrastructure.Calendar;
using GranBrain.Infrastructure.Vault;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var env = sp.GetRequiredService<IHostEnvironment>();
    var bound = config.GetSection(GranBrainOptions.SectionName).Get<GranBrainOptions>() ?? new GranBrainOptions();
    ProductionConfigurationValidator.ValidateOrThrow(bound, env.IsProduction());
    return bound;
});

builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return config
        .GetSection($"{GranBrainOptions.SectionName}:{CalendarOptions.SectionName}")
        .Get<CalendarOptions>() ?? new CalendarOptions();
});

builder.Services.AddHttpClient();
builder.Services.AddSingleton(sp => new VaultPathGuard(sp.GetRequiredService<GranBrainOptions>().VaultPath));
builder.Services.AddSingleton<VaultInitializer>();
builder.Services.AddSingleton<IVaultService, ObsidianVaultService>();
builder.Services.AddSingleton<ICalendarConnector, GoogleCalendarConnector>();
builder.Services.AddSingleton<ICalendarConnector, MicrosoftGraphCalendarConnector>();
builder.Services.AddSingleton<ICalendarSyncService, CalendarSyncService>();
builder.Services.AddSingleton<ICliProcessRunner, CliProcessRunner>();
builder.Services.AddSingleton<IAgentOrchestrator, AgentOrchestratorService>();

builder.Services.AddCors();
builder.Services.AddOptions<CorsOptions>()
    .Configure<GranBrainOptions, IHostEnvironment>((cors, opts, env) =>
    {
        var normalizedAllowedOrigins = (opts.AllowedOrigins ?? [])
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        cors.AddDefaultPolicy(policy =>
        {
            if (normalizedAllowedOrigins.Length > 0)
            {
                policy.WithOrigins(normalizedAllowedOrigins).AllowAnyHeader().AllowAnyMethod();
            }
            else if (!env.IsProduction())
            {
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            }
        });
    });

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<GranBrainOptions>((jwt, opts) =>
    {
        jwt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = opts.Auth.Issuer,
            ValidAudience = opts.Auth.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.Auth.JwtSecret)),
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
        var runtimeOptions = httpContext.RequestServices.GetRequiredService<GranBrainOptions>();
        var partitionKey = ResolveClientPartitionKey(httpContext);
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, runtimeOptions.Auth.MaxLoginAttemptsPerWindow),
            Window = TimeSpan.FromSeconds(Math.Max(5, runtimeOptions.Auth.LoginWindowSeconds)),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
});

var app = builder.Build();
_ = app.Services.GetRequiredService<GranBrainOptions>();

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

        if (ex is VaultValidationException validation)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { code = validation.ErrorCode, message = validation.Message });
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

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    ForwardLimit = null
};
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();

app.UseForwardedHeaders(forwardedHeadersOptions);
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", (HttpContext httpContext, GranBrainOptions runtimeOptions) =>
    Results.Ok(BuildHealthStatus(httpContext, runtimeOptions, app.Environment.EnvironmentName)));
app.MapAuthEndpoints();
app.MapVaultEndpoints();
app.MapCalendarEndpoints();
app.MapAgentEndpoints();

app.Run();

static string ResolveClientPartitionKey(HttpContext httpContext)
{
    if (TryExtractFirstIp(httpContext.Request.Headers["CF-Connecting-IP"].FirstOrDefault(), out var cfIp))
    {
        return cfIp;
    }

    if (TryExtractFirstIp(httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault(), out var forwardedIp))
    {
        return forwardedIp;
    }

    return httpContext.Connection.RemoteIpAddress?.ToString() ?? "local-client";
}

static bool TryExtractFirstIp(string? headerValue, out string ipAddress)
{
    ipAddress = string.Empty;
    if (string.IsNullOrWhiteSpace(headerValue))
    {
        return false;
    }

    var firstToken = headerValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(firstToken) && IPAddress.TryParse(firstToken, out var parsed))
    {
        ipAddress = parsed.ToString();
        return true;
    }

    return false;
}

static SystemHealthStatusDto BuildHealthStatus(HttpContext httpContext, GranBrainOptions options, string environmentName)
{
    var vaultAccessible = IsVaultAccessible(options.VaultPath);
    var tunnelStatus = ResolveTunnelStatus(httpContext, options);
    var (syncStatus, lastSyncedAtUtc) = ReadSyncState(options.SyncStatusFilePath);
    var overallStatus = vaultAccessible && !string.Equals(syncStatus, "error", StringComparison.OrdinalIgnoreCase)
        ? "ok"
        : "degraded";

    return new SystemHealthStatusDto(
        Status: overallStatus,
        Service: "GranBrain.Api",
        Environment: environmentName,
        VaultAccessible: vaultAccessible,
        TunnelStatus: tunnelStatus,
        SyncStatus: syncStatus,
        LastSyncedAtUtc: lastSyncedAtUtc);
}

static bool IsVaultAccessible(string? vaultPath)
{
    if (string.IsNullOrWhiteSpace(vaultPath))
    {
        return false;
    }

    try
    {
        return Directory.Exists(vaultPath);
    }
    catch
    {
        return false;
    }
}

static string ResolveTunnelStatus(HttpContext httpContext, GranBrainOptions options)
{
    if (string.Equals(options.TunnelMode, "cloudflare-tunnel", StringComparison.OrdinalIgnoreCase) ||
        httpContext.Request.IsHttps ||
        string.Equals(httpContext.Request.Headers["X-Forwarded-Proto"].FirstOrDefault(), "https", StringComparison.OrdinalIgnoreCase) ||
        !string.IsNullOrWhiteSpace(httpContext.Request.Headers["CF-Connecting-IP"].FirstOrDefault()))
    {
        return "ok";
    }

    return "local";
}

static (string SyncStatus, string? LastSyncedAtUtc) ReadSyncState(string? syncStatusFilePath)
{
    if (string.IsNullOrWhiteSpace(syncStatusFilePath) || !File.Exists(syncStatusFilePath))
    {
        return ("pending", null);
    }

    try
    {
        var json = File.ReadAllText(syncStatusFilePath);
        var payload = JsonSerializer.Deserialize<RcloneSyncStatePayload>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var normalized = payload?.Status?.Trim().ToLowerInvariant();
        if (normalized is not ("ok" or "pending" or "error"))
        {
            return ("error", null);
        }

        return (normalized, payload?.LastSyncedAtUtc);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
    {
        return ("error", null);
    }
}

public partial class Program;
