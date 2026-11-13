namespace GranBrain.Core.Configuration;

public sealed class RoutinesOptions
{
    public const string SectionName = "Routines";

    public bool EnableBackgroundScheduler { get; set; } = true;
    public string MorningPrepTime { get; set; } = "06:00";
    public string NightlyClosureTime { get; set; } = "23:30";
    public int InboxIntervalMinutes { get; set; } = 120;
    public bool UseCli { get; set; } = false;
    public string CliProvider { get; set; } = "codex";
    public string InboxFolder { get; set; } = "inbox";
    public string ArchiveFolder { get; set; } = "inbox/_archive";
    public string ZettelkastenFolder { get; set; } = "zettelkasten";
    public string TimeZoneId { get; set; } = "America/Sao_Paulo";
    public int MaxAuditEntries { get; set; } = 50;
}
