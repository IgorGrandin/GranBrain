using GranBrain.Core.Interfaces;
using GranBrain.Core.Models;
using GranBrain.Infrastructure.Vault;

namespace GranBrain.Api.Endpoints;

public sealed record HabitToggleRequest(string HabitQuery, string? Date = null);
public sealed record KanbanTaskAddRequest(string Column, string TaskText);
public sealed record KanbanTaskMoveRequest(string TaskText, string TargetColumn);

public static class VaultEndpoints
{
    public static IEndpointRouteBuilder MapVaultEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/vault").RequireAuthorization();

        group.MapPost("/bootstrap", async (VaultInitializer initializer, CancellationToken ct) =>
        {
            var res = await initializer.EnsureInitializedAsync(ct);
            return Results.Ok(res);
        });

        group.MapGet("/overview", async (IVaultService vaultService, CancellationToken ct) =>
        {
            var overview = await vaultService.GetOverviewAsync(null, ct);
            return Results.Ok(overview);
        });

        group.MapGet("/habits", async (IVaultService vaultService, CancellationToken ct) =>
        {
            var habits = await vaultService.GetDailyHabitsAsync(null, ct);
            return Results.Ok(habits);
        });

        group.MapPost("/habits/toggle", async (HabitToggleRequest request, IVaultService vaultService, CancellationToken ct) =>
        {
            DateOnly? parsedDate = null;
            if (!string.IsNullOrWhiteSpace(request.Date) && DateOnly.TryParse(request.Date, out var d))
            {
                parsedDate = d;
            }

            var updated = await vaultService.ToggleHabitAsync(request.HabitQuery, parsedDate, ct);
            return Results.Ok(updated);
        });

        group.MapGet("/kanban/{board}", async (string board, IVaultService vaultService, CancellationToken ct) =>
        {
            var dto = await vaultService.GetKanbanBoardAsync(board, ct);
            return Results.Ok(dto);
        });

        group.MapPost("/kanban/{board}/tasks", async (string board, KanbanTaskAddRequest request, IVaultService vaultService, CancellationToken ct) =>
        {
            var dto = await vaultService.AddKanbanTaskAsync(board, request.Column, request.TaskText, ct);
            return Results.Ok(dto);
        });

        group.MapPut("/kanban/{board}/tasks/move", async (string board, KanbanTaskMoveRequest request, IVaultService vaultService, CancellationToken ct) =>
        {
            var dto = await vaultService.MoveKanbanTaskAsync(board, request.TaskText, request.TargetColumn, ct);
            return Results.Ok(dto);
        });

        group.MapPost("/notes", async (DynamicNoteCreateRequest request, IVaultService vaultService, CancellationToken ct) =>
        {
            var created = await vaultService.CreateStructuredNoteAsync(request, ct);
            return Results.Created($"/api/vault/notes?query={Uri.EscapeDataString(created.Title)}", created);
        });

        group.MapGet("/notes", async (string? query, string? category, int? limit, IVaultService vaultService, CancellationToken ct) =>
        {
            var list = await vaultService.QueryNotesAsync(query, category, limit ?? 20, ct);
            return Results.Ok(list);
        });

        return app;
    }
}
