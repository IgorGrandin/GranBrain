namespace GranBrain.Core.Interfaces;

using GranBrain.Core.Models;

public interface ICalendarConnector
{
    string ConnectorName { get; }
    bool IsConfigured { get; }
    Task<IReadOnlyList<CalendarEventDto>> FetchEventsAsync(DateOnly date, TimeZoneInfo timeZone, CancellationToken ct = default);
}
