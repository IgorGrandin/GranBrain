using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GranBrain.Core.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace GranBrain.Api.Endpoints;

public sealed record LoginRequest(string Username, string Password);
public sealed record LoginResponse(string Token, string Username, DateTime ExpiresAtUtc);

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", (LoginRequest request, GranBrainOptions options) =>
        {
            if (string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrWhiteSpace(request.Password) ||
                !string.Equals(request.Username.Trim(), options.Auth.Username, StringComparison.OrdinalIgnoreCase) ||
                !VerifyPassword(request.Password, options.Auth))
            {
                return Results.Json(
                    new { code = "invalid_credentials", message = "Usuário ou senha inválidos." },
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var expiresAt = DateTime.UtcNow.AddHours(Math.Max(1, options.Auth.TokenExpirationHours));
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Auth.JwtSecret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, options.Auth.Username),
                new Claim(ClaimTypes.Name, options.Auth.Username),
                new Claim("role", "owner")
            };

            var token = new JwtSecurityToken(
                issuer: options.Auth.Issuer,
                audience: options.Auth.Audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: creds);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            return Results.Ok(new LoginResponse(tokenString, options.Auth.Username, expiresAt));
        }).RequireRateLimiting("login-policy");

        return app;
    }

    public static string ComputeSha256Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    private static bool VerifyPassword(string inputPassword, AuthOptions auth)
    {
        if (!string.IsNullOrWhiteSpace(auth.PasswordHash))
        {
            var inputHash = ComputeSha256Hash(inputPassword);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(inputHash.ToUpperInvariant()),
                Encoding.UTF8.GetBytes(auth.PasswordHash.Trim().ToUpperInvariant()));
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(inputPassword),
            Encoding.UTF8.GetBytes(auth.DefaultPassword));
    }
}
