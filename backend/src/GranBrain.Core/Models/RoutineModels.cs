namespace GranBrain.Core.Models;

public sealed record RoutineStatusCardDto(
    string JobId,
    string Title,
    string ScheduleLabel,
    string Status,
    string StatusTone,
    string LastRunSummary);

public sealed record InboxQueueItemDto(
    string FileName,
    string RelativePath,
    string SuggestedDestination,
    string Tag,
    string Extension);

public sealed record RoutineExecutionLogDto(
    string Id,
    string JobId,
    string ExecutedAtIso,
    string DisplayTime,
    string Status,
    string Message,
    int ItemsProcessed,
    int ItemsFailed,
    IReadOnlyList<string> TouchedFiles);

public sealed record RoutinesOverviewDto(
    int InboxPendingCount,
    IReadOnlyList<RoutineStatusCardDto> Routines,
    IReadOnlyList<InboxQueueItemDto> InboxItems,
    IReadOnlyList<RoutineExecutionLogDto> Logs);

public sealed record InboxTriageFileResultDto(
    string SourceFile,
    string? DestinationNotePath,
    string? ArchivePath,
    string Category,
    bool Succeeded,
    string? ErrorMessage);

public sealed record RoutineRunResultDto(
    string JobId,
    bool Succeeded,
    int ProcessedCount,
    int FailedCount,
    string Summary,
    IReadOnlyList<string> TouchedFiles,
    IReadOnlyList<InboxTriageFileResultDto> FileResults,
    RoutinesOverviewDto Overview);

public sealed record RoutineRunRequest(
    string? Date = null,
    string? InboxFolder = null);
