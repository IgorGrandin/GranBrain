using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using GranBrain.Core.Configuration;
using GranBrain.Core.Interfaces;
using GranBrain.Core.Models;
using GranBrain.Core.Security;

namespace GranBrain.Infrastructure.Calendar;

internal sealed class OAuthTokenCache
{
    private sealed record CachedToken(string AccessToken, DateTimeOffset ExpiresAtUtc);

    private readonly ConcurrentDictionary<string, CachedToken> _tokens = new(StringComparer.OrdinalIgnoreCase);

    public async Task<string?> GetAccessTokenAsync(
        IHttpClientFactory httpClientFactory,
        string providerKey,
        OAuthCalendarProviderOptions creds,
        CancellationToken ct)
    {
        if (!creds.HasAnyCredential)
        {
            return null;
        }

        if (!creds.HasCompleteCredentials)
        {
            throw new VaultValidationException(
                "calendar_auth_failed",
                $"Credenciais OAuth parciais para '{providerKey}': preencha ClientId, ClientSecret e RefreshToken ou deixe todos vazios.");
        }

        var cacheKey = $"{providerKey}:{creds.TokenUri}:{creds.ClientId}";
        if (_tokens.TryGetValue(cacheKey, out var cached) && cached.ExpiresAtUtc > DateTimeOffset.UtcNow.AddSeconds(30))
        {
            return cached.AccessToken;
        }

        var client = httpClientFactory.CreateClient(providerKey);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = creds.ClientId,
            ["client_secret"] = creds.ClientSecret,
            ["refresh_token"] = creds.RefreshToken,
            ["grant_type"] = "refresh_token"
        });

        using var response = await client.PostAsync(creds.TokenUri, form, ct);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden || !response.IsSuccessStatusCode)
        {
            _tokens.TryRemove(cacheKey, out _);
            throw new VaultValidationException(
                "calendar_auth_failed",
                $"Falha OAuth ao renovar access token para '{providerKey}' (HTTP {(int)response.StatusCode}).");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;

        if (!root.TryGetProperty("access_token", out var tokenProp) || string.IsNullOrWhiteSpace(tokenProp.GetString()))
        {
            throw new VaultValidationException(
                "calendar_auth_failed",
                $"Resposta OAuth sem access_token válido para '{providerKey}'.");
        }

        var accessToken = tokenProp.GetString()!;
        var expiresInSeconds = root.TryGetProperty("expires_in", out var expProp) && expProp.TryGetInt32(out var secs)
            ? Math.Max(60, secs)
            : 3600;

        _tokens[cacheKey] = new CachedToken(accessToken, DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds));
        return accessToken;
    }

    public async Task<JsonDocument> GetJsonAsync(
        IHttpClientFactory httpClientFactory,
        string providerKey,
        OAuthCalendarProviderOptions creds,
        string requestUrl,
        string accessToken,
        Action<HttpRequestMessage>? configureRequest,
        CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(providerKey);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        configureRequest?.Invoke(request);

        using var response = await client.SendAsync(request, ct);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _tokens.TryRemove($"{providerKey}:{creds.TokenUri}:{creds.ClientId}", out _);
            throw new VaultValidationException(
                "calendar_auth_failed",
                $"Acesso negado pela API do calendário '{providerKey}' (HTTP {(int)response.StatusCode}).");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new VaultValidationException(
                "calendar_auth_failed",
                $"Erro ao consultar eventos de '{providerKey}' (HTTP {(int)response.StatusCode}).");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }
}

public sealed class GoogleCalendarConnector : ICalendarConnector
{
    private readonly CalendarOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OAuthTokenCache _tokenCache = new();

