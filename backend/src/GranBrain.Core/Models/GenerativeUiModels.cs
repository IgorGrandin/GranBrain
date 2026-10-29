namespace GranBrain.Core.Models;

public static class GenerativeUiBlockTypes
{
    public const string KpiGrid = "kpi-grid";
    public const string HabitChecklist = "habit-checklist";
    public const string KanbanBoard = "kanban-board";
    public const string DynamicForm = "dynamic-form";
    public const string MarkdownNoteList = "markdown-note-list";
}

public sealed record KpiMetricItemDto(
    string Label,
    string Value,
    string? Subtitle = null,
    string? Accent = null);

public sealed record DynamicFormFieldDto(
    string Key,
    string Label,
    string FieldType,
    bool Required = false,
    string? Placeholder = null,
    string? DefaultValue = null,
    IReadOnlyList<string>? Options = null);

public sealed record DynamicFormSchemaDto(
    string FormId,
    string NoteType,
    string SubmitLabel,
    IReadOnlyList<DynamicFormFieldDto> Fields);

public sealed record GenerativeUiBlock(
    string Id,
    string Type,
    string Title,
    string? Subtitle = null,
    IReadOnlyList<KpiMetricItemDto>? Metrics = null,
    IReadOnlyList<HabitItemDto>? Habits = null,
    KanbanBoardDto? Kanban = null,
    DynamicFormSchemaDto? Form = null,
    IReadOnlyList<VaultNoteSummaryDto>? Notes = null);

public sealed record AiProviderStatusDto(
    string Id,
    string DisplayName,
    string PlanName,
    string ExecutablePath,
    bool Installed,
    string Description);

public sealed record AgentChatRequest(
    string Prompt,
    string Provider = "codex",
    bool VoiceMode = false);

public sealed record AgentResponseEnvelope(
    string ProviderUsed,
    bool ExecutedViaCli,
    string ReplyText,
    IReadOnlyList<string> TouchedFiles,
    IReadOnlyList<GenerativeUiBlock> UiBlocks,
    DateTime TimestampUtc);
