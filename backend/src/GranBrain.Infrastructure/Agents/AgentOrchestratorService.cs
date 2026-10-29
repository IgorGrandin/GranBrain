using System.Text.Json;
using System.Text.RegularExpressions;
using GranBrain.Core.Configuration;
using GranBrain.Core.Interfaces;
using GranBrain.Core.Models;
using GranBrain.Core.Security;

namespace GranBrain.Infrastructure.Agents;

public sealed class AgentOrchestratorService : IAgentOrchestrator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IVaultService _vaultService;
    private readonly ICliProcessRunner _cliRunner;
    private readonly VaultPathGuard _guard;
    private readonly GranBrainOptions _options;

    public AgentOrchestratorService(
        IVaultService vaultService,
        ICliProcessRunner cliRunner,
        VaultPathGuard guard,
        GranBrainOptions options)
    {
        _vaultService = vaultService;
        _cliRunner = cliRunner;
        _guard = guard;
        _options = options;
    }

    public IReadOnlyList<AiProviderStatusDto> GetProvidersStatus()
    {
        return new List<AiProviderStatusDto>
        {
            new(
                Id: "codex",
                DisplayName: "OpenAI Codex CLI",
                PlanName: "ChatGPT Plus",
                ExecutablePath: _options.Cli.CodexPath,
                Installed: _cliRunner.IsExecutableAvailable(_options.Cli.CodexPath),
                Description: "Agente CLI da OpenAI autenticado via ChatGPT Plus (codex exec)."),
            new(
                Id: "claude",
                DisplayName: "Claude Code CLI",
                PlanName: "Claude Pro",
                ExecutablePath: _options.Cli.ClaudePath,
                Installed: _cliRunner.IsExecutableAvailable(_options.Cli.ClaudePath),
                Description: "Agente CLI da Anthropic autenticado via Claude Pro (claude -p)."),
            new(
                Id: "gemini",
                DisplayName: "Google Antigravity / Gemini CLI",
                PlanName: "Google AI Pro",
                ExecutablePath: _options.Cli.GeminiPath,
                Installed: _cliRunner.IsExecutableAvailable(_options.Cli.GeminiPath),
                Description: "Agente CLI do Google autenticado via Google AI Pro (agy / gemini).")
        };
    }

    public async Task<AgentResponseEnvelope> ExecutePromptAsync(AgentChatRequest request, CancellationToken ct = default)
    {
        var prompt = (request.Prompt ?? string.Empty).Trim();
        var provider = string.IsNullOrWhiteSpace(request.Provider) ? "codex" : request.Provider.Trim().ToLowerInvariant();
        var touchedFiles = new List<string>();

        // 1. Deterministic Vault intent actions (fast, immediate markdown updates)
        await ApplyVaultIntentMutationsAsync(prompt, touchedFiles, ct);

        // 2. Try CLI execution if requested with "cli:" prefix or when CLI returns structured JSON
        var executedViaCli = false;
        string? cliReplyText = null;
        IReadOnlyList<GenerativeUiBlock>? cliBlocks = null;

        if (prompt.StartsWith("cli:", StringComparison.OrdinalIgnoreCase))
        {
            var rawCliPrompt = prompt[4..].Trim();
            var cliResult = await InvokeSelectedCliAsync(provider, rawCliPrompt, ct);
            if (cliResult.Executed)
            {
                executedViaCli = true;
                if (TryExtractEnvelopeFromStdout(cliResult.StdOut, out var parsedReply, out var parsedBlocks))
                {
                    cliReplyText = parsedReply;
                    cliBlocks = parsedBlocks;
                }
                else if (!string.IsNullOrWhiteSpace(cliResult.StdOut))
                {
                    cliReplyText = cliResult.StdOut.Trim();
                }
            }
        }

        // 3. Build Generative UI blocks dynamically matching the user's request
        var overview = await _vaultService.GetOverviewAsync(null, ct);
        var uiBlocks = cliBlocks is { Count: > 0 }
            ? cliBlocks.ToList()
            : await BuildDynamicUiBlocksForPromptAsync(prompt, overview, ct);

        var replyText = !string.IsNullOrWhiteSpace(cliReplyText)
            ? cliReplyText
            : BuildContextualReply(prompt, provider, overview, uiBlocks, touchedFiles);

        return new AgentResponseEnvelope(
            ProviderUsed: provider,
            ExecutedViaCli: executedViaCli,
            ReplyText: replyText,
            TouchedFiles: touchedFiles,
            UiBlocks: uiBlocks,
            TimestampUtc: DateTime.UtcNow);
    }

    public static bool TryExtractEnvelopeFromStdout(
        string stdout,
        out string? replyText,
        out IReadOnlyList<GenerativeUiBlock>? blocks)
    {
        replyText = null;
        blocks = null;
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return false;
        }

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

            if (root.TryGetProperty("replyText", out var replyProp))
            {
                replyText = replyProp.GetString();
            }

            if (root.TryGetProperty("uiBlocks", out var blocksProp) && blocksProp.ValueKind == JsonValueKind.Array)
            {
                blocks = JsonSerializer.Deserialize<List<GenerativeUiBlock>>(blocksProp.GetRawText(), JsonOptions);
            }

            return replyText is not null || (blocks is not null && blocks.Count > 0);
        }
        catch
        {
            return false;
        }
    }

    private async Task<CliProcessResult> InvokeSelectedCliAsync(string provider, string prompt, CancellationToken ct)
    {
        var systemWrappedPrompt =
            $"Você está operando o cofre Obsidian em '{_guard.VaultRoot}'. Siga o arquivo AGENTS.md. Pedido do usuário: {prompt}";

        return provider switch
        {
            "claude" => await _cliRunner.RunAsync(
                _options.Cli.ClaudePath,
                new[] { "-p", systemWrappedPrompt },
                _guard.VaultRoot,
                _options.Cli.ExecutionTimeoutSeconds,
                ct),
            "gemini" => await _cliRunner.RunAsync(
                _options.Cli.GeminiPath,
                new[] { "--prompt", systemWrappedPrompt },
                _guard.VaultRoot,
                _options.Cli.ExecutionTimeoutSeconds,
                ct),
            _ => await _cliRunner.RunAsync(
                _options.Cli.CodexPath,
                new[] { "exec", "-C", _guard.VaultRoot, systemWrappedPrompt },
                _guard.VaultRoot,
                _options.Cli.ExecutionTimeoutSeconds,
                ct)
        };
    }

    private async Task ApplyVaultIntentMutationsAsync(string prompt, List<string> touchedFiles, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        var lower = prompt.ToLowerInvariant();

        // Intent: Quick inbox capture ("anota:", "captura:", "ideia:")
        var captureMatch = Regex.Match(prompt, @"^(?:anota|anotar|captura|capturar|inbox|ideia)\s*:\s*(?<text>.+)$", RegexOptions.IgnoreCase);
        if (captureMatch.Success)
        {
            var text = captureMatch.Groups["text"].Value.Trim();
            var created = await _vaultService.CreateStructuredNoteAsync(new DynamicNoteCreateRequest(
                NoteType: "inbox",
                Title: $"Captura {DateTime.Now:yyyy-MM-dd HHmmss}",
                Content: text,
                Area: "Inbox",
                Tags: new[] { "inbox", "captura-rapida" }), ct);
            touchedFiles.Add(created.RelativePath);
        }

        // Intent: Toggle habit by voice/text ("marcar hábito orar", "concluir hábito café")
        if (lower.Contains("marcar hábito") || lower.Contains("marcar habito") || lower.Contains("concluir hábito"))
        {
            var habits = await _vaultService.GetDailyHabitsAsync(null, ct);
            var matched = habits.FirstOrDefault(h => prompt.Contains(h.Label, StringComparison.OrdinalIgnoreCase) ||
                                                     h.Label.Split(' ').Any(w => w.Length > 3 && prompt.Contains(w, StringComparison.OrdinalIgnoreCase)));
            if (matched is not null)
            {
                await _vaultService.ToggleHabitAsync(matched.Label, null, ct);
                touchedFiles.Add($"daily/{DateTime.Now:yyyy-MM-dd}.md");
            }
        }
    }

    private async Task<List<GenerativeUiBlock>> BuildDynamicUiBlocksForPromptAsync(
        string prompt,
        VaultOverviewDto overview,
        CancellationToken ct)
    {
        var lower = prompt.ToLowerInvariant();
        var blocks = new List<GenerativeUiBlock>();

        var wantsAll = string.IsNullOrWhiteSpace(lower) ||
                       lower.Contains("dashboard") ||
                       lower.Contains("visão geral") ||
                       lower.Contains("tudo") ||
                       lower.Contains("resumo") ||
                       lower.Contains("hoje");

        var wantsMetrics = wantsAll || lower.Contains("métrica") || lower.Contains("metrica") || lower.Contains("kpi") || lower.Contains("estatística");
        var wantsHabits = wantsAll || lower.Contains("hábito") || lower.Contains("habito") || lower.Contains("rotina") || lower.Contains("cronograma");
        var wantsKanban = wantsAll || lower.Contains("kanban") || lower.Contains("tarefa") || lower.Contains("profissional") || lower.Contains("pessoal");
        var wantsForm = lower.Contains("formulário") || lower.Contains("formulario") || lower.Contains("cadastrar") || lower.Contains("criar nota") || lower.Contains("reunião") || lower.Contains("reuniao") || lower.Contains("form");
        var wantsNotes = wantsAll || lower.Contains("nota") || lower.Contains("zettelkasten") || lower.Contains("estudo") || lower.Contains("seguro") || lower.Contains("csharp") || lower.Contains("índice");

        if (wantsMetrics)
        {
            blocks.Add(new GenerativeUiBlock(
                Id: "block-kpis",
                Type: GenerativeUiBlockTypes.KpiGrid,
                Title: "Métricas do Cofre & Execução Diária",
                Subtitle: $"Sincronizado com {overview.TodayNotePath}",
                Metrics: new List<KpiMetricItemDto>
                {
                    new("Hábitos Hoje", $"{overview.HabitsCompletedToday}/{overview.HabitsTotalToday}", "Rotina matinal & diária", "emerald"),
                    new("Zettelkasten", overview.TotalZettelkastenNotes.ToString(), $"{overview.TotalIndices} Índices ativos", "indigo"),
                    new("Notas Diárias", overview.TotalDailyNotes.ToString(), "Histórico no cofre", "cyan"),
                    new("Inbox Pendente", overview.InboxPendingCount.ToString(), "Capturas rápidas", "amber")
                }));
        }

        if (wantsHabits)
        {
            blocks.Add(new GenerativeUiBlock(
                Id: "block-habits",
                Type: GenerativeUiBlockTypes.HabitChecklist,
                Title: "Rotina & Hábitos de Hoje",
                Subtitle: "Clique ou peça por voz para marcar/desmarcar direto no Markdown",
                Habits: overview.TodayHabits));
        }

        if (wantsKanban)
        {
            var targetBoardName = lower.Contains("pessoal") ? "Pessoal" : "Profissional";
            var board = overview.KanbanBoards.FirstOrDefault(b => string.Equals(b.BoardName, targetBoardName, StringComparison.OrdinalIgnoreCase))
                        ?? overview.KanbanBoards.FirstOrDefault()
                        ?? new KanbanBoardDto(targetBoardName, $"assets/kanban/{targetBoardName}.md", new List<KanbanColumnDto>
                        {
                            new("To Do", Array.Empty<KanbanTaskDto>()),
                            new("Doing", Array.Empty<KanbanTaskDto>()),
                            new("Done", Array.Empty<KanbanTaskDto>())
                        });

            blocks.Add(new GenerativeUiBlock(
                Id: $"block-kanban-{board.BoardName.ToLowerInvariant()}",
                Type: GenerativeUiBlockTypes.KanbanBoard,
                Title: $"Quadro Kanban — {board.BoardName}",
                Subtitle: board.RelativePath,
                Kanban: board));
        }

        if (wantsForm)
        {
            var isMeeting = lower.Contains("reunião") || lower.Contains("reuniao");
            blocks.Add(new GenerativeUiBlock(
                Id: "block-dynamic-form",
                Type: GenerativeUiBlockTypes.DynamicForm,
                Title: isMeeting ? "Nova Ata de Reunião (Gerado pela IA)" : "Captura Estruturada no Cofre (Gerado pela IA)",
                Subtitle: "Preenche automaticamente o YAML frontmatter e salva na pasta correta",
                Form: new DynamicFormSchemaDto(
                    FormId: isMeeting ? "meeting-form" : "note-form",
                    NoteType: isMeeting ? "reuniao" : "nota",
                    SubmitLabel: "Salvar no Obsidian",
                    Fields: new List<DynamicFormFieldDto>
                    {
                        new("title", "Título da Nota", "text", true, isMeeting ? "Ex: Alinhamento Arquitetura Confitec" : "Ex: Padrão CQRS no .NET 10"),
                        new("noteType", "Destino no Cofre", "select", true, null, isMeeting ? "inbox" : "nota", new[] { "inbox", "nota", "indice", "daily" }),
                        new("area", "Área / Índice Relacionado", "select", false, null, "CSharp", new[] { "CSharp", "Seguros - Confitec", "Fast MBA", "GranFramework", "Pessoal" }),
                        new("tags", "Tags (separadas por vírgula)", "text", false, "ia, obsidian, estudo", isMeeting ? "reuniao, confitec" : "nota, zettelkasten"),
                        new("content", "Conteúdo / Pontos Principais", "textarea", true, "Digite ou dite os detalhes...")
                    })));
        }

        if (wantsNotes)
        {
            string? searchFilter = null;
            if (lower.Contains("seguro")) searchFilter = "seguro";
            else if (lower.Contains("csharp") || lower.Contains("c#") || lower.Contains("linq")) searchFilter = "LINQ";

            var notes = searchFilter is not null
                ? await _vaultService.QueryNotesAsync(searchFilter, null, 8, ct)
                : overview.RecentNotes;

            blocks.Add(new GenerativeUiBlock(
                Id: "block-notes",
                Type: GenerativeUiBlockTypes.MarkdownNoteList,
                Title: "Explorador Zettelkasten & Índices",
                Subtitle: "Notas reais lidas do seu cofre Obsidian",
                Notes: notes));
        }

        if (blocks.Count == 0)
        {
            blocks.Add(new GenerativeUiBlock(
                Id: "block-kpis-default",
                Type: GenerativeUiBlockTypes.KpiGrid,
                Title: "Resumo Rápido do Cofre",
                Metrics: new List<KpiMetricItemDto>
                {
                    new("Hábitos Hoje", $"{overview.HabitsCompletedToday}/{overview.HabitsTotalToday}", "Nota diária", "emerald"),
                    new("Zettelkasten", overview.TotalZettelkastenNotes.ToString(), "Notas atômicas", "indigo")
                }));
        }

        return blocks;
    }

    private static string BuildContextualReply(
        string prompt,
        string provider,
        VaultOverviewDto overview,
        IReadOnlyList<GenerativeUiBlock> blocks,
        IReadOnlyList<string> touchedFiles)
    {
        var touchedMsg = touchedFiles.Count > 0
            ? $" Atualizei no cofre: {string.Join(", ", touchedFiles)}."
            : string.Empty;

        return $"[{provider.ToUpperInvariant()}] Montei {blocks.Count} bloco(s) dinâmico(s) na tela para '{(string.IsNullOrWhiteSpace(prompt) ? "Visão Geral" : prompt)}'. " +
               $"Hoje você concluiu {overview.HabitsCompletedToday} de {overview.HabitsTotalToday} hábitos e possui {overview.TotalZettelkastenNotes} notas no Zettelkasten.{touchedMsg}";
    }
}
