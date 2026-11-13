using System.Text.Json;
using GranBrain.Core.Interfaces;
using GranBrain.Core.Models;
using GranBrain.Core.Security;

namespace GranBrain.Api.Endpoints;

public static class CalendarEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapCalendarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/calendar").RequireAuthorization();

        group.MapGet("/today", async (string? date, ICalendarSyncService calendarSyncService, CancellationToken ct) =>
        {
            var today = await calendarSyncService.GetTodayAsync(date, ct);
            return Results.Ok(today);
        })
        .WithName("GetCalendarToday")
        .Produces<CalendarTodayDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/sync", async (string? date, HttpRequest httpRequest, ICalendarSyncService calendarSyncService, CancellationToken ct) =>
        {
            var request = await TryReadOptionalBodyAsync<CalendarSyncRequest>(httpRequest, ct);
            var result = await calendarSyncService.SyncAsync(request?.Date ?? date, request?.TargetFolder, ct);
            return Results.Ok(result);
        })
        .WithName("SyncCalendar")
        .Produces<CalendarSyncResultDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/events/{id}/note", async (string id, HttpRequest httpRequest, ICalendarSyncService calendarSyncService, CancellationToken ct) =>
        {
            var request = await TryReadOptionalBodyAsync<CalendarNoteRequest>(httpRequest, ct);
            var result = await calendarSyncService.CreateMeetingNoteAsync(id, request?.TargetFolder, request?.Date, ct);
            return Results.Ok(result);
        })
        .WithName("CreateCalendarEventNote")
        .Produces<CalendarNoteResultDto>(StatusCodes.Status200OK)
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
