# 0001 — GranBrain MVP: Obsidian AI OS com Orquestrador Multi-CLI e Generative UI

## Objetivo
Entregar a primeira versão funcional ponta a ponta do **GranBrain** (monorepo `.NET 10` + `Ionic Angular`), conectada ao cofre Obsidian em `G:\Meu Drive\Softwares\Obsidian\Igor Grandin`, permitindo captura zero-esforço, automação do cofre via CLIs de IA autenticadas (`Codex`, `Claude Code`, `Gemini/Antigravity`), proteção por login JWT + `Vault Jail`, e uma interface moderna com voz e **Generative UI** (renderização dinâmica de 5 blocos visuais).

## Contexto
O cofre Obsidian atual do usuário possui 148 notas Zettelkasten, 5 índices de estudo e 78 notas diárias, mas sofre de abandono recorrente devido ao esforço manual para formatar notas, atualizar quadros Kanban (`Profissional.md` e `Pessoal.md`) e manter templates sem frontmatter YAML padronizado. Inspirado na arquitetura *"Obsidian Backend"* (Eric Michaud), o `GranBrain` transforma o cofre no banco de dados Markdown do sistema, acoplando as assinaturas Pro/Plus do usuário via CLIs locais e um frontend `Ionic Angular` capaz de montar telas sob demanda por voz ou texto.

## Escopo
**Dentro:**
- Governança e bootstrap seguro do Cofre Obsidian (`AGENTS.md`, `inbox/`, `Home.md` nativo com Dataview e templates YAML corrigidos sem sobrescrever notas antigas).
- Segurança de borda e confinamento (`VaultPathGuard` bloqueando qualquer acesso fora do cofre, autenticação JWT com senha hash e rate-limiting anti-brute-force no login).
- Motor de leitura e escrita do cofre no Backend `.NET 10` (leitura/toggle de hábitos na nota diária, leitura/movimentação/criação de cards nos Kanbans `Profissional.md` e `Pessoal.md`, captura na `inbox/`, criação de notas estruturadas por formulário dinâmico e listagem/busca de notas Zettelkasten e Índices).
- Orquestrador Multi-IA via CLI (`IAgentCliProvider` com suporte a `codex`, `claude` e `gemini`/`agy`, verificação de disponibilidade de cada binário e retorno de envelope JSON contendo resposta + blocos `GenerativeUiBlock`).
- Frontend `Ionic Angular` com tela de Login, seletor de provedor de IA, chat com entrada/saída de voz (`Web Speech API` / `SpeechSynthesis`), barra de Quick Capture e renderizador dinâmico dos 5 blocos de Generative UI (`kpi-grid`, `habit-checklist`, `kanban-board`, `dynamic-form`, `markdown-note-list`).

**Fora:**
- Provisionamento automatizado da VPS na Contabo e configuração de Cloudflare Tunnel (previsto para a Fase 2 após validação local).
- Integração com bots externos de WhatsApp/Telegram (o próprio app web/mobile Ionic Angular é o canal direto).
- Autenticação multifator TOTP (2FA) obrigatória no primeiro acesso (deixada como evolução opcional da camada de autenticação).

## Critérios de aceite
1. **AC1 — Bootstrap Seguro do Cofre Obsidian:** Ao iniciar ou acionar o bootstrap do cofre, o sistema cria `AGENTS.md`, `Home.md` e o diretório `inbox/` na raiz do `VaultPath` e corrige os marcadores YAML duplicados (`---`) em `templates/`, preservando 100% das notas existentes em `daily/`, `zettelkasten/` e `assets/`.
2. **AC2 — Confinamento de Diretório (`Vault Jail`):** Qualquer tentativa de ler ou gravar arquivo com caminho relativo que escape do `VaultPath` (ex.: `../../Windows/win.ini` ou caminho absoluto externo) é rejeitada por `VaultPathGuard` com erro `vault_path_violation` (HTTP 400/403).
3. **AC3 — Autenticação JWT e Anti-Brute-Force:** Endpoints de cofre e agente (`/api/vault/**` e `/api/agent/**`) rejeitam requisições sem token JWT válido com HTTP 401; credenciais válidas em `/api/auth/login` emitem JWT válido, credenciais inválidas retornam HTTP 401 (`invalid_credentials`), e excesso de tentativas dispara HTTP 429 (`rate_limit_exceeded`).
4. **AC4 — Operações Reais no Cofre (Hábitos, Kanban, Inbox, Zettelkasten e Formulário Dinâmico):**
   - Marcar/desmarcar um hábito atualiza literalmente `- [ ]` ↔ `- [x]` na nota diária (`daily/YYYY-MM-DD.md`), criando-a a partir do template se ainda não existir hoje.
   - Adicionar ou mover um item de Kanban atualiza a seção correspondente (`## To Do`, `## Doing`, `## Done`, `## Blocked`) em `assets/kanban/Profissional.md` ou `assets/kanban/Pessoal.md`.
   - Submeter uma captura rápida ou um formulário dinâmico (`reuniao`, `nota`, `tarefa`, `inbox`) grava o arquivo `.md` correspondente com frontmatter YAML válido dentro do cofre.
