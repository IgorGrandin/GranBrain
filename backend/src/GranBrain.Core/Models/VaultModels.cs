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

public sealed record IdeaItemDto(
    string Id,
    string Text,
    string Time,
    string Status,
    string SourcePath,
    IReadOnlyList<string>? Links = null);

public sealed record BrainstormHypothesisDto(
    string Text,
    string Confidence);

public sealed record BrainstormRiskDto(
    string Text,
    string Severity);

public sealed record BrainstormSessionDto(
    IdeaItemDto Idea,
    string NotePath,
    string Problem,
    IReadOnlyList<BrainstormHypothesisDto> Hypotheses,
    IReadOnlyList<BrainstormRiskDto> Risks,
    string SocraticQuestion,
    string? SocraticAnswer = null);

public sealed record IdeasOverviewDto(
    IReadOnlyList<IdeaItemDto> Ideas,
    int FleetingCount,
    int BrainstormCount,
    int PromotedCount,
    int DiscardedCount,
    BrainstormSessionDto? ActiveSession);

public sealed record FleetingIdeaCaptureRequest(
    string Text,
    string? Target = "fleeting",
    IReadOnlyList<string>? Links = null,
    IReadOnlyList<string>? Tags = null);

public sealed record BrainstormStartRequest(
    string? IdeaId = null,
    string? IdeaText = null,
    string? Problem = null,
    IReadOnlyList<BrainstormHypothesisDto>? Hypotheses = null,
    IReadOnlyList<BrainstormRiskDto>? Risks = null,
    string? SocraticQuestion = null,
    string? Project = null);

public sealed record IdeaPromoteRequest(
    string IdeaId,
    string Target = "permanente",
    string? Summary = null,
    string? Area = "profissional",
    string? SocraticAnswer = null);

public sealed record IdeaPromotionResultDto(
    IdeaItemDto Idea,
    string OriginPath,
    string? TargetPath,
    string Status);
