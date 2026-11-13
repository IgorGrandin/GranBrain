using System.Globalization;
using GranBrain.Core.Configuration;
using GranBrain.Core.Interfaces;
using GranBrain.Core.Models;
using GranBrain.Infrastructure.Calendar;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GranBrain.Infrastructure.Jobs;

public sealed class RoutineSchedulerHostedService : BackgroundService
{
    private readonly IRoutineExecutionService _routineService;
    private readonly RoutinesOptions _options;
    private readonly ILogger<RoutineSchedulerHostedService> _logger;
    private readonly Func<DateTimeOffset> _utcNowProvider;

    private DateOnly? _lastMorningPrepDate;
    private DateOnly? _lastNightlyClosureDate;
    private DateTimeOffset? _lastInboxTriageUtc;

    public RoutineSchedulerHostedService(
        IRoutineExecutionService routineService,
        RoutinesOptions options,
        ILogger<RoutineSchedulerHostedService> logger,
        Func<DateTimeOffset>? utcNowProvider = null)
    {
        _routineService = routineService;
        _options = options;
        _logger = logger;
        _utcNowProvider = utcNowProvider ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task ExecuteTickAsync(CancellationToken ct = default)
    {
        if (!_options.EnableBackgroundScheduler)
        {
            return;
        }

        var tz = CalendarTimeZoneResolver.Resolve(_options.TimeZoneId);
        var nowUtc = _utcNowProvider();
        var localNow = CalendarTimeZoneResolver.ConvertToLocal(nowUtc, tz);
        var today = DateOnly.FromDateTime(localNow.DateTime);
        var currentTime = TimeOnly.FromDateTime(localNow.DateTime);

        var morningTime = ParseScheduleTime(_options.MorningPrepTime, new TimeOnly(6, 0));
        var nightlyTime = ParseScheduleTime(_options.NightlyClosureTime, new TimeOnly(23, 30));
        var dateParam = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (currentTime >= morningTime && _lastMorningPrepDate != today)
        {
            await _routineService.RunJobAsync(
                RoutineExecutionService.MorningPrepJobId,
                new RoutineRunRequest(Date: dateParam),
                ct);
            _lastMorningPrepDate = today;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.InboxIntervalMinutes));
        if (_lastInboxTriageUtc is null || nowUtc - _lastInboxTriageUtc.Value >= interval)
        {
            await _routineService.RunInboxTriageAsync(new RoutineRunRequest(Date: dateParam), ct);
            _lastInboxTriageUtc = nowUtc;
        }

        if (currentTime >= nightlyTime && _lastNightlyClosureDate != today)
        {
            await _routineService.RunJobAsync(
                RoutineExecutionService.NightlyClosureJobId,
                new RoutineRunRequest(Date: dateParam),
                ct);
            _lastNightlyClosureDate = today;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableBackgroundScheduler)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExecuteTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante ciclo do RoutineSchedulerHostedService.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private static TimeOnly ParseScheduleTime(string? rawTime, TimeOnly fallback)
    {
        if (!string.IsNullOrWhiteSpace(rawTime) &&
            TimeOnly.TryParseExact(rawTime.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }
}
