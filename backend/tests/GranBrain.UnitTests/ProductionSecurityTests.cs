using System.Net;
using System.Net.Http.Json;
using GranBrain.Api.Endpoints;
using GranBrain.Core.Configuration;
using GranBrain.Core.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace GranBrain.UnitTests;

public sealed class ProductionSecurityTests
{
    private static GranBrainOptions CreateValidProductionOptions() => new()
    {
        VaultPath = "/data/vault",
        SyncStatusFilePath = "/var/run/granbrain/sync-status.json",
        TunnelMode = "cloudflare-tunnel",
        AllowedOrigins = ["https://app.granbrain.com.br"],
        Auth = new AuthOptions
        {
            Username = "igor",
            PasswordHash = AuthEndpoints.ComputeSha256Hash("ProdStrongPass#2026"),
            DefaultPassword = string.Empty,
            JwtSecret = "Prod-Jwt-Secret-With-At-Least-Forty-Eight-Characters-987654"
        }
    };

    [Fact]
    public void ValidateOrThrow_BlocksDefaultOrWeakConfigInProduction()
    {
        // (a) Default, empty, or short JwtSecret -> production_secret_violation
        var defaultSecretOpts = CreateValidProductionOptions();
        defaultSecretOpts.Auth.JwtSecret = ProductionConfigurationValidator.DefaultInsecureJwtSecret;
        var exDefaultSecret = Assert.Throws<InvalidOperationException>(
            () => ProductionConfigurationValidator.ValidateOrThrow(defaultSecretOpts, isProduction: true));
        Assert.Contains("production_secret_violation", exDefaultSecret.Message);

        var emptySecretOpts = CreateValidProductionOptions();
        emptySecretOpts.Auth.JwtSecret = string.Empty;
        var exEmptySecret = Assert.Throws<InvalidOperationException>(
            () => ProductionConfigurationValidator.ValidateOrThrow(emptySecretOpts, isProduction: true));
        Assert.Contains("production_secret_violation", exEmptySecret.Message);

        var shortSecretOpts = CreateValidProductionOptions();
        shortSecretOpts.Auth.JwtSecret = "curto";
        var exShortSecret = Assert.Throws<InvalidOperationException>(
            () => ProductionConfigurationValidator.ValidateOrThrow(shortSecretOpts, isProduction: true));
        Assert.Contains("production_secret_violation", exShortSecret.Message);

        // (b) Empty PasswordHash with default or empty DefaultPassword -> production_secret_violation
        var defaultPassOpts = CreateValidProductionOptions();
        defaultPassOpts.Auth.PasswordHash = string.Empty;
        defaultPassOpts.Auth.DefaultPassword = ProductionConfigurationValidator.DefaultInsecurePassword;
        var exDefaultPass = Assert.Throws<InvalidOperationException>(
            () => ProductionConfigurationValidator.ValidateOrThrow(defaultPassOpts, isProduction: true));
        Assert.Contains("production_secret_violation", exDefaultPass.Message);

        var emptyPassOpts = CreateValidProductionOptions();
        emptyPassOpts.Auth.PasswordHash = string.Empty;
        emptyPassOpts.Auth.DefaultPassword = string.Empty;
        var exEmptyPass = Assert.Throws<InvalidOperationException>(
            () => ProductionConfigurationValidator.ValidateOrThrow(emptyPassOpts, isProduction: true));
        Assert.Contains("production_secret_violation", exEmptyPass.Message);

        // (c) Windows G:\Meu Drive or empty VaultPath -> production_config_violation
        var windowsVaultOpts = CreateValidProductionOptions();
        windowsVaultOpts.VaultPath = @"G:\Meu Drive\Softwares\Obsidian\Igor Grandin";
        var exWindowsVault = Assert.Throws<InvalidOperationException>(
            () => ProductionConfigurationValidator.ValidateOrThrow(windowsVaultOpts, isProduction: true));
        Assert.Contains("production_config_violation", exWindowsVault.Message);

        var emptyVaultOpts = CreateValidProductionOptions();
        emptyVaultOpts.VaultPath = string.Empty;
        var exEmptyVault = Assert.Throws<InvalidOperationException>(
            () => ProductionConfigurationValidator.ValidateOrThrow(emptyVaultOpts, isProduction: true));
        Assert.Contains("production_config_violation", exEmptyVault.Message);

        // (d) Empty, wildcard '*', or non-HTTPS AllowedOrigins -> production_cors_violation
        var emptyOriginsOpts = CreateValidProductionOptions();
        emptyOriginsOpts.AllowedOrigins = [];
        var exEmptyOrigins = Assert.Throws<InvalidOperationException>(
            () => ProductionConfigurationValidator.ValidateOrThrow(emptyOriginsOpts, isProduction: true));
        Assert.Contains("production_cors_violation", exEmptyOrigins.Message);

        var wildcardOriginsOpts = CreateValidProductionOptions();
        wildcardOriginsOpts.AllowedOrigins = ["*"];
        var exWildcardOrigins = Assert.Throws<InvalidOperationException>(
            () => ProductionConfigurationValidator.ValidateOrThrow(wildcardOriginsOpts, isProduction: true));
        Assert.Contains("production_cors_violation", exWildcardOrigins.Message);

        var httpOriginsOpts = CreateValidProductionOptions();
        httpOriginsOpts.AllowedOrigins = ["http://app.dominio.com"];
        var exHttpOrigins = Assert.Throws<InvalidOperationException>(
            () => ProductionConfigurationValidator.ValidateOrThrow(httpOriginsOpts, isProduction: true));
        Assert.Contains("production_cors_violation", exHttpOrigins.Message);

        // Non-production never throws even with default options
        ProductionConfigurationValidator.ValidateOrThrow(new GranBrainOptions(), isProduction: false);
    }