5. **AC5 — Orquestrador Multi-CLI e Generative UI no Backend:** O endpoint `/api/agent/providers` lista os provedores (`codex`, `claude`, `gemini`) indicando quais binários estão instalados no host; o endpoint `/api/agent/chat` aceita o provedor escolhido e o prompt (texto/voz), executa o provedor CLI (ou motor estruturado do cofre quando solicitado/fallback) e devolve um `AgentResponseEnvelope` válido contendo `replyText` e a lista de `uiBlocks` tipados (`kpi-grid`, `habit-checklist`, `kanban-board`, `dynamic-form`, `markdown-note-list`).
6. **AC6 — Frontend Ionic Angular com Voz e Renderização Dinâmica (Generative UI):** O app Angular exibe tela de login funcional, seletor de IA no topo, chat com botão de microfone (voz para texto e leitura em voz alta) e renderiza dinamicamente na tela principal qualquer combinação dos 5 blocos (`kpi-grid`, `habit-checklist`, `kanban-board`, `dynamic-form`, `markdown-note-list`) retornados pelo backend, permitindo interação direta (marcar hábito, mover card Kanban, enviar formulário dinâmico).

## Área de impacto esperada
- `backend/GranBrain.sln`
- `backend/src/GranBrain.Core/**`
- `backend/src/GranBrain.Infrastructure/**`
- `backend/src/GranBrain.Api/**`
- `backend/tests/GranBrain.UnitTests/**`
- `frontend/**`
- Cofre Obsidian externo configurado em `VaultPath`: `G:\Meu Drive\Softwares\Obsidian\Igor Grandin` (`AGENTS.md`, `Home.md`, `inbox/`, `templates/`)

## Referências de arquitetura
- [CLAUDE.md](../../CLAUDE.md)
- [ADR-0001: Arquitetura Monorepo GranBrain](../../docs/adr/0001-granbrain-architecture.md)

## Decisões do dev
| ID | Resumo | Data |
|---|---|---|
| D1 | Monorepo `GranBrain` em `C:\Users\igor.grandin\Documents\Projects\GranBrain` com specs locais `backlog/tasks/` | 2026-10-29 |
| D2 | Cofre Obsidian desacoplado via configuração `VaultPath` com `VaultInitializer` não-destrutivo | 2026-10-29 |
| D3 | Orquestrador Multi-IA via adaptadores CLI (`IAgentCliProvider` para `codex`, `claude`, `gemini`/`agy`) retornando envelope de Generative UI | 2026-10-29 |
| D4 | Segurança Defense-in-Depth com Login JWT, Rate Limiting anti-brute-force e `Vault Jail` contra Path Traversal | 2026-10-29 |
| D5 | Catálogo MVP de 5 blocos de Generative UI (`kpi-grid`, `habit-checklist`, `kanban-board`, `dynamic-form`, `markdown-note-list`) + Voz bidirecional | 2026-10-29 |

### D1 — Estrutura do Monorepo `GranBrain` e Rastreamento Local de Specs
- **Problema:** O projeto precisa seguir o fluxo disciplinado de `/spec-task`, `/plan-task` e `/execute-ig`, mas é um produto pessoal do desenvolvedor (prefixo `Gran`), separado do Azure Boards corporativo (`brixbroker`).
- **Decisão:** Criar o monorepo Git em `C:\Users\igor.grandin\Documents\Projects\GranBrain` (conectado a `https://github.com/IgorGrandin/GranBrain`) contendo `backend/` (`.NET 10`) e `frontend/` (`Ionic Angular`), usando `backlog/tasks/<id>-<slug>.md` como fonte canônica de especificação e decisões.
- **Motivo:** Mantém toda a rastreabilidade e qualidade de engenharia sem poluir o board corporativo de outro projeto.
- **Alternativas descartadas:** Criar Work Items no projeto `brixbroker` do Azure DevOps (descartado por misturar escopo pessoal com corporativo); repositórios separados para front e back (descartado por dificultar commits e contratos sincronizados).
- **Impacto e dependências:** Todas as skills leem as decisões diretamente de `backlog/tasks/0001-granbrain-mvp-obsidian-ai-os.md`.
- **Incertezas:** nenhuma.

