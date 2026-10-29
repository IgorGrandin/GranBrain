namespace GranBrain.Core.Models;

public sealed record HabitItemDto(
    string Time,
    string Label,
    string FullText,
    bool Completed);

public sealed record KanbanTaskDto(
    string Id,
    string Text,
    bool Completed,
    string Column);

public sealed record KanbanColumnDto(
    string Name,
    IReadOnlyList<KanbanTaskDto> Tasks);

public sealed record KanbanBoardDto(
    string BoardName,
    string RelativePath,
    IReadOnlyList<KanbanColumnDto> Columns);

public sealed record VaultNoteSummaryDto(
    string Title,
    string RelativePath,
    string Category,
    DateTime LastModifiedUtc,
    IReadOnlyList<string> Tags,
    string Excerpt);

public sealed record DynamicNoteCreateRequest(
    string NoteType,
    string Title,
    string Content,
    string? Area = null,
    IReadOnlyList<string>? Tags = null,
    IDictionary<string, string>? ExtraFields = null);

public sealed record VaultOverviewDto(
    string VaultPath,
    string TodayNotePath,
    int TotalZettelkastenNotes,
    int TotalDailyNotes,
    int TotalIndices,
    int InboxPendingCount,
    int HabitsCompletedToday,
    int HabitsTotalToday,
    IReadOnlyList<HabitItemDto> TodayHabits,
    IReadOnlyList<KanbanBoardDto> KanbanBoards,
    IReadOnlyList<VaultNoteSummaryDto> RecentNotes);
