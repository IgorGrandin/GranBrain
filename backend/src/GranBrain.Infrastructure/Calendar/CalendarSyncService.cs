using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GranBrain.Core.Configuration;
using GranBrain.Core.Interfaces;
using GranBrain.Core.Models;
using GranBrain.Core.Security;
using GranBrain.Infrastructure.Vault;

namespace GranBrain.Infrastructure.Calendar;

public sealed class CalendarSyncService : ICalendarSyncService
{
    private const double PixelsPerHour = 60.0;

    private static readonly Regex CalIdRegex = new(@"<!--\s*cal-id:(?<id>[^>]+?)\s*-->", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TimePrefixRegex = new(@"^\s*-\s*(?:\[[ xX]\]\s*)?(?<time>\d{2}:\d{2})", RegexOptions.Compiled);

    private readonly IReadOnlyList<ICalendarConnector> _connectors;
    private readonly CalendarOptions _options;
    private readonly VaultPathGuard _guard;
    private readonly VaultInitializer _initializer;
    private readonly IVaultService _vaultService;
    private readonly Func<DateTimeOffset> _utcNowProvider;

    public CalendarSyncService(
        IEnumerable<ICalendarConnector> connectors,
        CalendarOptions options,
        VaultPathGuard guard,
        VaultInitializer initializer,
        IVaultService vaultService,
        Func<DateTimeOffset>? utcNowProvider = null)
    {
        _connectors = connectors.ToList();
        _options = options;
        _guard = guard;
        _initializer = initializer;
        _vaultService = vaultService;
        _utcNowProvider = utcNowProvider ?? (() => DateTimeOffset.UtcNow);
    }

    public Task<CalendarTodayDto> GetTodayAsync(string? date = null, CancellationToken ct = default)
    {
        var tz = CalendarTimeZoneResolver.Resolve(_options.TimeZoneId);
        var targetDate = ParseDateOrDefault(date, tz);
        return BuildAgendaAsync(targetDate, tz, _options.MeetingNotesFolder, ct);
    }

    public async Task<CalendarSyncResultDto> SyncAsync(string? date = null, string? targetFolder = null, CancellationToken ct = default)
    {
        var tz = CalendarTimeZoneResolver.Resolve(_options.TimeZoneId);
        var targetDate = ParseDateOrDefault(date, tz);

        var rawMeetingFolder = string.IsNullOrWhiteSpace(targetFolder) ? _options.MeetingNotesFolder : targetFolder.Trim();
        _guard.ResolveSafePath(rawMeetingFolder);

        var rawDailyFolder = string.IsNullOrWhiteSpace(_options.DailyNotesFolder) ? "daily" : _options.DailyNotesFolder.Trim();
        _guard.ResolveSafePath(rawDailyFolder);

        var agenda = await BuildAgendaAsync(targetDate, tz, rawMeetingFolder, ct);

        var dailyPath = await EnsureDailyNoteFileAsync(targetDate, rawDailyFolder, ct);
        var relativeDailyPath = Path.GetRelativePath(_guard.VaultRoot, dailyPath).Replace('\\', '/');

        var lines = (await File.ReadAllLinesAsync(dailyPath, Encoding.UTF8, ct)).ToList();
        lines = UpsertCronogramaSection(lines, agenda.Events);
        lines = UpsertReunioesSection(lines, agenda.Events, rawMeetingFolder);

        await File.WriteAllLinesAsync(dailyPath, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), ct);

        return new CalendarSyncResultDto(
            Date: agenda.Date,
            DailyNotePath: relativeDailyPath,
            SyncedCount: agenda.Events.Count,
            ConflictsCount: agenda.ConflictsCount,
            CreatedNotes: Array.Empty<string>(),
            Agenda: agenda);
    }

    public async Task<CalendarNoteResultDto> CreateMeetingNoteAsync(
        string eventId,
        string? targetFolder = null,
        string? date = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            throw new VaultEntityNotFoundException("calendar_event_not_found", "Evento de calendário não informado.");
        }

        var rawFolder = string.IsNullOrWhiteSpace(targetFolder) ? _options.MeetingNotesFolder : targetFolder.Trim();
        var resolvedFolder = _guard.ResolveSafePath(rawFolder);