    [Fact]
    public async Task Program_InProductionEnvironment_RefusesDefaultConfigAndBootsWithValidEnv()
    {
        using var insecureFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

        var startupException = Assert.Throws<InvalidOperationException>(() => insecureFactory.CreateClient());
        Assert.Contains("production_secret_violation", startupException.Message);

        var tempVault = Path.Combine(Path.GetTempPath(), "GranBrainProdBootTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempVault);
        try
        {
            var prodHash = AuthEndpoints.ComputeSha256Hash("ProdStrongPass#2026");
            using var validFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["GranBrain:VaultPath"] = tempVault,
                        ["GranBrain:Auth:Username"] = "igor",
                        ["GranBrain:Auth:PasswordHash"] = prodHash,
                        ["GranBrain:Auth:DefaultPassword"] = string.Empty,
                        ["GranBrain:Auth:JwtSecret"] = "Prod-Jwt-Secret-With-At-Least-Forty-Eight-Characters-123456",
                        ["GranBrain:AllowedOrigins:0"] = "https://app.granbrain.com.br",
                        ["GranBrain:TunnelMode"] = "cloudflare-tunnel"
                    });
                });
            });

            using var client = validFactory.CreateClient();

            var healthRes = await client.GetAsync("/api/health");
            Assert.Equal(HttpStatusCode.OK, healthRes.StatusCode);
            var health = await healthRes.Content.ReadFromJsonAsync<SystemHealthStatusDto>();
            Assert.NotNull(health);
            Assert.Equal("Production", health.Environment);
            Assert.True(health.VaultAccessible);

            var validLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("igor", "ProdStrongPass#2026"));
            Assert.Equal(HttpStatusCode.OK, validLogin.StatusCode);

            var defaultPassLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("igor", "GranBrain@2026"));
            Assert.Equal(HttpStatusCode.Unauthorized, defaultPassLogin.StatusCode);
        }
        finally
        {
            if (Directory.Exists(tempVault))
            {
                Directory.Delete(tempVault, true);
            }
        }
    }

    [Fact]
    public void VerifyPassword_RejectsEmptyOrNullDefaultPassword()
    {
        Assert.False(AuthEndpoints.VerifyPassword(string.Empty, new AuthOptions
        {
            PasswordHash = string.Empty,
            DefaultPassword = string.Empty
        }));

        Assert.False(AuthEndpoints.VerifyPassword("qualquer", new AuthOptions
        {
            PasswordHash = string.Empty,
            DefaultPassword = null!
        }));

        Assert.False(AuthEndpoints.VerifyPassword("   ", new AuthOptions
        {
            PasswordHash = AuthEndpoints.ComputeSha256Hash("ProdStrongPass#2026"),
            DefaultPassword = string.Empty
        }));
    }
}
