namespace GranBrain.Core.Configuration;

public sealed class GranBrainOptions
{
    public const string SectionName = "GranBrain";

    public string VaultPath { get; set; } = @"G:\Meu Drive\Softwares\Obsidian\Igor Grandin";
    public AuthOptions Auth { get; set; } = new();
    public CliOptions Cli { get; set; } = new();
}

public sealed class AuthOptions
{
    public string Username { get; set; } = "igor";
    public string PasswordHash { get; set; } = string.Empty;
    public string DefaultPassword { get; set; } = "GranBrain@2026";
    public string JwtSecret { get; set; } = "GranBrain-Super-Secret-Key-2026-Obsidian-AI-OS-Min32Chars!";
    public string Issuer { get; set; } = "GranBrain.Api";
    public string Audience { get; set; } = "GranBrain.App";
    public int TokenExpirationHours { get; set; } = 12;
    public int MaxLoginAttemptsPerWindow { get; set; } = 5;
    public int LoginWindowSeconds { get; set; } = 60;
}

public sealed class CliOptions
{
    public string CodexPath { get; set; } = @"C:\Users\igor.grandin\AppData\Local\Programs\OpenAI\Codex\bin\codex.exe";
    public string ClaudePath { get; set; } = "claude";
    public string GeminiPath { get; set; } = @"C:\Users\igor.grandin\AppData\Local\agy\bin\agy.exe";
    public int ExecutionTimeoutSeconds { get; set; } = 45;
}
