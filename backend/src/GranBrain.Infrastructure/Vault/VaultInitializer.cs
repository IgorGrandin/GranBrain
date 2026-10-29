using System.Text;
using System.Text.RegularExpressions;
using GranBrain.Core.Security;

namespace GranBrain.Infrastructure.Vault;

public sealed record VaultBootstrapResult(
    bool CreatedAgentsMd,
    bool CreatedHomeMd,
    bool CreatedInboxDirectory,
    IReadOnlyList<string> SanitizedTemplates);

public sealed class VaultInitializer
{
    private readonly VaultPathGuard _guard;

    public VaultInitializer(VaultPathGuard guard)
    {
        _guard = guard;
    }

    public async Task<VaultBootstrapResult> EnsureInitializedAsync(CancellationToken ct = default)
    {
        var root = _guard.VaultRoot;
        Directory.CreateDirectory(root);

        var inboxPath = _guard.ResolveSafePath("inbox");
        var createdInbox = false;
        if (!Directory.Exists(inboxPath))
        {
            Directory.CreateDirectory(inboxPath);
            createdInbox = true;
        }

        var agentsMdPath = _guard.ResolveSafePath("AGENTS.md");
        var createdAgents = false;
        if (!File.Exists(agentsMdPath))
        {
            await File.WriteAllTextAsync(agentsMdPath, BuildAgentsMarkdown(), Encoding.UTF8, ct);
            createdAgents = true;
        }

        var homeMdPath = _guard.ResolveSafePath("Home.md");
        var createdHome = false;
        if (!File.Exists(homeMdPath))
        {
            await File.WriteAllTextAsync(homeMdPath, BuildHomeMarkdown(), Encoding.UTF8, ct);
            createdHome = true;
        }

        var sanitized = new List<string>();
        var templatesDir = _guard.ResolveSafePath("templates");
        if (Directory.Exists(templatesDir))
        {
            foreach (var file in Directory.EnumerateFiles(templatesDir, "*.md", SearchOption.TopDirectoryOnly))
            {
                var content = await File.ReadAllTextAsync(file, Encoding.UTF8, ct);
                var updated = SanitizeTemplateFrontmatter(Path.GetFileName(file), content);
                if (!string.Equals(content, updated, StringComparison.Ordinal))
                {
                    await File.WriteAllTextAsync(file, updated, Encoding.UTF8, ct);
                    sanitized.Add(Path.GetFileName(file));
                }
            }
        }

        return new VaultBootstrapResult(createdAgents, createdHome, createdInbox, sanitized);
    }

    public static string SanitizeTemplateFrontmatter(string fileName, string rawContent)
    {
        var normalized = rawContent.Replace("\r\n", "\n");

        // Collapse duplicate closing YAML delimiters (--- followed immediately by ---)
        var duplicateDelimiterRegex = new Regex(@"^---\n([\s\S]*?)\n---\n\s*---\n", RegexOptions.Multiline);
        if (duplicateDelimiterRegex.IsMatch(normalized))
        {
            normalized = duplicateDelimiterRegex.Replace(normalized, "---\n$1\n---\n", 1);
        }

        // If daily template has no YAML frontmatter, prepend clean YAML frontmatter
        if (!normalized.TrimStart().StartsWith("---", StringComparison.Ordinal) &&
            fileName.Contains("Di", StringComparison.OrdinalIgnoreCase))
        {
            var frontmatter = """
                ---
                type: daily
                created: "{{date:YYYY-MM-DD}}"
                tags:
                  - daily
                ---

                """.Replace("\r\n", "\n");
            normalized = frontmatter + normalized;
        }

        return normalized;
    }

    private static string BuildAgentsMarkdown() => """
        ---
        type: system-protocol
        created: "2026-10-29"
        tags:
          - granbrain
          - agents
        ---

        # 🧠 GranBrain — Protocolo Oficial do Cofre Obsidian (Igor Grandin)

        Este arquivo define as regras que qualquer agente de IA (`Codex`, `Claude Code`, `Gemini / Antigravity`) deve obedecer ao operar neste cofre.

        ## 1. Estrutura de Diretórios
        - `daily/`: Notas diárias no formato `YYYY-MM-DD.md`. Contêm hábitos do dia, tarefas, reuniões e aprendizados.
        - `inbox/`: Caixa de entrada de captura rápida (Zero-Esforço). Notas brutas para triagem automática.
        - `zettelkasten/`: Notas atômicas permanentes e de literatura (`type: nota` ou `type: permanente`).
        - `zettelkasten/Índices/`: Mapas de Conteúdo (MOCs) como `CSharp.md`, `Curso de Seguros - Confitec.md`, `Fast MBA - Solução Estratégica de Problemas.md`.
        - `assets/kanban/`: Quadros Kanban (`Profissional.md` e `Pessoal.md`) com colunas `## To Do`, `## Doing`, `## Done`, `## Blocked`.
        - `templates/`: Modelos padronizados com YAML frontmatter único.

        ## 2. Regras Inegociáveis para Agentes
        1. **Nunca apague notas existentes** em `daily/` ou `zettelkasten/`.
        2. **Frontmatter YAML Único:** Toda nota nova deve iniciar com um único bloco `--- ... ---` contendo ao menos `type`, `created` e `tags`.
        3. **Conexão Zettelkasten:** Ao criar uma nota de estudo ou conceito técnico, adicione `[[WikiLinks]]` para os conceitos relacionados e referencie o Índice correspondente em `zettelkasten/Índices/`.
        """;

    private static string BuildHomeMarkdown() => """
        ---
        type: dashboard
        created: "2026-10-29"
        tags:
          - dashboard
          - granbrain
        ---

        # 🚀 GranBrain Command Center — Igor Grandin

        > Painel sincronizado com o sistema **GranBrain** (.NET 10 + Ionic Angular Generative UI).

        ## 📌 Acesso Rápido
        - 📋 Kanban: [[Profissional]] | [[Pessoal]]
        - 💡 Ideias: [[Fleeting]] | [[Permanente]]
        - 📚 Índices Principais:
          - [[CSharp]]
          - [[dotNet Core - Um curso orientado para o Mercado de Trabalho]]
          - [[Curso de Seguros - Confitec]]
          - [[ENS - Iniciação em Seguros]]
          - [[Fast MBA - Solução Estratégica de Problemas]]

        ## 📥 Inbox (Pendentes de Triagem)
        ```dataview
        TABLE created as "Criado em", tags as "Tags"
        FROM "inbox"
        SORT file.mtime DESC
        LIMIT 10
        ```

        ## 🧠 Últimos Conceitos no Zettelkasten
        ```dataview
        TABLE tags as "Tags", file.mtime as "Atualizado"
        FROM "zettelkasten"
        WHERE file.name != "Fleeting" AND file.name != "Permanente"
        SORT file.mtime DESC
        LIMIT 12
        ```

        ## 📅 Últimas Notas Diárias
        ```dataview
        LIST
        FROM "daily"
        SORT file.name DESC
        LIMIT 7
        ```
        """;
}
