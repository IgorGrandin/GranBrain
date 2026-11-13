namespace GranBrain.Core.Configuration;

public static class ProductionConfigurationValidator
{
    public const string DefaultInsecureJwtSecret = "GranBrain-Super-Secret-Key-2026-Obsidian-AI-OS-Min32Chars!";
    public const string DefaultInsecurePassword = "GranBrain@2026";
    private const string WindowsDriveVaultPrefix = @"G:\Meu Drive";
    private const int MinimumJwtSecretLength = 32;

    /// <summary>
    /// Enforces fail-fast validation of secrets, vault path, and CORS origins when running in Production.
    /// </summary>
    public static void ValidateOrThrow(GranBrainOptions options, bool isProduction)
    {
        if (!isProduction)
        {
            return;
        }

        if (options is null)
        {
            throw new InvalidOperationException("production_config_violation: GranBrainOptions não pode ser nulo em produção.");
        }

        ValidateJwtSecret(options.Auth);
        ValidateAdminPassword(options.Auth);
        ValidateVaultPath(options.VaultPath);
        ValidateAllowedOrigins(options.AllowedOrigins);
    }

    private static void ValidateJwtSecret(AuthOptions auth)
    {
        var secret = auth?.JwtSecret?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(secret) ||
            secret.Length < MinimumJwtSecretLength ||
            string.Equals(secret, DefaultInsecureJwtSecret, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "production_secret_violation: GranBrain:Auth:JwtSecret deve ser definido em produção com ao menos 32 caracteres e nunca usar o valor default do repositório.");
        }
    }

    private static void ValidateAdminPassword(AuthOptions auth)
    {
        var hasHash = !string.IsNullOrWhiteSpace(auth?.PasswordHash);
        if (hasHash)
        {
            return;
        }

        var defaultPassword = auth?.DefaultPassword?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(defaultPassword) ||
            string.Equals(defaultPassword, DefaultInsecurePassword, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "production_secret_violation: Defina GranBrain:Auth:PasswordHash ou uma senha forte customizada em GranBrain:Auth:DefaultPassword em produção.");
        }
    }

    private static void ValidateVaultPath(string? vaultPath)
    {
        var trimmed = vaultPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed) ||
            trimmed.StartsWith(WindowsDriveVaultPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "production_config_violation: GranBrain:VaultPath deve apontar para o caminho montado no ambiente de produção e não pode ser vazio nem usar G:\\Meu Drive.");
        }
    }

    private static void ValidateAllowedOrigins(string[]? allowedOrigins)
    {
        if (allowedOrigins is null || allowedOrigins.Length == 0)
        {
            throw new InvalidOperationException(
                "production_cors_violation: GranBrain:AllowedOrigins deve listar ao menos uma origem HTTPS explícita em produção.");
        }

        foreach (var rawOrigin in allowedOrigins)
        {
            var origin = rawOrigin?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(origin) || origin.Contains('*', StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "production_cors_violation: GranBrain:AllowedOrigins não pode conter entradas vazias ou wildcard '*'.");
            }

            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"production_cors_violation: Origem inválida '{rawOrigin}' em GranBrain:AllowedOrigins. Apenas URLs https:// absolutas são permitidas em produção.");
            }
        }
    }
}