    public GoogleCalendarConnector(CalendarOptions options, IHttpClientFactory httpClientFactory)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
    }

    public string ConnectorName => "Google";
    public bool IsConfigured => _options.Google.HasCompleteCredentials;

    public async Task<IReadOnlyList<CalendarEventDto>> FetchEventsAsync(
        DateOnly date,
        TimeZoneInfo timeZone,
        CancellationToken ct = default)
    {
        var accessToken = await _tokenCache.GetAccessTokenAsync(_httpClientFactory, "google-calendar", _options.Google, ct);
        if (accessToken is null)
        {
            return BuildFallbackEvents(date, timeZone);
        }

        var localStart = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), timeZone.GetUtcOffset(date.ToDateTime(TimeOnly.MinValue)));
        var localEnd = localStart.AddDays(1);
        var timeMin = Uri.EscapeDataString(localStart.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        var timeMax = Uri.EscapeDataString(localEnd.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        var calendarId = string.IsNullOrWhiteSpace(_options.Google.CalendarId) ? "primary" : _options.Google.CalendarId.Trim();
        var baseUrl = _options.Google.ApiBaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/calendars/{Uri.EscapeDataString(calendarId)}/events?singleEvents=true&orderBy=startTime&timeMin={timeMin}&timeMax={timeMax}";

        using var doc = await _tokenCache.GetJsonAsync(_httpClientFactory, "google-calendar", _options.Google, url, accessToken, null, ct);
        return ParseEvents(doc.RootElement, date, timeZone);
    }

    internal static CalendarEventDto CreateLocalEvent(
        string id,
        CalendarProvider provider,
        string title,
        DateOnly date,
        TimeOnly startTime,
        TimeOnly endTime,
        TimeZoneInfo timeZone,
        string? location,
        IReadOnlyList<string> attendees,
        string? joinUrl)
    {
        var startLocal = date.ToDateTime(startTime);
        var endLocal = date.ToDateTime(endTime);
        var startDto = new DateTimeOffset(startLocal, timeZone.GetUtcOffset(startLocal));
        var endDto = new DateTimeOffset(endLocal, timeZone.GetUtcOffset(endLocal));
        return ToDto(id, provider, title, startDto, endDto, location, attendees, joinUrl);
    }

    internal static CalendarEventDto ToDto(
        string id,
        CalendarProvider provider,
        string title,
        DateTimeOffset startLocal,
        DateTimeOffset endLocal,
        string? location,
        IReadOnlyList<string> attendees,
        string? joinUrl)
    {
        var duration = Math.Max(0, (int)Math.Round((endLocal - startLocal).TotalMinutes));
        return new CalendarEventDto(
            Id: id,
            Provider: provider,
            Title: title,
            StartIso: startLocal.ToString("O", CultureInfo.InvariantCulture),
            EndIso: endLocal.ToString("O", CultureInfo.InvariantCulture),
            StartTime: startLocal.ToString("HH:mm", CultureInfo.InvariantCulture),
            EndTime: endLocal.ToString("HH:mm", CultureInfo.InvariantCulture),
            DurationMinutes: duration,
            Location: location,
            Attendees: attendees,
            JoinUrl: joinUrl,
            NotePath: null,
            Status: "next",
            HasConflict: false,
            CalendarTopPx: 0,
            CalendarHeightPx: 0,
            CalendarColumn: 0,
            CalendarTotalColumns: 1);
    }

    private static IReadOnlyList<CalendarEventDto> BuildFallbackEvents(DateOnly date, TimeZoneInfo timeZone)
    {
        return new[]
        {
            CreateLocalEvent(
                id: "gcal-fallback-alinhamento",
                provider: CalendarProvider.Google,
                title: "Alinhamento GranBrain",
                date: date,
                startTime: new TimeOnly(9, 30),
                endTime: new TimeOnly(10, 30),
                timeZone: timeZone,
                location: "Google Meet",
                attendees: new[] { "Igor Grandin", "Equipe Produto" },
                joinUrl: "https://meet.google.com/granbrain-sync"),
            CreateLocalEvent(
                id: "gcal-fallback-dentista",
                provider: CalendarProvider.Google,
                title: "Dentista",
                date: date,
                startTime: new TimeOnly(15, 30),
                endTime: new TimeOnly(16, 0),
                timeZone: timeZone,
                location: "Consultório Odontológico",
                attendees: new[] { "Igor Grandin" },
                joinUrl: null)
        };
    }

    private static IReadOnlyList<CalendarEventDto> ParseEvents(JsonElement root, DateOnly targetDate, TimeZoneInfo timeZone)
    {
        var events = new List<CalendarEventDto>();
        if (!root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return events;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("status", out var statusProp) &&
                string.Equals(statusProp.GetString(), "cancelled", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            var title = item.TryGetProperty("summary", out var sumProp) ? sumProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            if (!item.TryGetProperty("start", out var startEl) || !item.TryGetProperty("end", out var endEl))
            {
                continue;
            }

            var startRaw = startEl.TryGetProperty("dateTime", out var sDt) ? sDt.GetString()
                : startEl.TryGetProperty("date", out var sD) ? sD.GetString() : null;
            var endRaw = endEl.TryGetProperty("dateTime", out var eDt) ? eDt.GetString()
                : endEl.TryGetProperty("date", out var eD) ? eD.GetString() : null;
            var startTz = startEl.TryGetProperty("timeZone", out var sTz) ? sTz.GetString() : null;
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

            var location = item.TryGetProperty("location", out var locProp) ? locProp.GetString() : null;
            var joinUrl = item.TryGetProperty("hangoutLink", out var hangProp) ? hangProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(joinUrl) &&
                item.TryGetProperty("conferenceData", out var conf) &&
                conf.TryGetProperty("entryPoints", out var eps) &&
                eps.ValueKind == JsonValueKind.Array)
            {
                foreach (var ep in eps.EnumerateArray())
                {
                    var uri = ep.TryGetProperty("uri", out var uriProp) ? uriProp.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(uri))
                    {
                        joinUrl = uri;
                        break;
                    }
                }
            }

            var attendees = new List<string>();
            if (item.TryGetProperty("attendees", out var attProp) && attProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var att in attProp.EnumerateArray())
                {
                    var name = att.TryGetProperty("displayName", out var dn) && !string.IsNullOrWhiteSpace(dn.GetString())
                        ? dn.GetString()
                        : att.TryGetProperty("email", out var em) ? em.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        attendees.Add(name.Trim());
                    }
                }
            }

            events.Add(ToDto(id.Trim(), CalendarProvider.Google, title.Trim(), startLocal.Value, endLocal.Value, location, attendees, joinUrl));
        }

        return events;
    }
}
