using GranBrain.Core.Models;

namespace GranBrain.Core.Interfaces;

public interface IVaultService
{
    Task<VaultOverviewDto> GetOverviewAsync(DateOnly? date = null, CancellationToken ct = default);
    Task<IReadOnlyList<HabitItemDto>> GetDailyHabitsAsync(DateOnly? date = null, CancellationToken ct = default);
    Task<IReadOnlyList<HabitItemDto>> ToggleHabitAsync(string habitQuery, DateOnly? date = null, CancellationToken ct = default);
    Task<KanbanBoardDto> GetKanbanBoardAsync(string boardName, CancellationToken ct = default);
    Task<KanbanBoardDto> AddKanbanTaskAsync(string boardName, string column, string taskText, CancellationToken ct = default);
    Task<KanbanBoardDto> MoveKanbanTaskAsync(string boardName, string taskText, string targetColumn, CancellationToken ct = default);
    Task<VaultNoteSummaryDto> CreateStructuredNoteAsync(DynamicNoteCreateRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<VaultNoteSummaryDto>> QueryNotesAsync(string? query = null, string? category = null, int limit = 20, CancellationToken ct = default);
}
