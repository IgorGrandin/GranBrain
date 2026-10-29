using GranBrain.Core.Models;

namespace GranBrain.Core.Interfaces;

public sealed record CliProcessResult(
    bool Executed,
    int ExitCode,
    string StdOut,
    string StdErr);

public interface ICliProcessRunner
{
    bool IsExecutableAvailable(string executableOrCommand);
    Task<CliProcessResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        int timeoutSeconds,
        CancellationToken ct = default);
}

public interface IAgentOrchestrator
{
    IReadOnlyList<AiProviderStatusDto> GetProvidersStatus();
    Task<AgentResponseEnvelope> ExecutePromptAsync(AgentChatRequest request, CancellationToken ct = default);
}