        var tz = CalendarTimeZoneResolver.Resolve(_options.TimeZoneId);
        var targetDate = ParseDateOrDefault(date, tz);
        var agenda = await BuildAgendaAsync(targetDate, tz, rawFolder, ct);

        var targetEvent = agenda.Events.FirstOrDefault(e => string.Equals(e.Id, eventId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (targetEvent is null)
        {
            throw new VaultEntityNotFoundException(
                "calendar_event_not_found",
                $"Evento '{eventId}' não foi encontrado na agenda de {targetDate:yyyy-MM-dd}.");
        }

        await _initializer.EnsureInitializedAsync(ct);
        Directory.CreateDirectory(resolvedFolder);

        var safeTitle = SanitizeFileName(targetEvent.Title);
        var noteFileName = $"Reunião - {safeTitle}.md";
        var fullNotePath = _guard.ResolveSafePath(Path.Combine(resolvedFolder, noteFileName));
        var relativeNotePath = Path.GetRelativePath(_guard.VaultRoot, fullNotePath).Replace('\\', '/');

        var created = !File.Exists(fullNotePath);
        string finalContent;
        if (created)
        {
            var templatePath = _guard.ResolveSafePath(Path.Combine("templates", "Reuniões.md"));
            var templateRaw = File.Exists(templatePath)
                ? await File.ReadAllTextAsync(templatePath, Encoding.UTF8, ct)
                : string.Empty;
            finalContent = BuildNewMeetingNote(templateRaw, targetEvent, targetDate);
        }
        else
        {
            var existingContent = await File.ReadAllTextAsync(fullNotePath, Encoding.UTF8, ct);
            finalContent = MergeMeetingNotePreservingSections(existingContent, targetEvent, targetDate);
        }

        await File.WriteAllTextAsync(fullNotePath, finalContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), ct);

        return new CalendarNoteResultDto(
            EventId: targetEvent.Id,
            Title: targetEvent.Title,
            NotePath: relativeNotePath,
            Created: created);
    }