### D2 — Cofre Obsidian Desacoplado via `VaultPath` e Bootstrapper Seguro
- **Problema:** O cofre principal vive em `G:\Meu Drive\Softwares\Obsidian\Igor Grandin` (sincronizado pelo Google Drive e com centenas de imagens/PDFs), e seus templates atuais possuem pequenos defeitos de YAML (`---` duplicado), além de não ter `AGENTS.md` nem pasta `inbox/`.
- **Decisão:** Manter o cofre fora do repositório Git do código, referenciado pela configuração `GranBrain:VaultPath`, e criar um serviço `VaultInitializer` no backend que injeta `AGENTS.md`, `Home.md` e `inbox/` e corrige os templates sem jamais apagar ou sobrescrever notas existentes.
- **Motivo:** Evita versionar binários pesados do Google Drive no código-fonte e permite apontar para o caminho local no Windows ou para o mount do Google Drive no servidor Linux da Contabo.
- **Alternativas descartadas:** Mover o cofre para dentro do repositório Git (descartado pelo peso de `assets/images/` e acoplamento com o Google Drive Desktop).
- **Impacto e dependências:** Exige validação rigorosa de caminhos (`D4`) e testes unitários com diretório temporário simulando o cofre.
- **Incertezas:** nenhuma.

### D3 — Orquestrador Multi-IA Baseado em CLIs Autenticadas (`Codex`, `Claude`, `Gemini/Antigravity`)
- **Problema:** O desenvolvedor possui assinaturas pagas mensais (Google AI Pro, Claude Pro, ChatGPT Plus), mas não planos de API cobrados por token. O sistema precisa permitir conversar e comandar o cofre trocando livremente entre essas IAs.
- **Decisão:** Implementar a interface `IAgentCliProvider` no backend `.NET 10` com adaptadores para `codex` (`codex exec`), `claude` (`claude -p`) e `gemini`/`agy`, executando no diretório do cofre (`VaultPath`) e retornando um envelope JSON padronizado (`AgentResponseEnvelope`) que combina resposta textual com blocos de Generative UI. Incluir motor de intenção local/fallback quando a CLI escolhida não estiver instalada ou quando o comando for uma ação direta de cofre.
- **Motivo:** Aproveita 100% as assinaturas Pro/Plus já pagas e o poder agentic nativo das CLIs sobre arquivos Markdown.
- **Alternativas descartadas:** Consumo exclusivo via SDK HTTP com `API_KEY` paga por token (descartado por gerar custo extra desnecessário); repasse de stdout bruto sem JSON estruturado (descartado por inviabilizar a Generative UI).
- **Impacto e dependências:** Depende de `D4` para restringir o diretório de trabalho das CLIs ao `VaultPath` e alimenta `D5` no frontend.
- **Incertezas:** `claude` CLI ainda não está instalado no PATH local do Windows (apenas `codex.exe` e `agy.exe` foram detectados); o endpoint `/api/agent/providers` deve reportar o status real de instalação de cada CLI e permitir uso imediato dos instalados + motor inteligente integrado.

### D4 — Segurança Defense-in-Depth (JWT + Rate Limiting + `Vault Jail`)
- **Problema:** Como o sistema ficará disponível via web/domínio na Contabo com as CLIs logadas nas contas pessoais do desenvolvedor, qualquer acesso não autorizado exporia tanto as notas pessoais quanto o uso das IAs.
- **Decisão:** Exigir autenticação JWT (`/api/auth/login` com validação de credencial hash + Rate Limiting contra força bruta) em todas as rotas `/api/vault/**` e `/api/agent/**`, e aplicar `VaultPathGuard` (`Vault Jail`) que resolve o caminho canônico (`Path.GetFullPath`) e bloqueia qualquer operação fora de `VaultPath`.
- **Motivo:** Protege a aplicação na camada HTTP e impede que qualquer comando ou requisição malformada acesse arquivos do sistema operacional fora do cofre Obsidian.
- **Alternativas descartadas:** Deixar a API aberta confiando apenas em obscuridade de porta ou rede local (descartado pelo risco na publicação em servidor público Contabo).
- **Impacto e dependências:** Todos os controllers/endpoints de cofre e agente exigem `[Authorize]` e todos os acessos a disco passam por `VaultPathGuard`.
- **Incertezas:** nenhuma.

### D5 — Catálogo MVP de 5 Blocos de Generative UI + Voz Bidirecional no Ionic Angular
- **Problema:** O usuário deseja que o frontend construa telas dinamicamente conforme pedidos por voz ou texto (ex.: mostrar métricas, checklists, kanban, formulários de cadastro ou notas de estudo).
- **Decisão:** Definir um contrato compartilhado de `GenerativeUiBlock` com 5 componentes dinâmicos no Angular (`kpi-grid`, `habit-checklist`, `kanban-board`, `dynamic-form`, `markdown-note-list`) mais integração de voz no chat via `Web Speech API` (`SpeechRecognition` e `SpeechSynthesis`).
- **Motivo:** Garante tipagem forte, visual moderno consistente (Dark Mode estilo Command Center) e interatividade real com o cofre sem precisar compilar código em runtime no navegador.
- **Alternativas descartadas:** Gerar HTML/JS arbitrário via `innerHTML` sem sanitização (descartado por vulnerabilidade XSS e quebra de layout).
- **Impacto e dependências:** Contrato compartilhado entre `GranBrain.Core` (DTOs de `GenerativeUiBlock`) e o componente `<app-generative-canvas>` no frontend Angular.
- **Incertezas:** nenhuma.
