using System.Text.Json;
using GranBrain.Core.Configuration;
using GranBrain.Core.Interfaces;
using GranBrain.Core.Models;

namespace GranBrain.Infrastructure.Calendar;

public sealed class MicrosoftGraphCalendarConnector : ICalendarConnector
{
    private readonly CalendarOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OAuthTokenCache _tokenCache = new();

    public MicrosoftGraphCalendarConnector(CalendarOptions options, IHttpClientFactory httpClientFactory)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
    }

    public string ConnectorName => "Microsoft";
    public bool IsConfigured => _options.Microsoft.HasCompleteCredentials;

    public async Task<IReadOnlyList<CalendarEventDto>> FetchEventsAsync(
        DateOnly date,
        TimeZoneInfo timeZone,
        CancellationToken ct = default)
    {
        var accessToken = await _tokenCache.GetAccessTokenAsync(_httpClientFactory, "microsoft-graph", _options.Microsoft, ct);
        if (accessToken is null)
        {
            return BuildFallbackEvents(date, timeZone);
        }

        var baseUrl = _options.Microsoft.ApiBaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/me/calendar/events?$top=50";

        using var doc = await _tokenCache.GetJsonAsync(
            _httpClientFactory,
            "microsoft-graph",
            _options.Microsoft,
            url,
            accessToken,
            req => req.Headers.TryAddWithoutValidation("Prefer", "outlook.timezone=\"UTC\""),
            ct);

        return ParseEvents(doc.RootElement, date, timeZone);
    }

    private static IReadOnlyList<CalendarEventDto> BuildFallbackEvents(DateOnly date, TimeZoneInfo timeZone)
    {
        return new[]
        {
            GoogleCalendarConnector.CreateLocalEvent(
                id: "ms-fallback-daily",
                provider: CalendarProvider.Teams,
                title: "Daily do time Projeto X",
                date: date,
                startTime: new TimeOnly(9, 0),
                endTime: new TimeOnly(9, 30),
                timeZone: timeZone,
                location: "Microsoft Teams",
                attendees: new[] { "Igor Grandin", "Squad Projeto X" },
                joinUrl: "https://teams.microsoft.com/l/meetup-join/projeto-x"),
            GoogleCalendarConnector.CreateLocalEvent(
                id: "ms-fallback-1on1",
                provider: CalendarProvider.Outlook,
                title: "1:1 com gestor",
                date: date,
                startTime: new TimeOnly(14, 0),
                endTime: new TimeOnly(14, 30),
                timeZone: timeZone,
                location: "Sala 3B / Híbrido",
                attendees: new[] { "Igor Grandin", "Gestor" },
                joinUrl: null),
            GoogleCalendarConnector.CreateLocalEvent(
                id: "ms-fallback-arquitetura",
                provider: CalendarProvider.Teams,
                title: "Revisão de arquitetura",
                date: date,
                startTime: new TimeOnly(15, 0),
                endTime: new TimeOnly(16, 0),
                timeZone: timeZone,
                location: "Microsoft Teams",
                attendees: new[] { "Igor Grandin", "Tech Leads" },
                joinUrl: "https://teams.microsoft.com/l/meetup-join/arquitetura")
        };
    }

    private static IReadOnlyList<CalendarEventDto> ParseEvents(JsonElement root, DateOnly targetDate, TimeZoneInfo timeZone)
    {
        var events = new List<CalendarEventDto>();
        if (!root.TryGetProperty("value", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return events;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("isCancelled", out var cancelledProp) &&
                cancelledProp.ValueKind == JsonValueKind.True)
            {
                continue;
            }

            var id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            var title = item.TryGetProperty("subject", out var subProp) ? subProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            if (!item.TryGetProperty("start", out var startEl) || !item.TryGetProperty("end", out var endEl))
            {
                continue;
            }

            var startRaw = startEl.TryGetProperty("dateTime", out var sDt) ? sDt.GetString() : null;
            var startTz = startEl.TryGetProperty("timeZone", out var sTz) ? sTz.GetString() : null;
            var endRaw = endEl.TryGetProperty("dateTime", out var eDt) ? eDt.GetString() : null;
            var endTz = endEl.TryGetProperty("timeZone", out var eTz) ? eTz.GetString() : null;

            var startLocal = CalendarTimeZoneResolver.ParseRemoteTimestamp(startRaw, startTz, timeZone);
            var endLocal = CalendarTimeZoneResolver.ParseRemoteTimestamp(endRaw, endTz, timeZone);
            if (startLocal is null || endLocal is null)
            {
                continue;
            }

            if (DateOnly.FromDateTime(startLocal.Value.DateTime) != targetDate)
            {
                continue;
            }

            var isOnlineMeeting = item.TryGetProperty("isOnlineMeeting", out var onlineProp) &&
                                  onlineProp.ValueKind == JsonValueKind.True;

            string? joinUrl = null;
            if (item.TryGetProperty("onlineMeeting", out var omEl) &&
                omEl.ValueKind == JsonValueKind.Object &&
                omEl.TryGetProperty("joinUrl", out var juProp))
            {
                joinUrl = juProp.GetString();
            }

            if (string.IsNullOrWhiteSpace(joinUrl) && item.TryGetProperty("onlineMeetingUrl", out var omuProp))
            {
                joinUrl = omuProp.GetString();
            }

            var provider = isOnlineMeeting ||
                           (!string.IsNullOrWhiteSpace(joinUrl) && joinUrl.Contains("teams.microsoft.com", StringComparison.OrdinalIgnoreCase))
                ? CalendarProvider.Teams
                : CalendarProvider.Outlook;

            string? location = null;
            if (item.TryGetProperty("location", out var locEl) &&
                locEl.ValueKind == JsonValueKind.Object &&
                locEl.TryGetProperty("displayName", out var locName))
            {
                location = locName.GetString();
            }

            var attendees = new List<string>();
            if (item.TryGetProperty("attendees", out var attProp) && attProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var att in attProp.EnumerateArray())
                {
                    if (!att.TryGetProperty("emailAddress", out var emailEl) || emailEl.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var name = emailEl.TryGetProperty("name", out var nProp) && !string.IsNullOrWhiteSpace(nProp.GetString())
                        ? nProp.GetString()
                        : emailEl.TryGetProperty("address", out var addrProp) ? addrProp.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        attendees.Add(name.Trim());
                    }
                }
            }

            events.Add(GoogleCalendarConnector.ToDto(id.Trim(), provider, title.Trim(), startLocal.Value, endLocal.Value, location, attendees, joinUrl));
        }

        return events;
    }
}
