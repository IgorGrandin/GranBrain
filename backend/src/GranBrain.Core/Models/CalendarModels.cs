using System.Text.Json.Serialization;

namespace GranBrain.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CalendarProvider
{
    Google,
    Teams,
    Outlook
}

public sealed record CalendarEventDto(
    string Id,
    CalendarProvider Provider,
    string Title,
    string StartIso,
    string EndIso,
    string StartTime,
    string EndTime,
    int DurationMinutes,
    string? Location,
    IReadOnlyList<string> Attendees,
    string? JoinUrl,
    string? NotePath,
    string Status,
    bool HasConflict,
    double CalendarTopPx,
    double CalendarHeightPx,
    int CalendarColumn,
    int CalendarTotalColumns);

public sealed record CalendarTodayDto(
    string Date,
    string TimeZone,
    DateTime LastSyncedAt,
    int ConflictsCount,
    IReadOnlyList<string> ProvidersConnected,
    IReadOnlyList<CalendarEventDto> Events);

public sealed record CalendarSyncResultDto(
    string Date,
    string DailyNotePath,
    int SyncedCount,
    int ConflictsCount,
    IReadOnlyList<string> CreatedNotes,
    CalendarTodayDto Agenda);

public sealed record CalendarNoteResultDto(
    string EventId,
    string Title,
    string NotePath,
    bool Created);

public sealed record CalendarSyncRequest(
    string? Date = null,
    string? TargetFolder = null);

public sealed record CalendarNoteRequest(
    string? TargetFolder = null,
    string? Date = null);
