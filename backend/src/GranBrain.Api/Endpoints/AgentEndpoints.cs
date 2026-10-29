using GranBrain.Core.Interfaces;
using GranBrain.Core.Models;

namespace GranBrain.Api.Endpoints;

public static class AgentEndpoints
{
    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/agent").RequireAuthorization();

        group.MapGet("/providers", (IAgentOrchestrator orchestrator) =>
        {
            var providers = orchestrator.GetProvidersStatus();
            return Results.Ok(providers);
        });

        group.MapPost("/chat", async (AgentChatRequest request, IAgentOrchestrator orchestrator, CancellationToken ct) =>
        {
            var envelope = await orchestrator.ExecutePromptAsync(request, ct);
            return Results.Ok(envelope);
        });

        return app;
    }
}
