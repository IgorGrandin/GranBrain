using System.Text.Json;
using GranBrain.Core.Interfaces;
using GranBrain.Core.Models;
using GranBrain.Core.Security;

namespace GranBrain.Api.Endpoints;

public static class RoutinesEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapRoutinesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/routines").RequireAuthorization();

        group.MapGet("/overview", async (string? inboxFolder, IRoutineExecutionService routineService, CancellationToken ct) =>
        {
            var overview = await routineService.GetOverviewAsync(inboxFolder, ct);
            return Results.Ok(overview);
        })
        .WithName("GetRoutinesOverview")
        .Produces<RoutinesOverviewDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/triage", async (
            string? date,
            string? inboxFolder,
            HttpRequest httpRequest,
            IRoutineExecutionService routineService,
            CancellationToken ct) =>
        {
            var body = await TryReadOptionalBodyAsync<RoutineRunRequest>(httpRequest, ct);
            var request = new RoutineRunRequest(
                Date: body?.Date ?? date,
                InboxFolder: body?.InboxFolder ?? inboxFolder);
            var result = await routineService.RunInboxTriageAsync(request, ct);
            return Results.Ok(result);
        })
        .WithName("TriageInboxNow")
        .Produces<RoutineRunResultDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/{jobId}/run", async (
            string jobId,
            string? date,
            string? inboxFolder,
            HttpRequest httpRequest,
            IRoutineExecutionService routineService,
            CancellationToken ct) =>
        {
            var body = await TryReadOptionalBodyAsync<RoutineRunRequest>(httpRequest, ct);
            var request = new RoutineRunRequest(
                Date: body?.Date ?? date,
                InboxFolder: body?.InboxFolder ?? inboxFolder);
            var result = await routineService.RunJobAsync(jobId, request, ct);
            return Results.Ok(result);
        })
        .WithName("RunRoutineJob")
        .Produces<RoutineRunResultDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<T?> TryReadOptionalBodyAsync<T>(HttpRequest httpRequest, CancellationToken ct) where T : class
    {
        if (httpRequest.ContentLength is 0)
        {
            return null;
        }

        if (httpRequest.ContentType is null || !httpRequest.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        httpRequest.EnableBuffering();
        using var reader = new StreamReader(httpRequest.Body, leaveOpen: true);
        var raw = await reader.ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(raw, JsonOptions);
        }
        catch (JsonException)
        {
            throw new VaultValidationException("invalid_request_body", "Corpo JSON inválido.");
        }
    }
}
