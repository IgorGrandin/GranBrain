using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GranBrain.Core.Interfaces;
using GranBrain.Core.Models;
using GranBrain.Core.Security;

namespace GranBrain.Infrastructure.Vault;

public sealed class ObsidianVaultService : IVaultService
{
    private static readonly Regex HabitLineRegex = new(
        @"^(?<prefix>\s*-\s*\[(?<state>[ xX])\]\s*)(?:(?<time>\d{2}:\d{2})\s*-\s*)?(?<label>.+)$",
        RegexOptions.Compiled);

    private static readonly Regex KanbanTaskRegex = new(
        @"^\s*-\s*\[(?<state>[ xX])\]\s*(?<text>.+)$",
        RegexOptions.Compiled);

    private readonly VaultPathGuard _guard;
    private readonly VaultInitializer _initializer;

    public ObsidianVaultService(VaultPathGuard guard, VaultInitializer initializer)
    {
        _guard = guard;
        _initializer = initializer;
    }

    public async Task<VaultOverviewDto> GetOverviewAsync(DateOnly? date = null, CancellationToken ct = default)
    {
        await _initializer.EnsureInitializedAsync(ct);

        var targetDate = date ?? DateOnly.FromDateTime(DateTime.Now);
        var habits = await GetDailyHabitsAsync(targetDate, ct);

        var boards = new List<KanbanBoardDto>();
        foreach (var boardName in new[] { "Profissional", "Pessoal" })
        {
            var boardPath = _guard.ResolveSafePath(Path.Combine("assets", "kanban", $"{boardName}.md"));
            if (File.Exists(boardPath))
            {
                boards.Add(await GetKanbanBoardAsync(boardName, ct));
            }
        }

        var zettelDir = _guard.ResolveSafePath("zettelkasten");
        var indicesDir = _guard.ResolveSafePath(Path.Combine("zettelkasten", "Índices"));
        var dailyDir = _guard.ResolveSafePath("daily");
        var inboxDir = _guard.ResolveSafePath("inbox");

        var totalZettel = Directory.Exists(zettelDir)
            ? Directory.EnumerateFiles(zettelDir, "*.md", SearchOption.TopDirectoryOnly).Count()
            : 0;
        var totalIndices = Directory.Exists(indicesDir)
            ? Directory.EnumerateFiles(indicesDir, "*.md", SearchOption.TopDirectoryOnly).Count()
            : 0;
        var totalDaily = Directory.Exists(dailyDir)
            ? Directory.EnumerateFiles(dailyDir, "*.md", SearchOption.TopDirectoryOnly).Count()
            : 0;
        var inboxCount = Directory.Exists(inboxDir)
            ? Directory.EnumerateFiles(inboxDir, "*.*", SearchOption.TopDirectoryOnly)
                .Count(f => f.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            : 0;

        var recentNotes = await QueryNotesAsync(null, null, 8, ct);

        return new VaultOverviewDto(
            VaultPath: _guard.VaultRoot,
            TodayNotePath: $"daily/{targetDate:yyyy-MM-dd}.md",
            TotalZettelkastenNotes: totalZettel,
            TotalDailyNotes: totalDaily,
            TotalIndices: totalIndices,
            InboxPendingCount: inboxCount,
            HabitsCompletedToday: habits.Count(h => h.Completed),
            HabitsTotalToday: habits.Count,
            TodayHabits: habits,
            KanbanBoards: boards,
            RecentNotes: recentNotes);
    }

    public async Task<IReadOnlyList<HabitItemDto>> GetDailyHabitsAsync(DateOnly? date = null, CancellationToken ct = default)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.Now);
        var dailyPath = await EnsureDailyNoteExistsAsync(targetDate, ct);
        var lines = await File.ReadAllLinesAsync(dailyPath, Encoding.UTF8, ct);
        return ParseHabitsFromLines(lines);
    }

    public async Task<IReadOnlyList<HabitItemDto>> ToggleHabitAsync(string habitQuery, DateOnly? date = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(habitQuery))
        {
            throw new VaultEntityNotFoundException("habit_not_found", "Habit query cannot be empty.");
        }

        var targetDate = date ?? DateOnly.FromDateTime(DateTime.Now);
        var dailyPath = await EnsureDailyNoteExistsAsync(targetDate, ct);
        var lines = (await File.ReadAllLinesAsync(dailyPath, Encoding.UTF8, ct)).ToList();

        var inCronograma = false;
        var toggled = false;

        for (var i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith("#### Cronograma", StringComparison.OrdinalIgnoreCase))
            {
                inCronograma = true;
                continue;
            }

            if (inCronograma && trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                inCronograma = false;
            }

            if (!inCronograma || lines[i].Contains("<!-- cal-id:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var match = HabitLineRegex.Match(lines[i]);
            if (!match.Success)
            {
                continue;
            }

            var fullText = $"{(match.Groups["time"].Success ? match.Groups["time"].Value + " - " : "")}{match.Groups["label"].Value.Trim()}";
            if (fullText.Contains(habitQuery.Trim(), StringComparison.OrdinalIgnoreCase) ||
                match.Groups["label"].Value.Contains(habitQuery.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                var isChecked = string.Equals(match.Groups["state"].Value, "x", StringComparison.OrdinalIgnoreCase);
                var newState = isChecked ? " " : "x";
                lines[i] = Regex.Replace(lines[i], @"^(\s*-\s*\[)[ xX](\])", $"$1{newState}$2");
                toggled = true;
                break;
            }
        }

        if (!toggled)
        {
            throw new VaultEntityNotFoundException("habit_not_found", $"Habit matching '{habitQuery}' was not found in daily note {targetDate:yyyy-MM-dd}.");
        }

        await File.WriteAllLinesAsync(dailyPath, lines, Encoding.UTF8, ct);
        return ParseHabitsFromLines(lines);
    }

    public async Task<KanbanBoardDto> GetKanbanBoardAsync(string boardName, CancellationToken ct = default)
    {
        var boardPath = ResolveKanbanBoardPath(boardName);
        if (!File.Exists(boardPath))
        {
            throw new VaultEntityNotFoundException("kanban_board_not_found", $"Kanban board '{boardName}' does not exist.");
        }

        var lines = await File.ReadAllLinesAsync(boardPath, Encoding.UTF8, ct);
        return ParseKanbanBoard(boardName, $"assets/kanban/{boardName}.md", lines);
    }

    public async Task<KanbanBoardDto> AddKanbanTaskAsync(string boardName, string column, string taskText, CancellationToken ct = default)
    {
        var boardPath = ResolveKanbanBoardPath(boardName);
        if (!File.Exists(boardPath))
        {
            throw new VaultEntityNotFoundException("kanban_board_not_found", $"Kanban board '{boardName}' does not exist.");
        }

        var lines = (await File.ReadAllLinesAsync(boardPath, Encoding.UTF8, ct)).ToList();
        var targetHeader = $"## {column.Trim()}";
        var headerIndex = lines.FindIndex(l => string.Equals(l.Trim(), targetHeader, StringComparison.OrdinalIgnoreCase));

        if (headerIndex < 0)
        {
            var settingsIdx = lines.FindIndex(l => l.TrimStart().StartsWith("%% kanban:settings", StringComparison.OrdinalIgnoreCase));
            var insertPos = settingsIdx >= 0 ? settingsIdx : lines.Count;
            lines.Insert(insertPos, targetHeader);
            lines.Insert(insertPos + 1, $"- [ ] {taskText.Trim()}");
            lines.Insert(insertPos + 2, string.Empty);
        }
        else
        {
            var isDoneColumn = string.Equals(column.Trim(), "Done", StringComparison.OrdinalIgnoreCase);
            var checkbox = isDoneColumn ? "[x]" : "[ ]";
            lines.Insert(headerIndex + 1, $"- {checkbox} {taskText.Trim()}");
        }

        await File.WriteAllLinesAsync(boardPath, lines, Encoding.UTF8, ct);
        return ParseKanbanBoard(boardName, $"assets/kanban/{boardName}.md", lines);
    }

    public async Task<KanbanBoardDto> MoveKanbanTaskAsync(string boardName, string taskText, string targetColumn, CancellationToken ct = default)
    {
        var boardPath = ResolveKanbanBoardPath(boardName);
        if (!File.Exists(boardPath))
        {
            throw new VaultEntityNotFoundException("kanban_board_not_found", $"Kanban board '{boardName}' does not exist.");
        }

        var lines = (await File.ReadAllLinesAsync(boardPath, Encoding.UTF8, ct)).ToList();
        var removedIndex = -1;
        string? cleanTaskLabel = null;

        for (var i = 0; i < lines.Count; i++)
        {
            var match = KanbanTaskRegex.Match(lines[i]);
            if (match.Success && match.Groups["text"].Value.Contains(taskText.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                cleanTaskLabel = match.Groups["text"].Value.Trim();
                removedIndex = i;
                break;
            }
        }

        if (removedIndex >= 0 && cleanTaskLabel is not null)
        {
            lines.RemoveAt(removedIndex);
            await File.WriteAllLinesAsync(boardPath, lines, Encoding.UTF8, ct);
            return await AddKanbanTaskAsync(boardName, targetColumn, cleanTaskLabel, ct);
        }

        return await AddKanbanTaskAsync(boardName, targetColumn, taskText, ct);
    }

    public async Task<VaultNoteSummaryDto> CreateStructuredNoteAsync(DynamicNoteCreateRequest request, CancellationToken ct = default)
    {
        var safeTitle = SanitizeFileName(string.IsNullOrWhiteSpace(request.Title)
            ? $"Nota-{DateTime.Now:yyyyMMdd-HHmmss}"
            : request.Title.Trim());

        var normalizedType = (request.NoteType ?? "inbox").Trim().ToLowerInvariant();
        var subFolder = normalizedType switch
        {
            "nota" or "zettelkasten" or "permanente" or "estudo" => "zettelkasten",
            "indice" or "índice" => Path.Combine("zettelkasten", "Índices"),
            "daily" or "diaria" => "daily",
            _ => "inbox"
        };

        var relativePath = Path.Combine(subFolder, $"{safeTitle}.md");
        var fullPath = _guard.ResolveSafePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        var tags = new List<string> { normalizedType };
        if (request.Tags is not null)
        {
            foreach (var t in request.Tags.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                var cleanTag = t.Trim().TrimStart('#');
                if (!tags.Contains(cleanTag, StringComparer.OrdinalIgnoreCase))
                {
                    tags.Add(cleanTag);
                }
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"type: {normalizedType}");
        sb.AppendLine($"created: \"{DateTime.Now:yyyy-MM-dd HH:mm}\"");
        if (!string.IsNullOrWhiteSpace(request.Area))
        {
            sb.AppendLine($"area: \"{request.Area.Trim()}\"");
        }

        if (request.ExtraFields is not null)
        {
            foreach (var kv in request.ExtraFields.Where(k => !string.IsNullOrWhiteSpace(k.Key)))
            {
                sb.AppendLine($"{kv.Key.Trim()}: \"{kv.Value?.Replace("\"", "'")}\"");
            }
        }

        sb.AppendLine("tags:");
        foreach (var tag in tags)
        {
            sb.AppendLine($"  - {tag}");
        }
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"# {request.Title.Trim()}");
        sb.AppendLine();
        sb.AppendLine(request.Content?.Trim() ?? string.Empty);

        await File.WriteAllTextAsync(fullPath, sb.ToString(), Encoding.UTF8, ct);

        return new VaultNoteSummaryDto(
            Title: Path.GetFileNameWithoutExtension(fullPath),
            RelativePath: relativePath.Replace('\\', '/'),
            Category: subFolder.Replace('\\', '/'),
            LastModifiedUtc: DateTime.UtcNow,
            Tags: tags,
            Excerpt: BuildExcerpt(request.Content ?? string.Empty));
    }

    public async Task<IReadOnlyList<VaultNoteSummaryDto>> QueryNotesAsync(
        string? query = null,
        string? category = null,
        int limit = 20,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(category))
        {
            _guard.ResolveSafePath(category);
        }

        var searchDirs = new[] { "inbox", "zettelkasten", Path.Combine("zettelkasten", "Índices"), "daily" };
        var files = new List<(string FullPath, string Category, DateTime LastWriteUtc)>();

        foreach (var dir in searchDirs)
        {
            if (!string.IsNullOrWhiteSpace(category) &&
                !dir.Contains(category.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var resolvedDir = _guard.ResolveSafePath(dir);
            if (!Directory.Exists(resolvedDir))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(resolvedDir, "*.md", SearchOption.TopDirectoryOnly))
            {
                files.Add((file, dir.Replace('\\', '/'), File.GetLastWriteTimeUtc(file)));
            }
        }

        var results = new List<VaultNoteSummaryDto>();
        foreach (var item in files.OrderByDescending(f => f.LastWriteUtc))
        {
            var title = Path.GetFileNameWithoutExtension(item.FullPath);
            var content = await File.ReadAllTextAsync(item.FullPath, Encoding.UTF8, ct);

            if (!string.IsNullOrWhiteSpace(query))
            {
                var q = query.Trim();
                if (!title.Contains(q, StringComparison.OrdinalIgnoreCase) &&
                    !content.Contains(q, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            var relative = Path.GetRelativePath(_guard.VaultRoot, item.FullPath).Replace('\\', '/');
            results.Add(new VaultNoteSummaryDto(
                Title: title,
                RelativePath: relative,
                Category: item.Category,
                LastModifiedUtc: item.LastWriteUtc,
                Tags: ExtractTags(content),
                Excerpt: BuildExcerpt(content)));

            if (results.Count >= limit)
            {
                break;
            }
        }

        return results;
    }

    public async Task<IdeaItemDto> CaptureFleetingIdeaAsync(FleetingIdeaCaptureRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            throw new VaultValidationException("idea_text_required", "O texto da ideia é obrigatório.");
        }

        var rawTarget = request.Target?.Trim();
        if (!string.IsNullOrWhiteSpace(rawTarget) &&
            (rawTarget.Contains("..", StringComparison.Ordinal) ||
             rawTarget.Contains('/', StringComparison.Ordinal) ||
             rawTarget.Contains('\\', StringComparison.Ordinal) ||
             Path.IsPathRooted(rawTarget)))
        {
            _guard.ResolveSafePath(rawTarget);
        }

        await _initializer.EnsureInitializedAsync(ct);

        var text = request.Text.Trim();
        var id = $"idea-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6]}";
        var time = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
        var target = (rawTarget ?? "fleeting").ToLowerInvariant();
        var links = request.Links?.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()).ToList()
            ?? new List<string>();

        if (string.Equals(target, "inbox", StringComparison.OrdinalIgnoreCase))
        {
            var safeTitle = SanitizeFileName(text.Length > 60 ? text[..60] : text);
            var relativePath = $"inbox/{safeTitle}.md";
            var fullPath = _guard.ResolveSafePath(relativePath);
            if (File.Exists(fullPath))
            {
                relativePath = $"inbox/{safeTitle}-{id[^6..]}.md";
                fullPath = _guard.ResolveSafePath(relativePath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            var tags = new List<string> { "fleeting", "inbox" };
            if (request.Tags is not null)
            {
                foreach (var t in request.Tags.Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    var clean = t.Trim().TrimStart('#');
                    if (!tags.Contains(clean, StringComparer.OrdinalIgnoreCase))
                    {
                        tags.Add(clean);
                    }
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("---");
            sb.AppendLine($"id: \"{id}\"");
            sb.AppendLine("type: fleeting");
            sb.AppendLine("status: fleeting");
            sb.AppendLine($"created: \"{DateTime.Now:yyyy-MM-dd HH:mm}\"");
            sb.AppendLine("tags:");
            foreach (var tag in tags)
            {
                sb.AppendLine($"  - {tag}");
            }
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine($"# {text}");
            sb.AppendLine();
            sb.AppendLine(text);
            sb.AppendLine();
            sb.AppendLine("## Conexões");
            sb.AppendLine("- [[Fleeting]]");
            foreach (var link in links)
            {
                sb.AppendLine($"- [[{link}]]");
            }

            await File.WriteAllTextAsync(fullPath, sb.ToString(), Encoding.UTF8, ct);
            return new IdeaItemDto(id, text, time, "fleeting", relativePath, links);
        }

        var fleetingPath = _guard.ResolveSafePath(Path.Combine("zettelkasten", "Fleeting.md"));
        var existingRaw = File.Exists(fleetingPath)
            ? await File.ReadAllTextAsync(fleetingPath, Encoding.UTF8, ct)
            : string.Empty;
        var sanitized = VaultInitializer.SanitizeTemplateFrontmatter("Fleeting.md", existingRaw);
        var lines = sanitized.Replace("\r\n", "\n").Split('\n').ToList();

        var linksSuffix = links.Count > 0
            ? " " + string.Join(" ", links.Select(l => $"[[{l}]]"))
            : string.Empty;
        var entryLine = $"- <!-- id:{id} | status:fleeting --> **{time}** — {text}{linksSuffix}";

        var headerIndex = lines.FindIndex(l => l.Trim().StartsWith("#### Ideias Rápidas", StringComparison.OrdinalIgnoreCase));
        if (headerIndex < 0)
        {
            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
            {
                lines.Add(string.Empty);
            }
            lines.Add("#### Ideias Rápidas");
            lines.Add(entryLine);
        }
        else
        {
            var insertAt = headerIndex + 1;
            while (insertAt < lines.Count && !lines[insertAt].TrimStart().StartsWith("#", StringComparison.Ordinal))
            {
                insertAt++;
            }

            while (insertAt > headerIndex + 1 && string.IsNullOrWhiteSpace(lines[insertAt - 1]))
            {
                insertAt--;
            }

            lines.Insert(insertAt, entryLine);
        }

        await File.WriteAllTextAsync(fleetingPath, string.Join("\n", lines), Encoding.UTF8, ct);
        return new IdeaItemDto(id, text, time, "fleeting", "zettelkasten/Fleeting.md", links);
    }

    public async Task<BrainstormSessionDto> StartBrainstormSessionAsync(BrainstormStartRequest request, CancellationToken ct = default)
    {
        await _initializer.EnsureInitializedAsync(ct);

        IdeaItemDto? targetIdea = null;
        if (!string.IsNullOrWhiteSpace(request.IdeaId))
        {
            var allIdeas = await LoadAllIdeasAsync(ct);
            targetIdea = allIdeas.FirstOrDefault(i => string.Equals(i.Id, request.IdeaId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (targetIdea is null)
            {
                throw new VaultEntityNotFoundException("idea_not_found", $"Ideia '{request.IdeaId}' não encontrada.");
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.IdeaText))
            {
                throw new VaultValidationException("idea_text_required", "Informe ideaId ou ideaText para iniciar o brainstorming.");
            }

            targetIdea = await CaptureFleetingIdeaAsync(new FleetingIdeaCaptureRequest(request.IdeaText, "fleeting"), ct);
        }

        var safeTitle = SanitizeFileName(targetIdea.Text.Length > 60 ? targetIdea.Text[..60] : targetIdea.Text);
        var noteRelativePath = $"zettelkasten/Brainstorming - {safeTitle}.md";
        var noteFullPath = _guard.ResolveSafePath(noteRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(noteFullPath)!);

        var problem = string.IsNullOrWhiteSpace(request.Problem)
            ? $"Qual dor real e recorrente '{targetIdea.Text}' resolve e quem sofre com ela hoje?"
            : request.Problem.Trim();

        var hypotheses = request.Hypotheses is { Count: > 0 }
            ? request.Hypotheses.ToList()
            : new List<BrainstormHypothesisDto>
            {
                new($"A proposta '{targetIdea.Text}' reduz atrito imediato no fluxo diário.", "alta"),
                new("O valor percebido justifica uso recorrente semanal.", "média"),
                new("Expansões secundárias podem ser adiadas sem perda de valor.", "baixa")
            };

        var risks = request.Risks is { Count: > 0 }
            ? request.Risks.ToList()
            : new List<BrainstormRiskDto>
            {
                new("Complexidade operacional maior do que o benefício inicial.", "alto"),
                new("Falta de métrica objetiva para validar adoção.", "médio"),
                new("Dependência de ajustes manuais esporádicos.", "baixo")
            };

        var socraticQuestion = string.IsNullOrWhiteSpace(request.SocraticQuestion)
            ? "Qual evidência concreta faria você descartar esta ideia ainda hoje?"
            : request.SocraticQuestion.Trim();

        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"id: \"{targetIdea.Id}\"");
        sb.AppendLine("type: brainstorming");
        sb.AppendLine("status: em_analise");
        sb.AppendLine("origem: fleeting");
        sb.AppendLine($"source_path: \"{targetIdea.SourcePath}\"");
        sb.AppendLine($"projeto: \"{(request.Project ?? string.Empty).Replace("\"", "'")}\"");
        sb.AppendLine($"created: \"{DateTime.Now:yyyy-MM-dd HH:mm}\"");
        sb.AppendLine("tags:");
        sb.AppendLine("  - brainstorming");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"# Brainstorming — {targetIdea.Text}");
        sb.AppendLine();
        sb.AppendLine("## Origem");
        sb.AppendLine("- [[Fleeting]]");
        if (targetIdea.SourcePath.StartsWith("inbox/", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine($"- [[inbox]] / [[{Path.GetFileNameWithoutExtension(targetIdea.SourcePath)}]]");
        }
        sb.AppendLine();
        sb.AppendLine("## Problema");
        sb.AppendLine(problem);
        sb.AppendLine();
        sb.AppendLine("## Hipóteses");
        foreach (var hyp in hypotheses)
        {
            sb.AppendLine($"- [{hyp.Confidence}] {hyp.Text}");
        }
        sb.AppendLine();
        sb.AppendLine("## Riscos levantados");
        foreach (var risk in risks)
        {
            sb.AppendLine($"- [{risk.Severity}] {risk.Text}");
        }
        sb.AppendLine();
        sb.AppendLine("## Grilling Socrático");
        sb.AppendLine($"**Pergunta:** {socraticQuestion}");
        sb.AppendLine();
        sb.AppendLine("## Veredicto");
        sb.AppendLine("- [ ] **Promover a Conceito:** [[Permanente]]");
        sb.AppendLine("- [ ] **Promover a Projeto:** [[Projetos]]");
        sb.AppendLine("- [ ] **Descartar**");

        await File.WriteAllTextAsync(noteFullPath, sb.ToString(), Encoding.UTF8, ct);
        await UpdateIdeaStatusInSourceAsync(targetIdea, "brainstorm", ct);

        var updatedIdea = targetIdea with { Status = "brainstorm" };
        return new BrainstormSessionDto(
            updatedIdea,
            noteRelativePath,
            problem,
            hypotheses,
            risks,
            socraticQuestion,
            null);
    }

    public async Task<IdeaPromotionResultDto> PromoteIdeaAsync(IdeaPromoteRequest request, CancellationToken ct = default)
    {
        var normalizedTarget = (request.Target ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedTarget is not ("permanente" or "projeto" or "descartada"))
        {
            throw new VaultValidationException(
                "invalid_promotion_target",
                $"Destino de promoção inválido: '{request.Target}'. Use 'permanente', 'projeto' ou 'descartada'.");
        }

        if (string.IsNullOrWhiteSpace(request.IdeaId))
        {
            throw new VaultEntityNotFoundException("idea_not_found", "O identificador da ideia é obrigatório.");
        }

        var allIdeas = await LoadAllIdeasAsync(ct);
        var idea = allIdeas.FirstOrDefault(i => string.Equals(i.Id, request.IdeaId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (idea is null)
        {
            throw new VaultEntityNotFoundException("idea_not_found", $"Ideia '{request.IdeaId}' não encontrada.");
        }

        await _initializer.EnsureInitializedAsync(ct);

        var safeTitle = SanitizeFileName(idea.Text.Length > 60 ? idea.Text[..60] : idea.Text);
        var newStatus = normalizedTarget == "descartada" ? "descartada" : "promovida";
        var brainstormPath = _guard.ResolveSafePath(Path.Combine("zettelkasten", $"Brainstorming - {safeTitle}.md"));

        await UpdateIdeaStatusInSourceAsync(idea, newStatus, ct);
        if (File.Exists(brainstormPath))
        {
            await UpdateBrainstormVerdictAsync(brainstormPath, normalizedTarget, request.SocraticAnswer, ct);
        }

        string? targetPath = null;
        if (normalizedTarget == "permanente")
        {
            var permanentePath = _guard.ResolveSafePath(Path.Combine("zettelkasten", "Permanente.md"));
            var permRaw = File.Exists(permanentePath)
                ? await File.ReadAllTextAsync(permanentePath, Encoding.UTF8, ct)
                : string.Empty;
            var permSanitized = VaultInitializer.SanitizeTemplateFrontmatter("Permanente.md", permRaw);
            var wikiLink = $"[[{safeTitle}]]";

            if (!permSanitized.Contains(wikiLink, StringComparison.OrdinalIgnoreCase))
            {
                var permLines = permSanitized.Replace("\r\n", "\n").Split('\n').ToList();
                var headerIdx = permLines.FindIndex(l => l.Trim().StartsWith("#### Ideias Maduras", StringComparison.OrdinalIgnoreCase));
                if (headerIdx < 0)
                {
                    if (permLines.Count > 0 && !string.IsNullOrWhiteSpace(permLines[^1]))
                    {
                        permLines.Add(string.Empty);
                    }
                    permLines.Add("#### Ideias Maduras");
                    permLines.Add($"- {wikiLink}");
                }
                else
                {
                    var insertAt = headerIdx + 1;
                    while (insertAt < permLines.Count && !permLines[insertAt].TrimStart().StartsWith("#", StringComparison.Ordinal))
                    {
                        insertAt++;
                    }
                    while (insertAt > headerIdx + 1 && string.IsNullOrWhiteSpace(permLines[insertAt - 1]))
                    {
                        insertAt--;
                    }
                    permLines.Insert(insertAt, $"- {wikiLink}");
                }

                await File.WriteAllTextAsync(permanentePath, string.Join("\n", permLines), Encoding.UTF8, ct);
            }
            else if (!string.Equals(permRaw, permSanitized, StringComparison.Ordinal))
            {
                await File.WriteAllTextAsync(permanentePath, permSanitized, Encoding.UTF8, ct);
            }

            targetPath = $"zettelkasten/{safeTitle}.md";
            var atomicFullPath = _guard.ResolveSafePath(targetPath);
            var summary = string.IsNullOrWhiteSpace(request.Summary) ? idea.Text : request.Summary.Trim();
            var atomicSb = new StringBuilder();
            atomicSb.AppendLine("---");
            atomicSb.AppendLine("type: permanente");
            atomicSb.AppendLine("status: promovida");
            atomicSb.AppendLine($"created: \"{DateTime.Now:yyyy-MM-dd HH:mm}\"");
            atomicSb.AppendLine("tags:");
            atomicSb.AppendLine("  - permanente");
            atomicSb.AppendLine("  - zettelkasten");
            atomicSb.AppendLine("---");
            atomicSb.AppendLine();
            atomicSb.AppendLine($"# {idea.Text}");
            atomicSb.AppendLine();
            atomicSb.AppendLine(summary);
            if (!string.IsNullOrWhiteSpace(request.SocraticAnswer))
            {
                atomicSb.AppendLine();
                atomicSb.AppendLine("## Síntese Socrática");
                atomicSb.AppendLine(request.SocraticAnswer.Trim());
            }
            atomicSb.AppendLine();
            atomicSb.AppendLine("## Conexões");
            atomicSb.AppendLine("- [[Fleeting]]");
            atomicSb.AppendLine("- [[Permanente]]");
            if (File.Exists(brainstormPath))
            {
                atomicSb.AppendLine($"- [[Brainstorming - {safeTitle}]]");
            }

            await File.WriteAllTextAsync(atomicFullPath, atomicSb.ToString(), Encoding.UTF8, ct);
        }
        else if (normalizedTarget == "projeto")
        {
            targetPath = $"zettelkasten/Projeto - {safeTitle}.md";
            var projectFullPath = _guard.ResolveSafePath(targetPath);
            var area = string.IsNullOrWhiteSpace(request.Area) ? "profissional" : request.Area.Trim();
            var summary = string.IsNullOrWhiteSpace(request.Summary) ? idea.Text : request.Summary.Trim();

            var projSb = new StringBuilder();
            projSb.AppendLine("---");
            projSb.AppendLine("type: projeto");
            projSb.AppendLine("status: ativo");
            projSb.AppendLine($"area: \"{area.Replace("\"", "'")}\"");
            projSb.AppendLine($"created: \"{DateTime.Now:yyyy-MM-dd HH:mm}\"");
            projSb.AppendLine("tags:");
            projSb.AppendLine("  - projeto");
            projSb.AppendLine("---");
            projSb.AppendLine();
            projSb.AppendLine($"# Projeto — {idea.Text}");
            projSb.AppendLine();
            projSb.AppendLine("## Objetivo");
            projSb.AppendLine(summary);
            if (!string.IsNullOrWhiteSpace(request.SocraticAnswer))
            {
                projSb.AppendLine();
                projSb.AppendLine("## Síntese Socrática");
                projSb.AppendLine(request.SocraticAnswer.Trim());
            }
            projSb.AppendLine();
            projSb.AppendLine("## Conexões");
            projSb.AppendLine("- [[Fleeting]]");
            projSb.AppendLine("- [[Brainstorming]]");
            if (File.Exists(brainstormPath))
            {
                projSb.AppendLine($"- [[Brainstorming - {safeTitle}]]");
            }

            await File.WriteAllTextAsync(projectFullPath, projSb.ToString(), Encoding.UTF8, ct);
        }

        var updatedIdea = idea with { Status = newStatus };
        return new IdeaPromotionResultDto(updatedIdea, idea.SourcePath, targetPath, newStatus);
    }

    public async Task<IdeasOverviewDto> GetIdeasOverviewAsync(CancellationToken ct = default)
    {
        await _initializer.EnsureInitializedAsync(ct);

        var ideas = await LoadAllIdeasAsync(ct);
        var activeSession = await LoadLatestBrainstormSessionAsync(ideas, ct);

        return new IdeasOverviewDto(
            Ideas: ideas,
            FleetingCount: ideas.Count(i => string.Equals(i.Status, "fleeting", StringComparison.OrdinalIgnoreCase)),
            BrainstormCount: ideas.Count(i => string.Equals(i.Status, "brainstorm", StringComparison.OrdinalIgnoreCase)),
            PromotedCount: ideas.Count(i => string.Equals(i.Status, "promovida", StringComparison.OrdinalIgnoreCase)),
            DiscardedCount: ideas.Count(i => string.Equals(i.Status, "descartada", StringComparison.OrdinalIgnoreCase)),
            ActiveSession: activeSession);
    }

    private async Task<List<IdeaItemDto>> LoadAllIdeasAsync(CancellationToken ct)
    {
        var ideas = new List<IdeaItemDto>();
        var fleetingPath = _guard.ResolveSafePath(Path.Combine("zettelkasten", "Fleeting.md"));
        if (File.Exists(fleetingPath))
        {
            var lines = await File.ReadAllLinesAsync(fleetingPath, Encoding.UTF8, ct);
            var inSection = false;
            var lineIdx = 0;
            foreach (var rawLine in lines)
            {
                lineIdx++;
                var trimmed = rawLine.Trim();
                if (trimmed.StartsWith("#### Ideias Rápidas", StringComparison.OrdinalIgnoreCase))
                {
                    inSection = true;
                    continue;
                }
                if (inSection && trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    inSection = false;
                }

                if (!trimmed.StartsWith("-", StringComparison.Ordinal))
                {
                    continue;
                }

                var metaMatch = Regex.Match(
                    trimmed,
                    @"^-\s*<!--\s*id:(?<id>[^\s|]+)\s*\|\s*status:(?<status>[a-z_]+)\s*-->\s*\*\*(?<time>\d{2}:\d{2})\*\*\s*[—-]\s*(?<rest>.+)$");
                if (metaMatch.Success)
                {
                    var rest = metaMatch.Groups["rest"].Value.Trim();
                    var links = Regex.Matches(rest, @"\[\[([^\]]+)\]\]").Select(m => m.Groups[1].Value).ToList();
                    var cleanText = Regex.Replace(rest, @"\s*\[\[[^\]]+\]\]", string.Empty).Trim();
                    ideas.Add(new IdeaItemDto(
                        metaMatch.Groups["id"].Value.Trim(),
                        cleanText,
                        metaMatch.Groups["time"].Value.Trim(),
                        metaMatch.Groups["status"].Value.Trim(),
                        "zettelkasten/Fleeting.md",
                        links));
                }
                else if (inSection)
                {
                    var plain = trimmed[1..].Trim();
                    if (!string.IsNullOrWhiteSpace(plain))
                    {
                        ideas.Add(new IdeaItemDto(
                            $"legacy-fleeting-{lineIdx}",
                            plain,
                            "--:--",
                            "fleeting",
                            "zettelkasten/Fleeting.md"));
                    }
                }
            }
        }

        var inboxDir = _guard.ResolveSafePath("inbox");
        if (Directory.Exists(inboxDir))
        {
            foreach (var file in Directory.EnumerateFiles(inboxDir, "*.md", SearchOption.TopDirectoryOnly)
                         .OrderByDescending(File.GetLastWriteTimeUtc))
            {
                var content = await File.ReadAllTextAsync(file, Encoding.UTF8, ct);
                var normalized = content.Replace("\r\n", "\n");
                var idMatch = Regex.Match(normalized, @"^id:\s*""?(?<id>[^""\n]+)""?", RegexOptions.Multiline);
                var typeMatch = Regex.Match(normalized, @"^type:\s*(?<type>[a-z_]+)", RegexOptions.Multiline);
                if (!idMatch.Success && !string.Equals(typeMatch.Groups["type"].Value, "fleeting", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var statusMatch = Regex.Match(normalized, @"^status:\s*(?<status>[a-z_]+)", RegexOptions.Multiline);
                var createdMatch = Regex.Match(normalized, @"^created:\s*""?(?<created>[^""\n]+)""?", RegexOptions.Multiline);
                var headingMatch = Regex.Match(normalized, @"^#\s+(?<title>.+)$", RegexOptions.Multiline);

                var id = idMatch.Success ? idMatch.Groups["id"].Value.Trim() : $"inbox-{Path.GetFileNameWithoutExtension(file)}";
                var status = statusMatch.Success ? statusMatch.Groups["status"].Value.Trim() : "fleeting";
                var text = headingMatch.Success ? headingMatch.Groups["title"].Value.Trim() : Path.GetFileNameWithoutExtension(file);
                var time = createdMatch.Success && createdMatch.Groups["created"].Value.Length >= 5
                    ? createdMatch.Groups["created"].Value.Trim()[^5..]
                    : "--:--";
                var relPath = Path.GetRelativePath(_guard.VaultRoot, file).Replace('\\', '/');

                ideas.Add(new IdeaItemDto(id, text, time, status, relPath));
            }
        }

        return ideas;
    }

    private async Task UpdateIdeaStatusInSourceAsync(IdeaItemDto idea, string newStatus, CancellationToken ct)
    {
        var fullPath = _guard.ResolveSafePath(idea.SourcePath);
        if (!File.Exists(fullPath))
        {
            return;
        }

        var content = await File.ReadAllTextAsync(fullPath, Encoding.UTF8, ct);
        if (idea.SourcePath.Equals("zettelkasten/Fleeting.md", StringComparison.OrdinalIgnoreCase))
        {
            var pattern = $@"(<!--\s*id:{Regex.Escape(idea.Id)}\s*\|\s*status:)[a-z_]+(\s*-->)";
            var updated = Regex.Replace(content, pattern, $"${{1}}{newStatus}${{2}}");
            if (!string.Equals(content, updated, StringComparison.Ordinal))
            {
                await File.WriteAllTextAsync(fullPath, updated, Encoding.UTF8, ct);
            }
        }
        else
        {
            var updated = Regex.IsMatch(content, @"^status:\s*[a-z_]+", RegexOptions.Multiline)
                ? Regex.Replace(content, @"^(status:\s*)[a-z_]+", $"$1{newStatus}", RegexOptions.Multiline)
                : content;
            if (!string.Equals(content, updated, StringComparison.Ordinal))
            {
                await File.WriteAllTextAsync(fullPath, updated, Encoding.UTF8, ct);
            }
        }
    }

    private async Task UpdateBrainstormVerdictAsync(string brainstormFullPath, string target, string? socraticAnswer, CancellationToken ct)
    {
        var content = (await File.ReadAllTextAsync(brainstormFullPath, Encoding.UTF8, ct)).Replace("\r\n", "\n");
        var statusValue = target == "descartada" ? "descartada" : "promovida";
        content = Regex.Replace(content, @"^(status:\s*)[a-z_]+", $"$1{statusValue}", RegexOptions.Multiline);

        if (target == "permanente")
        {
            content = content.Replace("- [ ] **Promover a Conceito:**", "- [x] **Promover a Conceito:**");
        }
        else if (target == "projeto")
        {
            content = content.Replace("- [ ] **Promover a Projeto:**", "- [x] **Promover a Projeto:**");
        }
        else if (target == "descartada")
        {
            content = content.Replace("- [ ] **Descartar**", "- [x] **Descartar**");
        }

        if (!string.IsNullOrWhiteSpace(socraticAnswer) && !content.Contains("**Resposta:**", StringComparison.Ordinal))
        {
            content = Regex.Replace(
                content,
                @"(\*\*Pergunta:\*\*[^\n]*)",
                $"$1\n**Resposta:** {socraticAnswer.Trim()}");
        }

        await File.WriteAllTextAsync(brainstormFullPath, content, Encoding.UTF8, ct);
    }

    private async Task<BrainstormSessionDto?> LoadLatestBrainstormSessionAsync(IReadOnlyList<IdeaItemDto> ideas, CancellationToken ct)
    {
        var zettelDir = _guard.ResolveSafePath("zettelkasten");
        if (!Directory.Exists(zettelDir))
        {
            return null;
        }

        var files = Directory.EnumerateFiles(zettelDir, "Brainstorming - *.md", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();

        if (files.Count == 0)
        {
            return null;
        }

        string? selectedFile = null;
        string? selectedContent = null;
        foreach (var file in files)
        {
            var raw = (await File.ReadAllTextAsync(file, Encoding.UTF8, ct)).Replace("\r\n", "\n");
            if (Regex.IsMatch(raw, @"^status:\s*em_analise", RegexOptions.Multiline))
            {
                selectedFile = file;
                selectedContent = raw;
                break;
            }
        }

        selectedFile ??= files[0];
        selectedContent ??= (await File.ReadAllTextAsync(selectedFile, Encoding.UTF8, ct)).Replace("\r\n", "\n");

        var idMatch = Regex.Match(selectedContent, @"^id:\s*""?(?<id>[^""\n]+)""?", RegexOptions.Multiline);
        var ideaId = idMatch.Success ? idMatch.Groups["id"].Value.Trim() : Path.GetFileNameWithoutExtension(selectedFile);
        var matchedIdea = ideas.FirstOrDefault(i => string.Equals(i.Id, ideaId, StringComparison.OrdinalIgnoreCase))
            ?? new IdeaItemDto(ideaId, Path.GetFileNameWithoutExtension(selectedFile).Replace("Brainstorming - ", string.Empty), "--:--", "brainstorm", Path.GetRelativePath(_guard.VaultRoot, selectedFile).Replace('\\', '/'));

        var problem = ExtractSectionBody(selectedContent, "## Problema");
        var hypotheses = Regex.Matches(ExtractSectionBody(selectedContent, "## Hipóteses"), @"^-\s*\[(?<conf>[^\]]+)\]\s*(?<text>.+)$", RegexOptions.Multiline)
            .Select(m => new BrainstormHypothesisDto(m.Groups["text"].Value.Trim(), m.Groups["conf"].Value.Trim()))
            .ToList();
        var risks = Regex.Matches(ExtractSectionBody(selectedContent, "## Riscos levantados"), @"^-\s*\[(?<sev>[^\]]+)\]\s*(?<text>.+)$", RegexOptions.Multiline)
            .Select(m => new BrainstormRiskDto(m.Groups["text"].Value.Trim(), m.Groups["sev"].Value.Trim()))
            .ToList();

        var qMatch = Regex.Match(selectedContent, @"\*\*Pergunta:\*\*\s*(?<q>.+)$", RegexOptions.Multiline);
        var aMatch = Regex.Match(selectedContent, @"\*\*Resposta:\*\*\s*(?<a>.+)$", RegexOptions.Multiline);

        return new BrainstormSessionDto(
            matchedIdea,
            Path.GetRelativePath(_guard.VaultRoot, selectedFile).Replace('\\', '/'),
            problem,
            hypotheses,
            risks,
            qMatch.Success ? qMatch.Groups["q"].Value.Trim() : string.Empty,
            aMatch.Success ? aMatch.Groups["a"].Value.Trim() : null);
    }

    private static string ExtractSectionBody(string markdown, string heading)
    {
        var pattern = $@"{Regex.Escape(heading)}\n(?<body>[\s\S]*?)(?=\n##\s|$)";
        var match = Regex.Match(markdown, pattern);
        return match.Success ? match.Groups["body"].Value.Trim() : string.Empty;
    }

    private string ResolveKanbanBoardPath(string boardName)
    {
        if (boardName.Contains("..", StringComparison.Ordinal) ||
            boardName.Contains('/') ||
            boardName.Contains('\\'))
        {
            _guard.ResolveSafePath(boardName);
        }

        var cleanName = boardName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? boardName
            : $"{boardName}.md";

        return _guard.ResolveSafePath(Path.Combine("assets", "kanban", cleanName));
    }

    private async Task<string> EnsureDailyNoteExistsAsync(DateOnly date, CancellationToken ct)
    {
        var dailyDir = _guard.ResolveSafePath("daily");
        Directory.CreateDirectory(dailyDir);

        var filePath = _guard.ResolveSafePath(Path.Combine("daily", $"{date:yyyy-MM-dd}.md"));
        if (File.Exists(filePath))
        {
            return filePath;
        }

        var templatePath = _guard.ResolveSafePath(Path.Combine("templates", "Notas Diárias.md"));
        string content;
        if (File.Exists(templatePath))
        {
            var rawTemplate = await File.ReadAllTextAsync(templatePath, Encoding.UTF8, ct);
            content = VaultInitializer.SanitizeTemplateFrontmatter("Notas Diárias.md", rawTemplate)
                .Replace("{{date:YYYY-MM-DD}}", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Replace("{{date:DD MMMM, YYYY (dddd)}}", date.ToString("dd MMMM, yyyy (dddd)", CultureInfo.InvariantCulture))
                .Replace("{{date:YYYY/MM/DD}}", date.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture));
        }
        else
        {
            content = $"""
                ---
                type: daily
                created: "{date:yyyy-MM-dd}"
                tags:
                  - daily
                ---

                # {date:yyyy-MM-dd}

                ## 📅 Cronograma, Tarefas e Anotações

                #### Cronograma

                - [ ] 07:00 - Colocar Alanna na perua
                - [ ] 07:30 - Orar/Ler Bíblia
                - [ ] 08:15 - Exercícios Matinais
                - [ ] 09:00 - Iniciar Trabalho
                - [ ] 09:30 - Café da manhã
                - [ ] 12:30 - Almoço
                - [ ] 18:00 - Finalizar Trabalho
                """;
        }

        await File.WriteAllTextAsync(filePath, content, Encoding.UTF8, ct);
        return filePath;
    }

    private static IReadOnlyList<HabitItemDto> ParseHabitsFromLines(IEnumerable<string> lines)
    {
        var habits = new List<HabitItemDto>();
        var inCronograma = false;

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

            var time = match.Groups["time"].Success ? match.Groups["time"].Value : "";
            var label = match.Groups["label"].Value.Trim();
            var completed = string.Equals(match.Groups["state"].Value, "x", StringComparison.OrdinalIgnoreCase);
            var fullText = string.IsNullOrEmpty(time) ? label : $"{time} - {label}";

            habits.Add(new HabitItemDto(time, label, fullText, completed));
        }

        return habits;
    }

    private static KanbanBoardDto ParseKanbanBoard(string boardName, string relativePath, IReadOnlyList<string> lines)
    {
        var columns = new List<KanbanColumnDto>();
        string? currentColumn = null;
        var currentTasks = new List<KanbanTaskDto>();

        void FlushColumn()
        {
            if (currentColumn is not null)
            {
                columns.Add(new KanbanColumnDto(currentColumn, currentTasks.ToList()));
                currentTasks.Clear();
            }
        }

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("%% kanban:settings", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (trimmed.StartsWith("## ", StringComparison.Ordinal))
            {
                FlushColumn();
                currentColumn = trimmed[3..].Trim();
                continue;
            }

            if (currentColumn is null)
            {
                continue;
            }

            var match = KanbanTaskRegex.Match(line);
            if (match.Success)
            {
                var text = match.Groups["text"].Value.Trim();
                var completed = string.Equals(match.Groups["state"].Value, "x", StringComparison.OrdinalIgnoreCase);
                var id = $"{currentColumn}:{text}".ToLowerInvariant();
                currentTasks.Add(new KanbanTaskDto(id, text, completed, currentColumn));
            }
        }

        FlushColumn();
        return new KanbanBoardDto(boardName, relativePath, columns);
    }

    private static IReadOnlyList<string> ExtractTags(string markdown)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in Regex.Matches(markdown, @"(?<!\w)#([A-Za-z0-9_/-]+)"))
        {
            tags.Add(m.Groups[1].Value);
        }

        var yamlMatch = Regex.Match(markdown.Replace("\r\n", "\n"), @"^---\n([\s\S]*?)\n---");
        if (yamlMatch.Success)
        {
            foreach (Match m in Regex.Matches(yamlMatch.Groups[1].Value, @"^\s*-\s*([A-Za-z0-9_À-ÿ/-]+)\s*$", RegexOptions.Multiline))
            {
                tags.Add(m.Groups[1].Value);
            }
        }

        return tags.Take(6).ToList();
    }

    private static string BuildExcerpt(string markdown)
    {
        var withoutYaml = Regex.Replace(markdown.Replace("\r\n", "\n"), @"^---\n[\s\S]*?\n---\n*", string.Empty);
        var clean = Regex.Replace(withoutYaml, @"[#>*`_\-\[\]]+", " ");
        clean = Regex.Replace(clean, @"\s+", " ").Trim();
        return clean.Length <= 140 ? clean : clean[..140] + "...";
    }

    private static string SanitizeFileName(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(title.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "Nota" : cleaned;
    }
}
