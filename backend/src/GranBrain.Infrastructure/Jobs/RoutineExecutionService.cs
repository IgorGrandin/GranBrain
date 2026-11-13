using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GranBrain.Core.Configuration;
using GranBrain.Core.Interfaces;
using GranBrain.Core.Models;
using GranBrain.Core.Security;
using GranBrain.Infrastructure.Calendar;
using GranBrain.Infrastructure.Vault;

namespace GranBrain.Infrastructure.Jobs;

public sealed class RoutineExecutionService : IRoutineExecutionService
{
    public const string MorningPrepJobId = "MorningPrepJob";
    public const string InboxTriageJobId = "InboxTriageJob";
    public const string NightlyClosureJobId = "NightlyClosureJob";

    private const string MorningPrepMarker = "<!-- routine-id:morning-prep -->";
    private const string NightlyClosureMarker = "<!-- routine-id:nightly-closure -->";

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static readonly Regex HabitLineRegex = new(
        @"^(?<prefix>\s*-\s*\[(?<state>[ xX])\]\s*)(?:(?<time>\d{2}:\d{2})\s*-\s*)?(?<label>.+)$",
        RegexOptions.Compiled);

    private static readonly Regex InlineTagRegex = new(
        @"(?<!\w)#([A-Za-z0-9_/-]+)",
        RegexOptions.Compiled);

