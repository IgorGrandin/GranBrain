# ADR-0001: Arquitetura Monorepo GranBrain (Obsidian Backend + Multi-CLI Agent + Generative UI)

- **Status:** Aceito
- **Data:** 2026-10-29

## Contexto
O cofre Obsidian (`G:\Meu Drive\Softwares\Obsidian\Igor Grandin`) acumula alto custo de manutenção manual para registro diário de tarefas, hábitos, atas de reunião e notas de estudo Zettelkasten. Além disso, o usuário possui assinaturas ativas de **ChatGPT Plus (Codex CLI)**, **Claude Pro (Claude Code CLI)** e **Google AI Pro (Antigravity/Gemini CLI)**, sem uso de chaves de API tarifadas por token.

## Decisão
1. **Obsidian como Backend (Fonte de Verdade em Markdown):** Todos os dados permanecem em arquivos `.md` dentro do `VaultPath`, governados por um protocolo `AGENTS.md` na raiz do cofre, pasta `inbox/` para captura zero-esforço, `Home.md` nativo e frontmatter YAML padronizado.
2. **Backend `.NET 10` (`backend/`):**
   - `VaultPathGuard` (`Vault Jail`) impedindo qualquer leitura/escrita fora do `VaultPath`.
   - Autenticação JWT com proteção anti-brute-force (`RateLimiter`) para acesso web/mobile seguro.
   - Orquestrador `IAgentCliProvider` invocando CLIs locais (`codex`, `claude`, `agy`/`gemini`) no diretório do cofre e exigindo contrato de resposta estruturado (`AgentResponseEnvelope`) com blocos de **Generative UI**.
3. **Frontend `Ionic Angular` (`frontend/`):**
   - Login seguro, seletor livre de provedor de IA, comando de voz bidirecional (`Web Speech API` + `SpeechSynthesis`) e motor de **Generative UI** capaz de renderizar dinamicamente 5 blocos canônicos: `kpi-grid`, `habit-checklist`, `kanban-board`, `dynamic-form` e `markdown-note-list`.