    private async Task<CalendarTodayDto> BuildAgendaAsync(
        DateOnly targetDate,
        TimeZoneInfo timeZone,
        string rawMeetingFolder,
        CancellationToken ct)
    {
        var collected = new List<CalendarEventDto>();
        foreach (var connector in _connectors)
        {
            var items = await connector.FetchEventsAsync(targetDate, timeZone, ct);
            collected.AddRange(items);
        }

        var parsed = collected
            .Select(e => new
            {
                Event = e,
                Start = DateTimeOffset.Parse(e.StartIso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                End = DateTimeOffset.Parse(e.EndIso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            })
            .OrderBy(x => x.Start)
            .ThenBy(x => x.End)
            .ThenBy(x => x.Event.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var localNow = CalendarTimeZoneResolver.ConvertToLocal(_utcNowProvider(), timeZone);
        var localNowDate = DateOnly.FromDateTime(localNow.DateTime);
        var conflictPairs = 0;

        for (var i = 0; i < parsed.Count; i++)
        {
            for (var j = i + 1; j < parsed.Count; j++)
            {
                if (parsed[i].Start < parsed[j].End && parsed[j].Start < parsed[i].End)
                {
                    conflictPairs++;
                }
            }
        }

        var resolvedFolder = _guard.ResolveSafePath(string.IsNullOrWhiteSpace(rawMeetingFolder) ? _options.MeetingNotesFolder : rawMeetingFolder.Trim());
        var startDayMinutes = _options.DefaultStartHour * 60.0;

        var finalEvents = new List<CalendarEventDto>(parsed.Count);
        foreach (var item in parsed)
        {
            var group = parsed
                .Where(o => o.Start < item.End && item.Start < o.End)
                .ToList();

            var totalColumns = Math.Max(1, group.Count);
            var column = Math.Max(0, group.IndexOf(item));
            var hasConflict = totalColumns > 1;

            var startLocal = CalendarTimeZoneResolver.ConvertToLocal(item.Start, timeZone);
            var endLocal = CalendarTimeZoneResolver.ConvertToLocal(item.End, timeZone);
            var startMinutes = startLocal.Hour * 60.0 + startLocal.Minute;
            var endMinutes = endLocal.Hour * 60.0 + endLocal.Minute;

            var topPx = Math.Max(0, Math.Round((startMinutes - startDayMinutes) / 60.0 * PixelsPerHour, 2));
            var heightPx = Math.Max(20, Math.Round((endMinutes - startMinutes) / 60.0 * PixelsPerHour - 4, 2));

            string status;
            if (targetDate < localNowDate)
            {
                status = "past";
            }
            else if (targetDate > localNowDate)
            {
                status = "next";
            }
            else if (endLocal <= localNow)
            {
                status = "past";
            }
            else if (startLocal <= localNow && localNow < endLocal)
            {
                status = "now";
            }
            else
            {
                status = "next";
            }

            var safeTitle = SanitizeFileName(item.Event.Title);
            var expectedNoteFull = _guard.ResolveSafePath(Path.Combine(resolvedFolder, $"Reunião - {safeTitle}.md"));
            var notePath = File.Exists(expectedNoteFull)
                ? Path.GetRelativePath(_guard.VaultRoot, expectedNoteFull).Replace('\\', '/')
                : null;

            finalEvents.Add(item.Event with
            {
                Status = status,
                HasConflict = hasConflict,
                CalendarTopPx = topPx,
                CalendarHeightPx = heightPx,
                CalendarColumn = column,
                CalendarTotalColumns = totalColumns,
                NotePath = notePath
            });
        }

        var providersConnected = finalEvents
            .Select(e => e.Provider.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CalendarTodayDto(
            Date: targetDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TimeZone: timeZone.Id,
            LastSyncedAt: _utcNowProvider().UtcDateTime,
            ConflictsCount: conflictPairs,
            ProvidersConnected: providersConnected,
            Events: finalEvents);
    }

    private async Task<string> EnsureDailyNoteFileAsync(DateOnly date, string dailyFolder, CancellationToken ct)
    {
        if (string.Equals(dailyFolder.TrimEnd('/', '\\'), "daily", StringComparison.OrdinalIgnoreCase))
        {
            await _vaultService.GetDailyHabitsAsync(date, ct);
            return _guard.ResolveSafePath(Path.Combine("daily", $"{date:yyyy-MM-dd}.md"));
        }

        var dir = _guard.ResolveSafePath(dailyFolder);
        Directory.CreateDirectory(dir);
        var path = _guard.ResolveSafePath(Path.Combine(dir, $"{date:yyyy-MM-dd}.md"));
        if (!File.Exists(path))
        {
            await File.WriteAllTextAsync(
                path,
                $"---\ntype: daily\ncreated: \"{date:yyyy-MM-dd}\"\ntags:\n  - daily\n---\n\n# {date:yyyy-MM-dd}\n\n#### Cronograma\n\n",
                new UTF8Encoding(false),
                ct);
        }

        return path;
    }

    private static List<string> UpsertCronogramaSection(List<string> lines, IReadOnlyList<CalendarEventDto> events)
    {
        var updated = lines.ToList();
        var cronoIdx = updated.FindIndex(l => l.Trim().StartsWith("#### Cronograma", StringComparison.OrdinalIgnoreCase));

        if (cronoIdx < 0)
        {
            updated.Add(string.Empty);
            updated.Add("#### Cronograma");
            updated.Add(string.Empty);
            cronoIdx = updated.Count - 2;
        }

        var sectionEnd = updated.Count;
        for (var i = cronoIdx + 1; i < updated.Count; i++)
        {
            if (updated[i].Trim().StartsWith("#", StringComparison.Ordinal))
            {
                sectionEnd = i;
                break;
            }
        }

        // Preserve non-calendar lines inside #### Cronograma, remove old <!-- cal-id:... --> lines in this section
        var keptSectionLines = new List<string>();
        for (var i = cronoIdx + 1; i < sectionEnd; i++)
        {
            if (!CalIdRegex.IsMatch(updated[i]))
            {
                keptSectionLines.Add(updated[i]);
            }
        }

        // Insert each calendar event chronologically (never using a checkbox [ ])
        foreach (var ev in events)
        {
            var calLine = $"- {ev.StartTime}–{ev.EndTime} · {ev.Title} ({ev.Provider}) <!-- cal-id:{ev.Id} -->";
            var insertAt = keptSectionLines.Count;

            // Trim trailing blank lines when finding chronological slot
            while (insertAt > 0 && string.IsNullOrWhiteSpace(keptSectionLines[insertAt - 1]))
            {
                insertAt--;
            }

            for (var i = 0; i < keptSectionLines.Count; i++)
            {
                var timeMatch = TimePrefixRegex.Match(keptSectionLines[i]);
                if (timeMatch.Success && string.CompareOrdinal(timeMatch.Groups["time"].Value, ev.StartTime) > 0)
                {
                    insertAt = i;
                    break;
                }
            }

            keptSectionLines.Insert(insertAt, calLine);
        }

        var result = new List<string>(updated.Count + events.Count);
        result.AddRange(updated.Take(cronoIdx + 1));
        result.AddRange(keptSectionLines);
        result.AddRange(updated.Skip(sectionEnd));
        return result;
    }

    private List<string> UpsertReunioesSection(List<string> lines, IReadOnlyList<CalendarEventDto> events, string rawMeetingFolder)
    {
        var updated = lines.ToList();
        var headerIdx = updated.FindIndex(l => l.Trim().StartsWith("## 🤝🏻 Reuniões", StringComparison.OrdinalIgnoreCase));

        var eventLines = events.Select(ev =>
        {
            var safeTitle = SanitizeFileName(ev.Title);
            return $"- {ev.StartTime}–{ev.EndTime} · [[Reunião - {safeTitle}|{ev.Title}]] ({ev.Provider}) <!-- cal-id:{ev.Id} -->";
        }).ToList();

        if (headerIdx < 0)
        {
            if (updated.Count > 0 && !string.IsNullOrWhiteSpace(updated[^1]))
            {
                updated.Add(string.Empty);
            }

            updated.Add("## 🤝🏻 Reuniões");
            updated.Add(string.Empty);
            updated.AddRange(eventLines);
            return updated;
        }

        var sectionEnd = updated.Count;
        for (var i = headerIdx + 1; i < updated.Count; i++)
        {
            var trimmed = updated[i].Trim();
            if (trimmed.StartsWith("## ", StringComparison.Ordinal) || trimmed == "---")
            {
                sectionEnd = i;
                break;
            }
        }

        var dataviewIdx = -1;
        for (var i = headerIdx + 1; i < sectionEnd; i++)
        {
            if (updated[i].TrimStart().StartsWith("```dataview", StringComparison.OrdinalIgnoreCase))
            {
                dataviewIdx = i;
                break;
            }
        }

        var preDataviewEnd = dataviewIdx >= 0 ? dataviewIdx : sectionEnd;
        var preservedBefore = new List<string>();
        for (var i = headerIdx + 1; i < preDataviewEnd; i++)
        {
            if (!CalIdRegex.IsMatch(updated[i]) && updated[i].Trim() != "-")
            {
                preservedBefore.Add(updated[i]);
            }
        }

        while (preservedBefore.Count > 0 && string.IsNullOrWhiteSpace(preservedBefore[^1]))
        {
            preservedBefore.RemoveAt(preservedBefore.Count - 1);
        }

        var newSectionBody = new List<string>();
        newSectionBody.AddRange(preservedBefore);
        if (newSectionBody.Count == 0 || !string.IsNullOrWhiteSpace(newSectionBody[0]))
        {
            newSectionBody.Insert(0, string.Empty);
        }
        newSectionBody.AddRange(eventLines);
        newSectionBody.Add(string.Empty);

        var result = new List<string>();
        result.AddRange(updated.Take(headerIdx + 1));
        result.AddRange(newSectionBody);
        if (dataviewIdx >= 0)
        {
            result.AddRange(updated.Skip(dataviewIdx));
        }
        else
        {
            result.AddRange(updated.Skip(sectionEnd));
        }

        return result;
    }

    private static string BuildNewMeetingNote(string templateRaw, CalendarEventDto ev, DateOnly date)
    {
        var attendeesYaml = ev.Attendees.Count == 0
            ? "[]"
            : "\n" + string.Join("\n", ev.Attendees.Select(a => $"  - \"{a.Replace("\"", "'")}\""));
        var attendeesLine = ev.Attendees.Count == 0 ? "-" : string.Join(", ", ev.Attendees);
        var linkOrLocation = !string.IsNullOrWhiteSpace(ev.JoinUrl)
            ? $"{ev.JoinUrl}" + (string.IsNullOrWhiteSpace(ev.Location) ? string.Empty : $" ({ev.Location})")
            : (ev.Location ?? "-");

        var header = $"""
            ---
            type: reuniao
            created: "{date:yyyy-MM-dd} {ev.StartTime}"
            provider: "{ev.Provider}"
            calendar_id: "{ev.Id}"
            tags:
              - reuniao
              - {ev.Provider.ToString().ToLowerInvariant()}
            participantes: {attendeesYaml}
            status: pendente
            aliases: []
            ---

            # 🤝🏻 Reunião — {ev.Title}

            > [!info] Metadados da Reunião
            > - **Data/Hora:** {date:yyyy-MM-dd} {ev.StartTime}–{ev.EndTime}
            > - **Provedor:** {ev.Provider}
            > - **Participantes:** {attendeesLine}
            > - **Link da Chamada / Local:** {linkOrLocation}

            ---
            """.Replace("\r\n", "\n");

        var bodyMarker = "## 🎯 Pauta & Objetivos";
        var normalizedTemplate = templateRaw.Replace("\r\n", "\n");
        var markerIdx = normalizedTemplate.IndexOf(bodyMarker, StringComparison.Ordinal);
        var body = markerIdx >= 0
            ? normalizedTemplate[markerIdx..]
            : """
              ## 🎯 Pauta & Objetivos
              - 

              ---

              ## 📝 Discussão & Anotações
              - 

              ---

              ## ✅ Plano de Ação & Tarefas
              - [ ] 

              ---

              ## 🔗 Notas Relacionadas
              ```dataview
              LIST
              FROM "zettelkasten"
              WHERE contains(file.outlinks, this.file.link)
              ```
              """.Replace("\r\n", "\n");

        return $"{header}\n\n{body.TrimStart('\n')}";
    }

    private static string MergeMeetingNotePreservingSections(string existingContent, CalendarEventDto ev, DateOnly date)
    {
        var normalized = existingContent.Replace("\r\n", "\n");
        var updatedHeader = BuildNewMeetingNote(string.Empty, ev, date);
        var headerSplitIdx = updatedHeader.IndexOf("## 🎯 Pauta & Objetivos", StringComparison.Ordinal);
        var freshTop = headerSplitIdx >= 0 ? updatedHeader[..headerSplitIdx].TrimEnd('\n') : updatedHeader;

        // Preserve from ## 🎯 Pauta & Objetivos or ## 📝 Discussão & Anotações onwards
        var preserveIdx = normalized.IndexOf("## 🎯 Pauta & Objetivos", StringComparison.Ordinal);
        if (preserveIdx < 0)
        {
            preserveIdx = normalized.IndexOf("## 📝 Discussão & Anotações", StringComparison.Ordinal);
        }
        if (preserveIdx < 0)
        {
            preserveIdx = normalized.IndexOf("## ✅ Plano de Ação & Tarefas", StringComparison.Ordinal);
        }

        if (preserveIdx >= 0)
        {
            return $"{freshTop}\n\n{normalized[preserveIdx..]}";
        }

        return $"{freshTop}\n\n{normalized}";
    }

    private DateOnly ParseDateOrDefault(string? date, TimeZoneInfo tz)
    {
        if (string.IsNullOrWhiteSpace(date))
        {
            return DateOnly.FromDateTime(CalendarTimeZoneResolver.ConvertToLocal(_utcNowProvider(), tz).DateTime);
        }

        if (!DateOnly.TryParseExact(date.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            throw new VaultValidationException("invalid_calendar_date", $"Data inválida '{date}'. Use o formato yyyy-MM-dd.");
        }

        return parsed;
    }

    private static string SanitizeFileName(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(title.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "Reuniao" : cleaned;
    }
}
