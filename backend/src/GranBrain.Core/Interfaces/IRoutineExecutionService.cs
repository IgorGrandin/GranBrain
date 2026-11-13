using GranBrain.Core.Models;

namespace GranBrain.Core.Interfaces;

public interface IRoutineExecutionService
{
    Task<RoutinesOverviewDto> GetOverviewAsync(string? inboxFolder = null, CancellationToken ct = default);
    Task<RoutineRunResultDto> RunInboxTriageAsync(RoutineRunRequest? request = null, CancellationToken ct = default);
    Task<RoutineRunResultDto> RunJobAsync(string jobId, RoutineRunRequest? request = null, CancellationToken ct = default);
}
