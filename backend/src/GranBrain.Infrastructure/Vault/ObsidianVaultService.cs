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
            ? Directory.EnumerateFiles(inboxDir, "*.md", SearchOption.TopDirectoryOnly).Count()
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

            if (!inCronograma && habits.Count > 0)
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