    private static readonly string[] MeetingKeywords =
    {
        "reunião", "reuniao", "ata", "daily", "alinhamento", "meeting", "1:1", "kickoff", "retro", "sync"
    };

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "curso", "para", "sobre", "como", "entre", "desde", "depois", "antes", "com", "sem", "uma", "uns", "das", "dos"
    };

    private readonly SemaphoreSlim _executionLock = new(1, 1);
    private readonly RoutinesOptions _options;
    private readonly VaultPathGuard _guard;
    private readonly VaultInitializer _initializer;
    private readonly IVaultService _vaultService;
    private readonly ICliProcessRunner? _cliRunner;
    private readonly GranBrainOptions? _granBrainOptions;
    private readonly Func<DateTimeOffset> _utcNowProvider;

    public RoutineExecutionService(
        RoutinesOptions options,
        VaultPathGuard guard,
        VaultInitializer initializer,
        IVaultService vaultService,
        ICliProcessRunner? cliRunner = null,
        GranBrainOptions? granBrainOptions = null,
        Func<DateTimeOffset>? utcNowProvider = null)
    {
        _options = options;
        _guard = guard;
        _initializer = initializer;
        _vaultService = vaultService;
        _cliRunner = cliRunner;
        _granBrainOptions = granBrainOptions;
        _utcNowProvider = utcNowProvider ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<RoutinesOverviewDto> GetOverviewAsync(string? inboxFolder = null, CancellationToken ct = default)
    {
        var rawInbox = ResolveRawInboxFolder(inboxFolder);
        ValidateConfiguredFolders(rawInbox);

        await _executionLock.WaitAsync(ct);
        try
        {
            return await BuildOverviewCoreAsync(rawInbox, ct);
        }
        finally
        {
            _executionLock.Release();
        }
    }

    public Task<RoutineRunResultDto> RunInboxTriageAsync(RoutineRunRequest? request = null, CancellationToken ct = default)
        => RunJobAsync(InboxTriageJobId, request, ct);

    public async Task<RoutineRunResultDto> RunJobAsync(string jobId, RoutineRunRequest? request = null, CancellationToken ct = default)
    {
        var canonicalJobId = NormalizeJobId(jobId);
        var rawInbox = ResolveRawInboxFolder(request?.InboxFolder);
        ValidateConfiguredFolders(rawInbox);

        var tz = CalendarTimeZoneResolver.Resolve(_options.TimeZoneId);
        var targetDate = ParseDateOrDefault(request?.Date, tz);

        await _executionLock.WaitAsync(ct);
        try
        {
            return canonicalJobId switch
            {
                MorningPrepJobId => await RunMorningPrepCoreAsync(targetDate, rawInbox, tz, ct),
                InboxTriageJobId => await RunInboxTriageCoreAsync(targetDate, rawInbox, tz, ct),
                NightlyClosureJobId => await RunNightlyClosureCoreAsync(targetDate, rawInbox, tz, ct),
                _ => throw new VaultEntityNotFoundException("routine_job_not_found", $"Rotina '{jobId}' não encontrada.")
            };
        }
        finally
        {
            _executionLock.Release();
        }
    }

    private string ResolveRawInboxFolder(string? overrideInbox)
        => string.IsNullOrWhiteSpace(overrideInbox) ? _options.InboxFolder : overrideInbox.Trim();

    private void ValidateConfiguredFolders(string rawInbox)
    {
        _guard.ResolveSafePath(rawInbox);
        _guard.ResolveSafePath(_options.ArchiveFolder);
        _guard.ResolveSafePath(_options.ZettelkastenFolder);
        _guard.ResolveSafePath("daily");
        _guard.ResolveSafePath("templates");
        _guard.ResolveSafePath(Path.Combine("assets", "kanban"));
    }

    private static string NormalizeJobId(string? jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            throw new VaultEntityNotFoundException("routine_job_not_found", "Identificador de rotina não informado.");
        }

        var trimmed = jobId.Trim();
        if (string.Equals(trimmed, MorningPrepJobId, StringComparison.OrdinalIgnoreCase))
        {
            return MorningPrepJobId;
        }
        if (string.Equals(trimmed, InboxTriageJobId, StringComparison.OrdinalIgnoreCase))
        {
            return InboxTriageJobId;
        }
        if (string.Equals(trimmed, NightlyClosureJobId, StringComparison.OrdinalIgnoreCase))
        {
            return NightlyClosureJobId;
        }

        throw new VaultEntityNotFoundException(
            "routine_job_not_found",
            $"Rotina '{jobId}' não encontrada. Valores aceitos: MorningPrepJob, InboxTriageJob, NightlyClosureJob.");
    }

    private DateOnly ParseDateOrDefault(string? rawDate, TimeZoneInfo tz)
    {
        if (string.IsNullOrWhiteSpace(rawDate))
        {
            return DateOnly.FromDateTime(CalendarTimeZoneResolver.ConvertToLocal(_utcNowProvider(), tz).DateTime);
        }

        if (!DateOnly.TryParseExact(rawDate.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            throw new VaultValidationException(
                "invalid_routine_date",
                $"Data inválida '{rawDate}'. Use o formato yyyy-MM-dd.");
        }

        return parsed;
    }

    private async Task<RoutineRunResultDto> RunInboxTriageCoreAsync(
        DateOnly targetDate,
        string rawInbox,
        TimeZoneInfo tz,
        CancellationToken ct)
    {
        await _initializer.EnsureInitializedAsync(ct);

        var inboxDir = _guard.ResolveSafePath(rawInbox);
        var archiveDir = _guard.ResolveSafePath(_options.ArchiveFolder);
        var zettelDir = _guard.ResolveSafePath(_options.ZettelkastenFolder);
        var indicesDir = _guard.ResolveSafePath(Path.Combine(_options.ZettelkastenFolder, "Índices"));

        Directory.CreateDirectory(inboxDir);
        Directory.CreateDirectory(archiveDir);
        Directory.CreateDirectory(zettelDir);

        var candidateFiles = EnumerateInboxPendingFiles(inboxDir);
        var availableMocs = LoadAvailableMocs(indicesDir);
        var fileResults = new List<InboxTriageFileResultDto>(candidateFiles.Count);
        var touchedFiles = new List<string>();
        var processedCount = 0;
        var failedCount = 0;

        foreach (var sourceFullPath in candidateFiles)
        {
            var relSource = ToVaultRelativePath(sourceFullPath);
            try
            {
                var rawContent = await File.ReadAllTextAsync(sourceFullPath, Encoding.UTF8, ct);
                var heuristic = ClassifyHeuristically(sourceFullPath, rawContent, availableMocs);
                var classification = _options.UseCli && _cliRunner is not null
                    ? await TryClassifyWithCliAsync(sourceFullPath, heuristic, availableMocs, ct)
                    : heuristic;

                var noteBaseName = BuildTargetBaseFileName(classification.Category, classification.SafeTitle);
                var zettelTargetFullPath = ResolveUniquePath(zettelDir, noteBaseName, ".md");
                var relZettelPath = ToVaultRelativePath(zettelTargetFullPath);

                var generatedMarkdown = BuildTriagedNoteMarkdown(
                    classification,
                    relSource,
                    targetDate,
                    tz,
                    _utcNowProvider());
                await File.WriteAllTextAsync(zettelTargetFullPath, generatedMarkdown, Utf8NoBom, ct);

                var archiveNameNoExt = Path.GetFileNameWithoutExtension(sourceFullPath);
                var archiveExt = Path.GetExtension(sourceFullPath);
                var archiveTargetFullPath = ResolveUniquePath(archiveDir, archiveNameNoExt, archiveExt);
                var relArchivePath = ToVaultRelativePath(archiveTargetFullPath);

                File.Move(sourceFullPath, archiveTargetFullPath);

                processedCount++;
                touchedFiles.Add(relZettelPath);
                touchedFiles.Add(relArchivePath);
                fileResults.Add(new InboxTriageFileResultDto(
                    SourceFile: relSource,
                    DestinationNotePath: relZettelPath,
                    ArchivePath: relArchivePath,
                    Category: classification.Category,
                    Succeeded: true,
                    ErrorMessage: null));
            }
            catch (VaultPathViolationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failedCount++;
                fileResults.Add(new InboxTriageFileResultDto(
                    SourceFile: relSource,
                    DestinationNotePath: null,
                    ArchivePath: null,
                    Category: "error",
                    Succeeded: false,
                    ErrorMessage: ex.Message));
            }
        }

        var succeeded = failedCount == 0;
        var summary = candidateFiles.Count == 0
            ? "Inbox vazio — nenhum arquivo pendente."
            : failedCount == 0
                ? $"{processedCount} item(ns) triado(s) para zettelkasten/ e arquivado(s) em {_options.ArchiveFolder}."
                : $"{processedCount} item(ns) triado(s), {failedCount} falha(s) isolada(s).";

        var status = failedCount > 0 ? (processedCount > 0 ? "warn" : "error") : "ok";
        await AppendAuditEntryAsync(InboxTriageJobId, status, summary, processedCount, failedCount, touchedFiles, tz, ct);

        var overview = await BuildOverviewCoreAsync(rawInbox, ct);
        return new RoutineRunResultDto(
            JobId: InboxTriageJobId,
            Succeeded: succeeded,
            ProcessedCount: processedCount,
            FailedCount: failedCount,
            Summary: summary,
            TouchedFiles: touchedFiles,
            FileResults: fileResults,
            Overview: overview);
    }

    private async Task<RoutineRunResultDto> RunMorningPrepCoreAsync(
        DateOnly targetDate,
        string rawInbox,
        TimeZoneInfo tz,
        CancellationToken ct)
    {
        await _initializer.EnsureInitializedAsync(ct);

        var dailyDir = _guard.ResolveSafePath("daily");
        Directory.CreateDirectory(dailyDir);
        var dailyPath = _guard.ResolveSafePath(Path.Combine("daily", $"{targetDate:yyyy-MM-dd}.md"));
        var relDailyPath = ToVaultRelativePath(dailyPath);

        List<string> lines;
        if (!File.Exists(dailyPath))
        {
            var templatePath = _guard.ResolveSafePath(Path.Combine("templates", "Notas Diárias.md"));
            var rawTemplate = File.Exists(templatePath)
                ? await File.ReadAllTextAsync(templatePath, Encoding.UTF8, ct)
                : string.Empty;

            var sanitized = VaultInitializer.SanitizeTemplateFrontmatter("Notas Diárias.md", rawTemplate)
                .Replace("{{date:YYYY-MM-DD}}", targetDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Replace("{{date:YYYY-MM-dd}}", targetDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Replace("{{date:DD MMMM, YYYY (dddd)}}", targetDate.ToString("dd MMMM, yyyy (dddd)", CultureInfo.InvariantCulture))
                .Replace("{{date:YYYY/MM/DD}}", targetDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture));

            lines = NormalizeNewDailyCronogramaHabits(sanitized.Replace("\r\n", "\n").Split('\n').ToList());
        }
        else
        {
            lines = (await File.ReadAllLinesAsync(dailyPath, Encoding.UTF8, ct)).ToList();
        }

        var pendingTasks = await LoadPendingKanbanTasksAsync(ct);
        var updatedLines = UpsertMorningPrepTasks(lines, pendingTasks);
        await File.WriteAllLinesAsync(dailyPath, updatedLines, Utf8NoBom, ct);

        var touched = new[] { relDailyPath };
        var summary = $"Preparação matinal de {targetDate:yyyy-MM-dd}: {pendingTasks.Count} tarefa(s) pendente(s) sincronizada(s).";
        await AppendAuditEntryAsync(MorningPrepJobId, "ok", summary, pendingTasks.Count, 0, touched, tz, ct);

        var overview = await BuildOverviewCoreAsync(rawInbox, ct);
        return new RoutineRunResultDto(
            JobId: MorningPrepJobId,
            Succeeded: true,
            ProcessedCount: pendingTasks.Count,
            FailedCount: 0,
            Summary: summary,
            TouchedFiles: touched,
            FileResults: Array.Empty<InboxTriageFileResultDto>(),
            Overview: overview);
    }

    private async Task<RoutineRunResultDto> RunNightlyClosureCoreAsync(
        DateOnly targetDate,
        string rawInbox,
        TimeZoneInfo tz,
        CancellationToken ct)
    {
        await _initializer.EnsureInitializedAsync(ct);

        var dailyDir = _guard.ResolveSafePath("daily");
        Directory.CreateDirectory(dailyDir);
        var dailyPath = _guard.ResolveSafePath(Path.Combine("daily", $"{targetDate:yyyy-MM-dd}.md"));
        if (!File.Exists(dailyPath))
        {
            await _vaultService.GetDailyHabitsAsync(targetDate, ct);
        }

        var relDailyPath = ToVaultRelativePath(dailyPath);
        var lines = (await File.ReadAllLinesAsync(dailyPath, Encoding.UTF8, ct)).ToList();

        var (completedHabits, totalHabits) = CountCronogramaHabits(lines);
        var adherencePct = totalHabits == 0
            ? 0
            : (int)Math.Round(completedHabits * 100.0 / totalHabits, MidpointRounding.AwayFromZero);

        var (kanbanDone, kanbanPending) = await CountKanbanTotalsAsync(ct);
        var closureLine =
            $"- **Fechamento autônomo ({targetDate:yyyy-MM-dd}):** Hábitos: {completedHabits}/{totalHabits} ({adherencePct}%) · Kanban: {kanbanDone} concluída(s), {kanbanPending} em aberto {NightlyClosureMarker}";

        var updatedLines = UpsertNightlyClosureReflection(lines, closureLine);
        await File.WriteAllLinesAsync(dailyPath, updatedLines, Utf8NoBom, ct);

        var touched = new[] { relDailyPath };
        var summary = $"Fechamento {targetDate:yyyy-MM-dd}: {completedHabits}/{totalHabits} ({adherencePct}%) hábitos, {kanbanDone} concluída(s) / {kanbanPending} em aberto.";
        await AppendAuditEntryAsync(NightlyClosureJobId, "ok", summary, 1, 0, touched, tz, ct);

        var overview = await BuildOverviewCoreAsync(rawInbox, ct);
        return new RoutineRunResultDto(
            JobId: NightlyClosureJobId,
            Succeeded: true,
            ProcessedCount: 1,
            FailedCount: 0,
            Summary: summary,
            TouchedFiles: touched,
            FileResults: Array.Empty<InboxTriageFileResultDto>(),
            Overview: overview);
    }

    private async Task<RoutinesOverviewDto> BuildOverviewCoreAsync(string rawInbox, CancellationToken ct)
    {
        var inboxDir = _guard.ResolveSafePath(rawInbox);
        var zettelDir = _guard.ResolveSafePath(_options.ZettelkastenFolder);
        var indicesDir = _guard.ResolveSafePath(Path.Combine(_options.ZettelkastenFolder, "Índices"));
        var availableMocs = LoadAvailableMocs(indicesDir);

        var pendingFiles = EnumerateInboxPendingFiles(inboxDir);
        var inboxItems = new List<InboxQueueItemDto>(pendingFiles.Count);
        foreach (var file in pendingFiles)
        {
            var content = await SafeReadTextForPreviewAsync(file, ct);
            var preview = ClassifyHeuristically(file, content, availableMocs);
            var targetFileName = BuildTargetBaseFileName(preview.Category, preview.SafeTitle) + ".md";
            var suggestedDest = $"{_options.ZettelkastenFolder.TrimEnd('/', '\\')}/{targetFileName}";
            var primaryTag = preview.Tags.FirstOrDefault(t => !string.Equals(t, "inbox", StringComparison.OrdinalIgnoreCase))
                             ?? preview.Category;

            inboxItems.Add(new InboxQueueItemDto(
                FileName: Path.GetFileName(file),
                RelativePath: ToVaultRelativePath(file),
                SuggestedDestination: suggestedDest.Replace('\\', '/'),
                Tag: primaryTag,
                Extension: Path.GetExtension(file).TrimStart('.').ToLowerInvariant()));
        }

        var logs = await LoadAuditLogsAsync(ct);
        var cards = BuildStatusCards(inboxItems.Count, logs);

        return new RoutinesOverviewDto(
            InboxPendingCount: inboxItems.Count,
            Routines: cards,
            InboxItems: inboxItems,
            Logs: logs);
    }

    private IReadOnlyList<RoutineStatusCardDto> BuildStatusCards(int inboxPendingCount, IReadOnlyList<RoutineExecutionLogDto> logs)
    {
        var morningLast = logs.FirstOrDefault(l => string.Equals(l.JobId, MorningPrepJobId, StringComparison.OrdinalIgnoreCase));
        var triageLast = logs.FirstOrDefault(l => string.Equals(l.JobId, InboxTriageJobId, StringComparison.OrdinalIgnoreCase));
        var nightlyLast = logs.FirstOrDefault(l => string.Equals(l.JobId, NightlyClosureJobId, StringComparison.OrdinalIgnoreCase));

        var intervalLabel = _options.InboxIntervalMinutes >= 60 && _options.InboxIntervalMinutes % 60 == 0
            ? $"A cada {_options.InboxIntervalMinutes / 60} h e sob demanda"
            : $"A cada {Math.Max(1, _options.InboxIntervalMinutes)} min e sob demanda";

        return new List<RoutineStatusCardDto>
        {
            new(
                JobId: MorningPrepJobId,
                Title: "Preparação matinal",
                ScheduleLabel: $"Diária às {_options.MorningPrepTime}",
                Status: morningLast?.Status is "error" or "warn" ? "Atenção" : "Em dia",
                StatusTone: morningLast?.Status is "error" or "warn" ? "warn" : "ok",
                LastRunSummary: morningLast is null
                    ? "Última: ainda não executada"
                    : $"Última: {morningLast.DisplayTime} — {morningLast.Message}"),
            new(
                JobId: InboxTriageJobId,
                Title: "Triagem do inbox",
                ScheduleLabel: intervalLabel,
                Status: inboxPendingCount > 0 ? "Pendente" : "Em dia",
                StatusTone: inboxPendingCount > 0 ? "warn" : "ok",
                LastRunSummary: triageLast is null
                    ? "Última: ainda não executada"
                    : $"Última: {triageLast.DisplayTime} — {triageLast.Message}"),
            new(
                JobId: NightlyClosureJobId,
                Title: "Fechamento noturno",
                ScheduleLabel: $"Diária às {_options.NightlyClosureTime}",
                Status: nightlyLast?.Status is "error" or "warn" ? "Atenção" : "Em dia",
                StatusTone: nightlyLast?.Status is "error" or "warn" ? "warn" : "ok",
                LastRunSummary: nightlyLast is null
                    ? "Última: ainda não executada"
                    : $"Última: {nightlyLast.DisplayTime} — {nightlyLast.Message}")
        };
    }

    private static List<string> EnumerateInboxPendingFiles(string inboxDir)
    {
        if (!Directory.Exists(inboxDir))
        {
            return new List<string>();
        }

        return Directory.EnumerateFiles(inboxDir, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => f.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<string> SafeReadTextForPreviewAsync(string fullPath, CancellationToken ct)
    {
        try
        {
            return await File.ReadAllTextAsync(fullPath, Encoding.UTF8, ct);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static IReadOnlyList<string> LoadAvailableMocs(string indicesDir)
    {
        if (!Directory.Exists(indicesDir))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(indicesDir, "*.md", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Cast<string>()
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed record TriageClassification(
        string Category,
        string Title,
        string SafeTitle,
        string CleanBody,
        IReadOnlyList<string> Tags,
        IReadOnlyList<string> Mocs);

    private static TriageClassification ClassifyHeuristically(
        string sourceFullPath,
        string rawContent,
        IReadOnlyList<string> availableMocs)
    {
        var baseFileName = Path.GetFileNameWithoutExtension(sourceFullPath);
        var (yamlTags, cleanBody) = StripLeadingAndDuplicateFrontmatter(rawContent);

        var title = baseFileName;
        var safeTitle = SanitizeFileName(title);
        var combinedSearchText = $"{baseFileName}\n{rawContent}";
        var lower = combinedSearchText.ToLowerInvariant();

        var isMeeting = MeetingKeywords.Any(k => lower.Contains(k, StringComparison.Ordinal));
        var category = isMeeting ? "reuniao" : "nota";

        var tags = new List<string> { category };
        foreach (var yt in yamlTags)
        {
            AddUniqueTag(tags, yt);
        }

        foreach (Match m in InlineTagRegex.Matches(rawContent))
        {
            AddUniqueTag(tags, m.Groups[1].Value);
        }

        if (tags.Count == 1)
        {
            AddUniqueTag(tags, "fleeting");
        }

        var matchedMocs = MatchMocs(combinedSearchText, availableMocs);
        return new TriageClassification(category, title, safeTitle, cleanBody, tags, matchedMocs);
    }

    private async Task<TriageClassification> TryClassifyWithCliAsync(
        string sourceFullPath,
        TriageClassification fallback,
        IReadOnlyList<string> availableMocs,
        CancellationToken ct)
    {
        if (_cliRunner is null)
        {
            return fallback;
        }

        try
        {
            var mocsList = availableMocs.Count > 0 ? string.Join(", ", availableMocs) : "(nenhum)";
            var prompt =
                $"Classifique a captura do inbox '{Path.GetFileName(sourceFullPath)}' para o Zettelkasten. " +
                $"MOCs disponíveis em zettelkasten/Índices/: {mocsList}. " +
                "Responda APENAS com um JSON válido no formato: " +
                "{\"category\":\"reuniao|nota\",\"title\":\"...\",\"tags\":[\"...\"],\"mocs\":[\"...\"]}.\n\n" +
                $"Conteúdo:\n{fallback.CleanBody}";

            var (executable, args, timeout) = BuildCliInvocation(prompt);
            var cliResult = await _cliRunner.RunAsync(executable, args, _guard.VaultRoot, timeout, ct);
            if (!cliResult.Executed || cliResult.ExitCode != 0 || string.IsNullOrWhiteSpace(cliResult.StdOut))
            {
                return fallback;
            }

            if (!TryParseCliClassification(cliResult.StdOut, fallback, availableMocs, out var parsed))
            {
                return fallback;
            }

            return parsed;
        }
        catch
        {
            return fallback;
        }
    }

    private (string Executable, IReadOnlyList<string> Args, int TimeoutSeconds) BuildCliInvocation(string prompt)
    {
        var cliOpts = _granBrainOptions?.Cli ?? new CliOptions();
        var provider = (_options.CliProvider ?? "codex").Trim().ToLowerInvariant();
        var timeout = Math.Max(5, cliOpts.ExecutionTimeoutSeconds);

        return provider switch
        {
            "claude" => (cliOpts.ClaudePath, new[] { "-p", prompt, "--model", cliOpts.ClaudeModel }, timeout),
            "gemini" => (cliOpts.GeminiPath, new[] { "--model", cliOpts.GeminiModel, "--prompt", prompt }, timeout),
            _ => (cliOpts.CodexPath, new[] { "exec", "-m", cliOpts.CodexModel, "-C", _guard.VaultRoot, prompt }, timeout)
        };
    }

    private static bool TryParseCliClassification(
        string stdout,
        TriageClassification fallback,
        IReadOnlyList<string> availableMocs,
        out TriageClassification result)
    {
        result = fallback;
        var candidate = stdout.Trim();
        var fenceMatch = Regex.Match(candidate, @"```(?:json)?\s*([\s\S]*?)```", RegexOptions.IgnoreCase);
        if (fenceMatch.Success)
        {
            candidate = fenceMatch.Groups[1].Value.Trim();
        }

        try
        {
            using var doc = JsonDocument.Parse(candidate);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var category = fallback.Category;
            if (root.TryGetProperty("category", out var catProp) && catProp.ValueKind == JsonValueKind.String)
            {
                var rawCat = catProp.GetString()?.Trim().ToLowerInvariant();
                if (rawCat is "reuniao" or "reunião" or "meeting")
                {
                    category = "reuniao";
                }
                else if (rawCat is "nota" or "fleeting" or "conceito" or "estudo")
                {
                    category = "nota";
                }
            }

            var title = fallback.Title;
            if (root.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String)
            {
                var rawTitle = titleProp.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(rawTitle))
                {
                    title = rawTitle;
                }
            }

            var tags = new List<string> { category };
            foreach (var existing in fallback.Tags)
            {
                if (!string.Equals(existing, "nota", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(existing, "reuniao", StringComparison.OrdinalIgnoreCase))
                {
                    AddUniqueTag(tags, existing);
                }
            }

            if (root.TryGetProperty("tags", out var tagsProp) && tagsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in tagsProp.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                    {
                        AddUniqueTag(tags, item.GetString()!);
                    }
                }
            }

            var mocs = fallback.Mocs.ToList();
            if (root.TryGetProperty("mocs", out var mocsProp) && mocsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in mocsProp.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var rawMoc = item.GetString()?.Trim().Trim('[', ']');
                    if (string.IsNullOrWhiteSpace(rawMoc))
                    {
                        continue;
                    }

                    var canonicalMoc = availableMocs.FirstOrDefault(m => string.Equals(m, rawMoc, StringComparison.OrdinalIgnoreCase))
                                       ?? rawMoc;
                    if (!mocs.Contains(canonicalMoc, StringComparer.OrdinalIgnoreCase))
                    {
                        mocs.Add(canonicalMoc);
                    }
                }
            }

            result = new TriageClassification(
                Category: category,
                Title: title,
                SafeTitle: SanitizeFileName(title),
                CleanBody: fallback.CleanBody,
                Tags: tags,
                Mocs: mocs);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildTargetBaseFileName(string category, string safeTitle)
    {
        if (string.Equals(category, "reuniao", StringComparison.OrdinalIgnoreCase))
        {
            return safeTitle.StartsWith("Reunião - ", StringComparison.OrdinalIgnoreCase)
                ? safeTitle
                : $"Reunião - {safeTitle}";
        }

        return safeTitle;
    }

    private static string BuildTriagedNoteMarkdown(
        TriageClassification classification,
        string sourceInboxRelPath,
        DateOnly targetDate,
        TimeZoneInfo tz,
        DateTimeOffset utcNow)
    {
        var nowLocal = CalendarTimeZoneResolver.ConvertToLocal(utcNow, tz);
        var createdStamp = $"{targetDate:yyyy-MM-dd} {nowLocal:HH:mm}";

        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"type: {classification.Category}");
        sb.AppendLine($"created: \"{createdStamp}\"");
        sb.AppendLine($"source_inbox: \"{sourceInboxRelPath.Replace("\"", "'")}\"");
        sb.AppendLine("tags:");
        foreach (var tag in classification.Tags)
        {
            sb.AppendLine($"  - {tag}");
        }
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"# {classification.Title}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(classification.CleanBody))
        {
            sb.AppendLine(classification.CleanBody.Trim());
            sb.AppendLine();
        }

        sb.AppendLine("## Conexões");
        sb.AppendLine("- [[Fleeting]]");
        foreach (var moc in classification.Mocs)
        {
            sb.AppendLine($"- [[{moc}]]");
        }

        return sb.ToString().Replace("\r\n", "\n");
    }

    private static (IReadOnlyList<string> YamlTags, string CleanBody) StripLeadingAndDuplicateFrontmatter(string rawContent)
    {
        var normalized = rawContent.Replace("\r\n", "\n").TrimStart('\uFEFF');
        var tags = new List<string>();

        while (true)
        {
            var trimmedStart = normalized.TrimStart();
            if (!trimmedStart.StartsWith("---\n", StringComparison.Ordinal) &&
                !string.Equals(trimmedStart, "---", StringComparison.Ordinal))
            {
                break;
            }

            if (trimmedStart.StartsWith("---\n---\n", StringComparison.Ordinal) ||
                string.Equals(trimmedStart, "---\n---", StringComparison.Ordinal))
            {
                normalized = trimmedStart[7..].TrimStart('\n');
                continue;
            }

            var afterOpening = trimmedStart[4..];
            var closeMatch = Regex.Match(afterOpening, @"(?:^|\n)---(?:\n|$)");
            if (!closeMatch.Success)
            {
                // Partial/unclosed leading delimiter
                normalized = afterOpening.TrimStart('\n');
                continue;
            }

            var yamlBody = afterOpening[..closeMatch.Index];
            foreach (Match m in Regex.Matches(yamlBody, @"^\s*-\s*([A-Za-z0-9_À-ÿ/#-]+)\s*$", RegexOptions.Multiline))
            {
                AddUniqueTag(tags, m.Groups[1].Value);
            }

            normalized = afterOpening[(closeMatch.Index + closeMatch.Length)..].TrimStart('\n');
        }

        return (tags, normalized.Trim());
    }

    private static IReadOnlyList<string> MatchMocs(string text, IReadOnlyList<string> availableMocs)
    {
        var matched = new List<string>();
        foreach (var moc in availableMocs)
        {
            if (text.Contains(moc, StringComparison.OrdinalIgnoreCase))
            {
                matched.Add(moc);
                continue;
            }

            if (string.Equals(moc, "CSharp", StringComparison.OrdinalIgnoreCase) &&
                (text.Contains("C#", StringComparison.OrdinalIgnoreCase) ||
                 text.Contains("LINQ", StringComparison.OrdinalIgnoreCase) ||
                 text.Contains(".NET", StringComparison.OrdinalIgnoreCase) ||
                 text.Contains("dotnet", StringComparison.OrdinalIgnoreCase)))
            {
                matched.Add(moc);
                continue;
            }

            var tokens = Regex.Split(moc, @"[^A-Za-z0-9À-ÿ]+")
                .Where(t => t.Length >= 4 && !StopWords.Contains(t));
            if (tokens.Any(tok => text.Contains(tok, StringComparison.OrdinalIgnoreCase)))
            {
                matched.Add(moc);
            }
        }

        return matched;
    }

    private static void AddUniqueTag(List<string> tags, string rawTag)
    {
        var clean = rawTag.Trim().TrimStart('#');
        if (!string.IsNullOrWhiteSpace(clean) && !tags.Contains(clean, StringComparer.OrdinalIgnoreCase))
        {
            tags.Add(clean);
        }
    }

    private string ResolveUniquePath(string folderFullPath, string baseNameWithoutExt, string extension)
    {
        var candidate = _guard.ResolveSafePath(Path.Combine(folderFullPath, $"{baseNameWithoutExt}{extension}"));
        var counter = 2;
        while (File.Exists(candidate))
        {
            candidate = _guard.ResolveSafePath(Path.Combine(folderFullPath, $"{baseNameWithoutExt} - {counter}{extension}"));
            counter++;
        }

        return candidate;
    }

    private string ToVaultRelativePath(string fullPath)
        => Path.GetRelativePath(_guard.VaultRoot, fullPath).Replace('\\', '/');

    private static List<string> NormalizeNewDailyCronogramaHabits(List<string> lines)
    {
        var result = new List<string>(lines.Count);
        var inCronograma = false;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("#### Cronograma", StringComparison.OrdinalIgnoreCase))
            {
                inCronograma = true;
                result.Add(line);
                continue;
            }

            if (inCronograma && trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                inCronograma = false;
            }

            if (inCronograma && !line.Contains("<!-- cal-id:", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(Regex.Replace(line, @"^(\s*-\s*\[)[xX](\])", "$1 $2"));
            }
            else
            {
                result.Add(line);
            }
        }

        return result;
    }

    private async Task<IReadOnlyList<string>> LoadPendingKanbanTasksAsync(CancellationToken ct)
    {
        var lines = new List<string>();
        foreach (var boardName in new[] { "Profissional", "Pessoal" })
        {
            var boardPath = _guard.ResolveSafePath(Path.Combine("assets", "kanban", $"{boardName}.md"));
            if (!File.Exists(boardPath))
            {
                continue;
            }

            var board = await _vaultService.GetKanbanBoardAsync(boardName, ct);
            foreach (var column in board.Columns)
            {
                if (!string.Equals(column.Name, "To Do", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(column.Name, "Doing", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var task in column.Tasks)
                {
                    lines.Add($"- [ ] [{boardName}/{column.Name}] {task.Text} {MorningPrepMarker}");
                }
            }
        }

        return lines;
    }

    private static List<string> UpsertMorningPrepTasks(List<string> lines, IReadOnlyList<string> generatedTaskLines)
    {
        var updated = lines.ToList();
        var sectionIdx = updated.FindIndex(l => l.Trim().StartsWith("#### Tarefas", StringComparison.OrdinalIgnoreCase));
        if (sectionIdx < 0)
        {
            if (updated.Count > 0 && !string.IsNullOrWhiteSpace(updated[^1]))
            {
                updated.Add(string.Empty);
            }

            updated.Add("#### Tarefas");
            updated.Add(string.Empty);
            updated.Add("- [[Profissional]]");
            updated.Add("- [[Pessoal]]");
            updated.AddRange(generatedTaskLines);
            return updated;
        }

        var sectionEnd = FindSectionEnd(updated, sectionIdx);
        var keptLines = new List<string>();
        for (var i = sectionIdx + 1; i < sectionEnd; i++)
        {
            if (!updated[i].Contains(MorningPrepMarker, StringComparison.OrdinalIgnoreCase))
            {
                keptLines.Add(updated[i]);
            }
        }

        while (keptLines.Count > 0 && string.IsNullOrWhiteSpace(keptLines[^1]))
        {
            keptLines.RemoveAt(keptLines.Count - 1);
        }

        var insertAt = keptLines.FindLastIndex(l =>
            l.Contains("[[Profissional]]", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("[[Pessoal]]", StringComparison.OrdinalIgnoreCase));

        if (insertAt >= 0)
        {
            keptLines.InsertRange(insertAt + 1, generatedTaskLines);
        }
        else
        {
            keptLines.AddRange(generatedTaskLines);
        }

        if (sectionEnd < updated.Count)
        {
            keptLines.Add(string.Empty);
        }

        var result = new List<string>(updated.Count + generatedTaskLines.Count);
        result.AddRange(updated.Take(sectionIdx + 1));
        result.AddRange(keptLines);
        result.AddRange(updated.Skip(sectionEnd));
        return result;
    }

    private static (int Completed, int Total) CountCronogramaHabits(IReadOnlyList<string> lines)
    {
        var inCronograma = false;
        var completed = 0;
        var total = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("#### Cronograma", StringComparison.OrdinalIgnoreCase))
            {
                inCronograma = true;
                continue;
            }

            if (inCronograma && trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                break;
            }

            if (!inCronograma || line.Contains("<!-- cal-id:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var match = HabitLineRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            total++;
            if (string.Equals(match.Groups["state"].Value, "x", StringComparison.OrdinalIgnoreCase))
            {
                completed++;
            }
        }

        return (completed, total);
    }

    private async Task<(int Done, int Pending)> CountKanbanTotalsAsync(CancellationToken ct)
    {
        var done = 0;
        var pending = 0;

        foreach (var boardName in new[] { "Profissional", "Pessoal" })
        {
            var boardPath = _guard.ResolveSafePath(Path.Combine("assets", "kanban", $"{boardName}.md"));
            if (!File.Exists(boardPath))
            {
                continue;
            }

            var board = await _vaultService.GetKanbanBoardAsync(boardName, ct);
            foreach (var col in board.Columns)
            {
                if (string.Equals(col.Name, "Done", StringComparison.OrdinalIgnoreCase))
                {
                    done += col.Tasks.Count;
                }
                else if (string.Equals(col.Name, "To Do", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(col.Name, "Doing", StringComparison.OrdinalIgnoreCase))
                {
                    pending += col.Tasks.Count;
                }
            }
        }

        return (done, pending);
    }

    private static List<string> UpsertNightlyClosureReflection(List<string> lines, string closureLine)
    {
        var updated = lines.ToList();
        var sectionIdx = updated.FindIndex(l => l.Trim().StartsWith("#### Reflexões do Dia", StringComparison.OrdinalIgnoreCase));
        if (sectionIdx < 0)
        {
            if (updated.Count > 0 && !string.IsNullOrWhiteSpace(updated[^1]))
            {
                updated.Add(string.Empty);
            }

            updated.Add("#### Reflexões do Dia");
            updated.Add(string.Empty);
            updated.Add("> **Reflexões:**");
            updated.Add(closureLine);
            return updated;
        }

        var sectionEnd = FindSectionEnd(updated, sectionIdx);
        var keptLines = new List<string>();
        for (var i = sectionIdx + 1; i < sectionEnd; i++)
        {
            if (!updated[i].Contains(NightlyClosureMarker, StringComparison.OrdinalIgnoreCase))
            {
                keptLines.Add(updated[i]);
            }
        }

        var reflexoesIdx = keptLines.FindIndex(l => l.Trim().StartsWith("> **Reflexões:**", StringComparison.OrdinalIgnoreCase));
        if (reflexoesIdx >= 0)
        {
            keptLines.Insert(reflexoesIdx + 1, closureLine);
        }
        else
        {
            keptLines.Insert(0, "> **Reflexões:**");
            keptLines.Insert(1, closureLine);
        }

        var result = new List<string>(updated.Count + 2);
        result.AddRange(updated.Take(sectionIdx + 1));
        result.AddRange(keptLines);
        result.AddRange(updated.Skip(sectionEnd));
        return result;
    }

    private static int FindSectionEnd(IReadOnlyList<string> lines, int headingIdx)
    {
        for (var i = headingIdx + 1; i < lines.Count; i++)
        {
            if (lines[i].TrimStart().StartsWith("#", StringComparison.Ordinal))
            {
                return i;
            }
        }

        return lines.Count;
    }

    private string GetAuditLogFullPath()
        => _guard.ResolveSafePath(Path.Combine(_options.ArchiveFolder, "routines-audit-log.json"));

    private async Task<List<RoutineExecutionLogDto>> LoadAuditLogsAsync(CancellationToken ct)
    {
        var logPath = GetAuditLogFullPath();
        if (!File.Exists(logPath))
        {
            return new List<RoutineExecutionLogDto>();
        }

        try
        {
            var rawJson = await File.ReadAllTextAsync(logPath, Encoding.UTF8, ct);
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return new List<RoutineExecutionLogDto>();
            }

            var parsed = JsonSerializer.Deserialize<List<RoutineExecutionLogDto>>(rawJson, JsonOptions);
            return parsed ?? new List<RoutineExecutionLogDto>();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return new List<RoutineExecutionLogDto>();
        }
    }

    private async Task AppendAuditEntryAsync(
        string jobId,
        string status,
        string message,
        int itemsProcessed,
        int itemsFailed,
        IReadOnlyList<string> touchedFiles,
        TimeZoneInfo tz,
        CancellationToken ct)
    {
        var archiveDir = _guard.ResolveSafePath(_options.ArchiveFolder);
        Directory.CreateDirectory(archiveDir);

        var logs = await LoadAuditLogsAsync(ct);
        var nowUtc = _utcNowProvider();
        var nowLocal = CalendarTimeZoneResolver.ConvertToLocal(nowUtc, tz);

        var entry = new RoutineExecutionLogDto(
            Id: $"run-{nowUtc:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6]}",
            JobId: jobId,
            ExecutedAtIso: nowUtc.ToString("O", CultureInfo.InvariantCulture),
            DisplayTime: nowLocal.ToString("HH:mm · dd/MM", CultureInfo.InvariantCulture),
            Status: status,
            Message: message,
            ItemsProcessed: itemsProcessed,
            ItemsFailed: itemsFailed,
            TouchedFiles: touchedFiles);

        logs.Insert(0, entry);
        var maxEntries = Math.Max(1, _options.MaxAuditEntries);
        if (logs.Count > maxEntries)
        {
            logs = logs.Take(maxEntries).ToList();
        }

        var logPath = GetAuditLogFullPath();
        var tmpPath = _guard.ResolveSafePath(Path.Combine(_options.ArchiveFolder, $"routines-audit-log.{Guid.NewGuid():N}.tmp"));
        var serialized = JsonSerializer.Serialize(logs, JsonOptions);
        await File.WriteAllTextAsync(tmpPath, serialized, Utf8NoBom, ct);
        File.Move(tmpPath, logPath, overwrite: true);
    }

    private static string SanitizeFileName(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(title.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "Nota" : cleaned;
    }
}
