using System.Globalization;

namespace GranBrain.Infrastructure.Calendar;

public static class CalendarTimeZoneResolver
{
    public static TimeZoneInfo Resolve(string? timeZoneId)
    {
        var requested = string.IsNullOrWhiteSpace(timeZoneId) ? "America/Sao_Paulo" : timeZoneId.Trim();

        if (TryFindTimeZone(requested, out var resolved))
        {
            return resolved;
        }

        if (string.Equals(requested, "America/Sao_Paulo", StringComparison.OrdinalIgnoreCase) &&
            TryFindTimeZone("E. South America Standard Time", out var windowsBr))
        {
            return windowsBr;
        }

        if (string.Equals(requested, "E. South America Standard Time", StringComparison.OrdinalIgnoreCase) &&
            TryFindTimeZone("America/Sao_Paulo", out var ianaBr))
        {
            return ianaBr;
        }

        return TimeZoneInfo.Local;
    }

    public static DateTimeOffset ConvertToLocal(DateTimeOffset timestamp, TimeZoneInfo timeZone)
        => TimeZoneInfo.ConvertTime(timestamp, timeZone);

    public static DateTimeOffset? ParseRemoteTimestamp(string? rawDateTime, string? rawTimeZone, TimeZoneInfo fallbackTimeZone)
    {
        if (string.IsNullOrWhiteSpace(rawDateTime))
        {
            return null;
        }

        var trimmed = rawDateTime.Trim();

        // Explicit offset or trailing Z -> parse preserving exact instant then convert to target TZ
        if (trimmed.EndsWith("Z", StringComparison.OrdinalIgnoreCase) ||
            System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"[+-]\d{2}:\d{2}$"))
        {
            if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var exact))
            {
                return ConvertToLocal(exact, fallbackTimeZone);
            }
        }

        if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var naive))
        {
            var sourceTz = string.IsNullOrWhiteSpace(rawTimeZone)
                ? fallbackTimeZone
                : string.Equals(rawTimeZone.Trim(), "UTC", StringComparison.OrdinalIgnoreCase)
                    ? TimeZoneInfo.Utc
                    : Resolve(rawTimeZone);

            var unspecified = DateTime.SpecifyKind(naive, DateTimeKind.Unspecified);
            var offset = sourceTz.GetUtcOffset(unspecified);
            var dto = new DateTimeOffset(unspecified, offset);
            return ConvertToLocal(dto, fallbackTimeZone);
        }

        return null;
    }

    private static bool TryFindTimeZone(string id, out TimeZoneInfo timeZone)
    {
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            timeZone = TimeZoneInfo.Utc;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            timeZone = TimeZoneInfo.Utc;
            return false;
        }
    }
}
