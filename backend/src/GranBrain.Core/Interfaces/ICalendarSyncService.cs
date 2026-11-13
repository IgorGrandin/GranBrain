namespace GranBrain.Core.Interfaces;

using GranBrain.Core.Models;

public interface ICalendarSyncService
{
    Task<CalendarTodayDto> GetTodayAsync(string? date = null, CancellationToken ct = default);
    Task<CalendarSyncResultDto> SyncAsync(string? date = null, string? targetFolder = null, CancellationToken ct = default);
    Task<CalendarNoteResultDto> CreateMeetingNoteAsync(string eventId, string? targetFolder = null, string? date = null, CancellationToken ct = default);
}
