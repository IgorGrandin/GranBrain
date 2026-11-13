namespace GranBrain.Core.Configuration;

public sealed class CalendarOptions
{
    public const string SectionName = "Calendar";

    public GoogleCalendarOptions Google { get; set; } = new();
    public MicrosoftCalendarOptions Microsoft { get; set; } = new();
    public string TimeZoneId { get; set; } = "America/Sao_Paulo";
    public string DailyNotesFolder { get; set; } = "daily";
    public string MeetingNotesFolder { get; set; } = "zettelkasten";
    public int DefaultStartHour { get; set; } = 8;
    public int DefaultEndHour { get; set; } = 18;
}

public class OAuthCalendarProviderOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string TokenUri { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = string.Empty;

    public bool HasAnyCredential =>
        !string.IsNullOrWhiteSpace(ClientId) ||
        !string.IsNullOrWhiteSpace(ClientSecret) ||
        !string.IsNullOrWhiteSpace(RefreshToken);

    public bool HasCompleteCredentials =>
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret) &&
        !string.IsNullOrWhiteSpace(RefreshToken);
}

public sealed class GoogleCalendarOptions : OAuthCalendarProviderOptions
{
    public GoogleCalendarOptions()
    {
        TokenUri = "https://oauth2.googleapis.com/token";
        ApiBaseUrl = "https://www.googleapis.com/calendar/v3";
    }

    public string CalendarId { get; set; } = "primary";
}

public sealed class MicrosoftCalendarOptions : OAuthCalendarProviderOptions
{
    public MicrosoftCalendarOptions()
    {
        TokenUri = "https://login.microsoftonline.com/common/oauth2/v2.0/token";
        ApiBaseUrl = "https://graph.microsoft.com/v1.0";
    }
}
