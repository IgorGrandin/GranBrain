# 0005 — Rotinas Autônomas Noturnas e Triagem Inteligente do Inbox

## Objetivo

Implementar o motor de rotinas autônomas no backend `.NET 10` (`MorningPrepJob` às `06:00`, `InboxTriageJob` a cada `2 h` e sob demanda, `NightlyClosureJob` às `23:30`) com proteção estrita de cofre (`VaultPathGuard`), serialização concorrente (`SemaphoreSlim`), trilha de auditoria persistente em `inbox/_archive/routines-audit-log.json`, agendador configurável via `BackgroundService`, endpoints REST autenticados `/api/routines/*`, integração ao `AgentOrchestratorService` (`routines-panel`) e painel visual completo de **Rotinas e inbox** no Angular (`rail 0005`) fiel ao protótipo canônico via `/spec-frontend`.

## Contexto

Inspirado na tese do *"Obsidian Backend"*: para eliminar a fricção operacional que leva ao abandono do cofre, o GranBrain atua como escriba e arquivista determinístico sobre os diretórios reais do vault (`inbox/`, `daily/`, `zettelkasten/`, `zettelkasten/Índices/`, `assets/kanban/` e `templates/`). No mockup canônico aprovado ([granbrain-redesign.html](../../frontend/_imports/claude-design/granbrain-redesign/2026-10-30/original/granbrain-redesign.html)), a aba `Rotinas & Inbox` (`rail 0005`) já especifica os três cartões `.routine`, a fila de itens pendentes em `.row` com sugestão de destino e tag, o estado `.empty` (`"Inbox vazio. Tudo foi triado."`), o feed cronológico `.logrow` e o botão primário `[data-routine-triage]` (`Triar inbox agora`).

#### Reconciliação com o design-system — 2026-11-13

Comparação entre [granbrain-redesign.html](../../frontend/_imports/claude-design/granbrain-redesign/2026-10-30/original/granbrain-redesign.html) (`:238-246`, `:393`, `:395-397`, `:529`, `:740-744`, `:814-825`, `:1276-1308`, `:1527-1540`) e [styles.scss](../../frontend/src/styles.scss):

| Classe / Padrão no Mockup | Local no Mockup | Status em `styles.scss` | Bucket | Token / Valor Reconciliado |
| :--- | :--- | :--- | :--- | :--- |
| `.page-head`, `.eyebrow-row`, `.wi`, `.grid`, `.g-3`, `.g-2`, `.mt`, `.card`, `.card-h`, `.pill` (`.ok`, `.warn`, `.accent`), `.btn.primary`, `.mono`, `.muted`, `.small`, `.grow`, `.nt` | `granbrain-redesign.html:180-220` | Já implementado em `styles.scss:136-185`, `styles.scss:334` | A | `--surface`, `--surface-2`, `--line`, `--fg`, `--fg-2`, `--fg-3`, `--accent`, `--accent-soft`, `--ok`, `--ok-soft`, `--warn`, `--warn-soft`, `--info`, `--font-display`, `--font-mono`, `--radius-card`, `--radius-pill` |
| `.routine`, `.routine p` | `granbrain-redesign.html:395-397` | Ausente em `styles.scss` (novo bloco de componente usando tokens existentes) | A | `display: grid; gap: 8px; align-content: start;` e `margin: 0` em `.routine p` sobre `.card` (`--surface`, `--line`, `--radius-card`) |
| `.row`, `.row:last-child`, `button.row`, `button.row:last-child`, `button.row[aria-pressed="true"] strong` | `granbrain-redesign.html:238-241, 393` | Ausente em `styles.scss` | A | `display: flex; align-items: center; gap: 12px; padding: 12px 0; border-bottom: 1px solid var(--line); min-width: 0;`, estado ativo `var(--accent)` |
| `.empty` | `granbrain-redesign.html:242` | Ausente em `styles.scss` | A | `color: var(--fg-3); font-size: 14px; margin: 0; padding: 14px; border: 1px dashed var(--line-strong); border-radius: 10px; text-align: center;` |
| `.code` | `granbrain-redesign.html:243` | Ausente em `styles.scss` | A | `margin: 0; background: var(--surface-2); border-radius: 10px; padding: 14px; font: 12.5px/1.6 var(--font-mono); white-space: pre-wrap; color: var(--fg-2); overflow-x: auto;` |
| `.logrow`, `.logrow:last-child`, `.logrow p` | `granbrain-redesign.html:244-246` | Ausente em `styles.scss` | A | `display: grid; grid-template-columns: 120px minmax(0, 1fr); gap: 12px; padding: 11px 0; border-bottom: 1px solid var(--line);`, `.logrow p { margin: 0 0 2px; }` |
| `@media (max-width: 640px)` para `.logrow` | `granbrain-redesign.html:529` | Ausente em `styles.scss` | A | Em `@media (max-width: 640px)`: `.logrow { grid-template-columns: minmax(0, 1fr); gap: 4px; }` |

Resultado do `/spec-frontend`: **100% Balde A** — nenhuma variável `:root` nova é necessária em `frontend/src/styles.scss`; todas as classes faltantes reutilizam diretamente os tokens existentes em temas claro e escuro.

## Escopo

### Especificação visual

Fonte visual obrigatória: [granbrain-redesign.html](../../frontend/_imports/claude-design/granbrain-redesign/2026-10-30/original/granbrain-redesign.html) (`:238-246`, `:393`, `:395-397`, `:529`, `:740-744`, `:814-825`, `:1276-1308`, `:1527-1540`) e [styles.scss](../../frontend/src/styles.scss).

- **Cabeçalho da visão `Rotinas & Inbox` (`rail 0005`)**:
  - Navegação no rail esquerdo com badge `0005` e texto `Rotinas & Inbox`, definindo `currentView = 'rotinas'` (`.nav-item.on`) e atualizando o breadcrumb `.topbar .crumb` para `Rotinas & Inbox`.
  - Cabeçalho `.page-head` com `.eyebrow` (`Execução autônoma` / `Proposto`), tag `.wi` (`WI 0005`), título `Rotinas e inbox`, subtítulo `.lede` (`O GranBrain trabalha de madrugada. Nada é apagado: o que for triado vai para inbox/_archive/.`) e ação `.btn.primary` (`[data-routine-triage]`) com rótulo `Triar inbox agora` em repouso e `Triando…` com atributo `disabled` enquanto a requisição estiver em voo.
- **Grade de cartões de rotinas (`.grid.g-3`)**:
  - Renderiza os 3 cartões `.card.routine` (`MorningPrepJob` / `Preparação matinal`, `InboxTriageJob` / `Triagem do inbox`, `NightlyClosureJob` / `Fechamento noturno`).
  - Cada cartão exibe `.card-h` com `<h2>` e pill de status: `InboxTriageJob` exibe `.pill.warn` `Pendente` quando `inboxItems.length > 0` e `.pill.ok` `Em dia` quando o inbox está vazio; demais jobs exibem `.pill.ok` `Em dia` (ou `.pill.warn` caso último status seja `warn`/`error`), seguido do nome técnico em `.mono.muted` (`jobId`), janela agendada em `.small` e último resumo em `.small.muted` (`Última: ...`). Ações individuais `[data-routine-run]` permitem disparar `POST /api/routines/{jobId}/run` quando desejado.
- **Grade inferior em duas colunas (`.grid.g-2.mt`)**:
  - **Cartão esquerdo (`Inbox · N item/itens`)**: cabeçalho `.card-h` com contador dinâmico (`Inbox · N itens` / `Inbox · 1 item` / `Inbox · 0 itens`) e `.pill.accent` (`nada é apagado`). Cada item renderiza em `.row` com `<span class="grow"><strong class="nt">...</strong> <span class="small muted">sugestão → <span class="mono">...</span></span></span>` e tag opcional em `.pill`. Quando `inboxItems.length === 0`, exibe `<p class="empty">Inbox vazio. Tudo foi triado.</p>`.
  - **Cartão direito (`Registro de execução`)**: cabeçalho `.card-h` (`Registro de execução`) e lista cronológica de `.logrow` (`grid-template-columns: 120px minmax(0, 1fr)` no desktop e `minmax(0, 1fr)` em `max-width: 640px`), exibindo timestamp/rótulo em `.mono.muted` à esquerda e `<p>` à direita com descrição clara do resultado.
- **Feedback pós-triagem**:
  - Ao concluir `POST /api/routines/triage` (ou `POST /api/routines/{jobId}/run`), atualiza imediatamente os cartões, fila `.empty`, feed `.logrow` (topo mais recente) e dispara toast via `showToast(...)` (`ok` quando sucesso ou `danger` se a API falhar).

**Dentro:**

- Configuração e contratos tipados em `GranBrain.Core` (`RoutinesOptions`, `RoutineStatusCardDto`, `InboxQueueItemDto`, `RoutineExecutionLogDto`, `RoutinesOverviewDto`, `InboxTriageFileResultDto`, `RoutineRunResultDto`, `RoutineRunRequest`, `IRoutineExecutionService`, extensão de `GenerativeUiBlockTypes.RoutinesPanel = "routines-panel"` e `GenerativeUiBlock.RoutinesOverview`).
- Implementação do serviço `RoutineExecutionService` e do worker `RoutineSchedulerHostedService` (`BackgroundService`) em `GranBrain.Infrastructure/Jobs/`:
  - **Rotina 1 (`InboxTriageJob`):** varre `inbox/` (`SearchOption.TopDirectoryOnly`) para `*.md` e `*.txt` (ignorando `inbox/_archive/` e logs `.json`); classifica via heurística determinística (default) ou `ICliProcessRunner` quando `RoutinesOptions.UseCli == true` (com fallback automático para heurística se a CLI falhar/expirar/retornar JSON inválido); gera nota estruturada estritamente dentro de `zettelkasten/` (`zettelkasten/Reunião - <SafeTitle>.md` para reuniões/atas ou `zettelkasten/<SafeTitle>.md` para conceitos/estudos/capturas mobile) com YAML frontmatter único (`type`, `created`, `source_inbox`, `tags`) e `[[Fleeting]]` + `[[WikiLinks]]` para MOCs reais de `zettelkasten/Índices/`; garante criação não destrutiva em `zettelkasten/` (sufixo incremental ` - 2`, ` - 3`, ... se já existir); move o arquivo bruto intacto para `inbox/_archive/` (com sufixo seguro em colisão), nunca apagando nenhum arquivo; isola falhas por arquivo (exceção em um arquivo corrompido registra erro sem interromper os demais, exceto `VaultPathViolationException` que falha imediatamente).
  - **Rotina 2 (`NightlyClosureJob` — 23:30):** lê `daily/YYYY-MM-DD.md` na janela local (`CalendarTimeZoneResolver`), calcula taxa de adesão dos hábitos dentro de `#### Cronograma` ignorando linhas `<!-- cal-id:` (tratando `0/0` como `0%` sem divisão por zero) e contagens dos boards `Profissional.md` e `Pessoal.md`; faz upsert idempotente de uma única linha marcada com `<!-- routine-id:nightly-closure -->` sob `#### Reflexões do Dia` (`> **Reflexões:**`), criando a seção se ausente e preservando 100% das anotações manuais em `#### Diário`, `#### Reflexões do Dia`, `> **Agradecimentos:**`, `#### Cronograma` e `#### Anotações`.
  - **Rotina 3 (`MorningPrepJob` — 06:00):** cria `daily/YYYY-MM-DD.md` a partir de `templates/Notas Diárias.md` (garantida de modo não destrutivo por `VaultInitializer.EnsureInitializedAsync`), normalizando `- [x]` para `- [ ]` em `#### Cronograma` somente na criação da nova nota diária (preservando `- [x]` caso `daily/YYYY-MM-DD.md` já exista); lê tarefas pendentes (`## To Do` e `## Doing`, ignorando `## Done` e `## Blocked`) de `assets/kanban/Profissional.md` e `assets/kanban/Pessoal.md` e faz upsert idempotente de linhas marcadas `<!-- routine-id:morning-prep -->` sob `#### Tarefas` (criando `#### Tarefas` se ausente), preservando `- [[Profissional]]`, `- [[Pessoal]]` e tarefas manuais.
  - Consistência de contagem de inbox em `ObsidianVaultService.GetOverviewAsync`: contabilizar `*.md` e `*.txt` (`SearchOption.TopDirectoryOnly`) em `inbox/`.
  - Serialização via `SemaphoreSlim(1, 1)` e persistência atômica (`.tmp` + `File.Move(..., overwrite: true)`) do registro de auditoria em `inbox/_archive/routines-audit-log.json` sob `VaultPathGuard`, recuperando com segurança se o JSON estiver ausente ou corrompido.
- Endpoints REST protegidos (`.RequireAuthorization()`, JWT Bearer): `GET /api/routines/overview`, `POST /api/routines/triage` e `POST /api/routines/{jobId}/run`, mais integração em `AgentOrchestratorService` emitindo `routines-panel`.
- Frontend Angular (`styles.scss`, `granbrain.models.ts`, `granbrain-api.service.ts`, `AppComponent`, `GenerativeCanvasComponent`, `app.spec.ts`) com navegação pelo rail `0005` e suporte em blocos do chat generativo.

**Fora:**

- Exclusão irreversível de arquivos no disco (todo original processado vai exclusivamente para `inbox/_archive/`).
- Criação de painel cron arbitrário pelo usuário na UI ou alterações em infraestrutura Docker/CI-CD.
- Qualquer alteração fora da raiz do vault (`VaultPathGuard`) ou introdução de tokens CSS fora do Balde A.

## Critérios de aceite

1. **AC1 — Triagem Inteligente e Não Destrutiva do Inbox (`InboxTriageJob`):**
   - `InboxTriageJob` processa arquivos `*.md` e `*.txt` diretamente em `inbox/` (`SearchOption.TopDirectoryOnly`, ignorando subpastas como `inbox/_archive/` e arquivos `.json`).
   - Notas de reunião/ata geram arquivo em `zettelkasten/Reunião - <SafeTitle>.md` e notas conceituais/estudos/capturas geram `zettelkasten/<SafeTitle>.md`, sempre com um único bloco YAML frontmatter (`type`, `created`, `source_inbox`, `tags`, preservando `#mobile-capture` quando aplicável) e `[[Fleeting]]` + `[[WikiLinks]]` apontando para índices reais em `zettelkasten/Índices/` (mesmo quando o arquivo bruto já possuir cabeçalhos `---` duplicados/parciais).
   - Se o caminho alvo em `zettelkasten/` já existir, o job nunca sobrescreve a nota existente: utiliza sufixo colisão-seguro (`zettelkasten/<SafeTitle> - 2.md`, etc.).
   - Quando `RoutinesOptions.UseCli == true`, consulta `ICliProcessRunner` esperando saída JSON estruturada; em erro, timeout ou JSON inválido, faz fallback automático para a classificação heurística determinística sem perder o arquivo.
   - O arquivo bruto original é movido intacto para `inbox/_archive/` (com sufixo colisão-seguro se já existir no arquivo morto), nunca deletado. `ObsidianVaultService.GetOverviewAsync` e `RoutineExecutionService.GetOverviewAsync` concordam na contagem de `*.md` e `*.txt` pendentes em `inbox/`.
2. **AC2 — Fechamento Noturno Idempotente (`NightlyClosureJob`):**
   - Calcula adesão aos hábitos estritamente dentro de `#### Cronograma` ignorando linhas `<!-- cal-id:` (tratando `0/0` como `0%` sem divisão por zero) e contabiliza tarefas de `Profissional.md` e `Pessoal.md`.
   - Faz upsert de exatamente uma linha marcada `<!-- routine-id:nightly-closure -->` em `#### Reflexões do Dia` (abaixo de `> **Reflexões:**`, criando a seção se ausente); reexecutar após marcar mais um hábito atualiza as métricas na mesma linha sem duplicar entradas nem apagar conteúdo manual em `#### Diário`, `#### Reflexões do Dia`, `> **Agradecimentos:**`, `#### Cronograma` e `#### Anotações`.
3. **AC3 — Preparação Matinal Idempotente (`MorningPrepJob`):**
   - `VaultInitializer` garante `templates/Notas Diárias.md` de modo não destrutivo quando ausente; ao criar uma nota inexistente em `daily/YYYY-MM-DD.md`, `MorningPrepJob` expande o template e normaliza qualquer `- [x]` em `#### Cronograma` para `- [ ]`.
   - Se `daily/YYYY-MM-DD.md` já existir, preserva integralmente hábitos já marcados `[x]`. Em ambos os casos, puxa tarefas pendentes (`## To Do` e `## Doing`, excluindo `## Done` e `## Blocked`) de `Profissional.md` e `Pessoal.md` e faz upsert idempotente marcado `<!-- routine-id:morning-prep -->` sob `#### Tarefas`, mantendo `- [[Profissional]]`, `- [[Pessoal]]` e alterações subsequentes do Kanban sincronizadas em reexecuções.
4. **AC4 — Auditoria, Isolamento de Falha, Concorrência, Scheduler, Vault Jail e API JWT (`/api/routines/*`):**
   - Toda leitura/escrita valida caminhos brutos em `VaultPathGuard.ResolveSafePath` antes de sanitizar nomes de arquivo; tentativa de path traversal (`..` ou fora do vault) lança `VaultPathViolationException` mapeada em HTTP `400 Bad Request` (`vault_path_violation`).
   - Falha de I/O em um arquivo corrompido durante a triagem é isolada (`FailedCount++` + log de erro) sem abortar os arquivos saudáveis subsequentes; execuções concorrentes são serializadas por `SemaphoreSlim(1, 1)` e persistem o log atomicamente em `inbox/_archive/routines-audit-log.json` (tolerando arquivo ausente ou JSON corrompido).
   - `RoutineSchedulerHostedService` respeita `RoutinesOptions.EnableBackgroundScheduler` (sem disparos quando `false`) e executa `MorningPrepJob`, `InboxTriageJob` e `NightlyClosureJob` nos horários/intervalos configurados via `CalendarTimeZoneResolver` sem duplicar no mesmo dia/janela.
   - Endpoints `GET /api/routines/overview`, `POST /api/routines/triage` e `POST /api/routines/{jobId}/run` exigem JWT Bearer (`.RequireAuthorization()` -> `401 Unauthorized` sem token), aceitam corpo vazio opcional, retornam `400 invalid_request_body` para JSON inválido, `400 invalid_routine_date` para `date` inválida e `404 routine_job_not_found` para `jobId` desconhecido.
5. **AC5 — Orquestrador e Painel de Rotinas no Frontend Conforme Mockup (`/spec-frontend`):**
   - `AgentOrchestratorService` reconhece intenções de rotinas/triagem/inbox e retorna bloco `GenerativeUiBlock` com `type = "routines-panel"` (`GenerativeUiBlockTypes.RoutinesPanel`) e `routinesOverview` populado.
   - O item `0005` (`Rotinas & Inbox`) no rail de `AppComponent` navega para `currentView = 'rotinas'` (`.nav-item.on`), atualiza `.topbar .crumb` para `Rotinas & Inbox`, chama `GET /api/routines/overview` via `GranBrainApiService` e renderiza os 3 `.card.routine`, `.pill.warn` `Pendente` / `.pill.ok` `Em dia`, itens `.row`, estado `.empty` (`"Inbox vazio. Tudo foi triado."`) e histórico `.logrow`.
   - O botão `[data-routine-triage]` exibe `Triando…` com `disabled` enquanto `POST /api/routines/triage` está em voo, e após concluir atualiza para `Em dia`, exibe `.empty`, insere novo `.logrow` e dispara toast. `app.spec.ts` valida contrato camelCase C#/TS e `getComputedStyle` de `.routine`, `.logrow`, `.row`, `.empty` e `.code` contra o Balde A de `styles.scss`.

## Área de impacto esperada

- `backend/src/GranBrain.Core/Configuration/RoutinesOptions.cs`
- `backend/src/GranBrain.Core/Interfaces/IRoutineExecutionService.cs`
- `backend/src/GranBrain.Core/Models/RoutineModels.cs`
- `backend/src/GranBrain.Core/Models/GenerativeUiModels.cs`
- `backend/src/GranBrain.Infrastructure/Jobs/RoutineExecutionService.cs`
- `backend/src/GranBrain.Infrastructure/Jobs/RoutineSchedulerHostedService.cs`
- `backend/src/GranBrain.Infrastructure/Vault/ObsidianVaultService.cs`
- `backend/src/GranBrain.Infrastructure/Vault/VaultInitializer.cs`
- `backend/src/GranBrain.Infrastructure/Agents/AgentOrchestratorService.cs`
- `backend/src/GranBrain.Api/Endpoints/RoutinesEndpoints.cs`
- `backend/src/GranBrain.Api/Program.cs`
- `backend/src/GranBrain.Api/appsettings.json`
- `backend/tests/GranBrain.UnitTests/RoutineExecutionServiceTests.cs`
- `backend/tests/GranBrain.UnitTests/VaultInitializerTests.cs`
- `backend/tests/GranBrain.UnitTests/ObsidianVaultServiceTests.cs`
- `backend/tests/GranBrain.UnitTests/ApiSecurityAndAgentTests.cs`
- `frontend/src/styles.scss`
- `frontend/src/app/models/granbrain.models.ts`
- `frontend/src/app/services/granbrain-api.service.ts`
- `frontend/src/app/components/generative-canvas/generative-canvas.component.ts`
- `frontend/src/app/components/generative-canvas/generative-canvas.component.html`
- `frontend/src/app/app.component.ts`
- `frontend/src/app/app.component.html`
- `frontend/src/app/app.spec.ts`

## Referências de arquitetura

- [CLAUDE.md](../../CLAUDE.md)
- [ADR-0001 — GranBrain Architecture](../../docs/adr/0001-granbrain-architecture.md)
- [Import Local do Mockup](../../frontend/_imports/claude-design/granbrain-redesign/2026-10-30/original/granbrain-redesign.html)
- [Artefato Claude Design](https://claude.ai/artifact/1puqYFtE3guy1J6CyvDXHy)
- [Task 0003: Agenda & Sincronia](./0003-active-calendar-sync-google-microsoft.md)

## Decisões do dev

| ID | Decisão | Alternativas consideradas | Impacto |
| :--- | :--- | :--- | :--- |
| D1 | Triagem não destrutiva `inbox/` -> `zettelkasten/` (`Reunião - <SafeTitle>.md` ou `<SafeTitle>.md`) com classificação heurística default, CLI opcional (`UseCli`), proteção contra sobrescrita e arquivamento em `inbox/_archive/` | Criar subpasta `reunioes/` fora de `zettelkasten/`; sobrescrever notas existentes; apagar arquivos do inbox | Garante conformidade com o padrão real do vault (`zettelkasten/Reunião - ...`), evita custos/latência obrigatória de LLM em testes e protege contra perda de dados |
| D2 | Idempotência em `daily/YYYY-MM-DD.md` via marcadores `<!-- routine-id:morning-prep -->` e `<!-- routine-id:nightly-closure -->` nas seções reais de `templates/Notas Diárias.md` | Sobrescrever `daily/YYYY-MM-DD.md`; inventar seções fora do template; ignorar arquivos sem `#### Reflexões do Dia` | Mantém hábitos, diário, agradecimentos e eventos de calendário 100% intactos enquanto atualiza métricas e prioridades em reexecuções |
| D3 | Serialização via `SemaphoreSlim(1, 1)`, isolamento de falha por arquivo, auditoria atômica em `inbox/_archive/routines-audit-log.json`, `RoutineSchedulerHostedService` configurável e JWT Bearer em `/api/routines/*` | Gravar log em JSON fora do cofre; deixar timer rodando sem chave de opt-out em testes; falhar toda triagem por 1 arquivo ilegível | Elimina condições de corrida e interferência do agendador em `WebApplicationFactory<Program>`, além de padronizar autenticação JWT |
| D4 | Reconciliação visual `/spec-frontend` 100% Balde A em `styles.scss` + conexão do rail `0005` em `AppComponent` e bloco `routines-panel` no `GenerativeCanvasComponent` | Criar novos tokens `:root`; renderizar rotinas fora do padrão de blocos do canvas; deixar rail `0005` como toast futuro | Entrega fidelidade exata ao mockup nos modos claro/escuro e desktop/mobile com foco em testes DOM + `getComputedStyle` |

### D1 — Destino no Zettelkasten, Motor de Classificação e Arquivamento no `InboxTriageJob`
- **Pergunta:** Como o `InboxTriageJob` deve classificar e rotear arquivos brutos (`*.md` e `*.txt`) do `inbox/` para notas permanentes e arquivar os originais sem violar o cofre do usuário?
- **Opções consideradas:**
  1. Roteamento determinístico estritamente para `zettelkasten/Reunião - <SafeTitle>.md` (reuniões/atas) e `zettelkasten/<SafeTitle>.md` (conceitos, literatura, ideias, `#mobile-capture`) com YAML frontmatter único (`type`, `created`, `source_inbox`, `tags`) + `[[Fleeting]]` + `[[WikiLinks]]` para MOCs reais de `zettelkasten/Índices/`, sem sobrescrever notas existentes (sufixo colisão-seguro), usando classificação heurística por padrão e `ICliProcessRunner` opcional (`UseCli = true`) com fallback automático para heurística, e movendo o original intacto para `inbox/_archive/`.
  2. Criar nova pasta raiz `reunioes/` separada de `zettelkasten/` e sobrescrever notas com mesmo título.
  3. Tornar chamada externa via CLI obrigatória em toda execução de triagem.
- **Decisão:** Opção 1 — Tudo nasce dentro de `zettelkasten/` seguindo a convenção da WI 0003 (`zettelkasten/Reunião - <SafeTitle>.md` e `zettelkasten/<SafeTitle>.md`), nunca sobrescrevendo notas existentes nem apagando originais (`inbox/_archive/` com sufixo colisão-seguro), com heurística determinística como padrão e `UseCli` opcional com fallback seguro. Além disso, `ObsidianVaultService.GetOverviewAsync` passa a contar `*.md` e `*.txt` (`SearchOption.TopDirectoryOnly`) em `inbox/` para manter paridade com a fila do painel de rotinas.
- **Justificativa:** Segue o layout real do Obsidian do Igor (confirmado também em WI 0003), preserva 100% do histórico bruto e mantém a triagem rápida, determinística e testável sem depender de binários externos.

### D2 — Marcadores Idempotentes de `MorningPrepJob` e `NightlyClosureJob` na Nota Diária
- **Pergunta:** Como atualizar a Nota Diária (`daily/YYYY-MM-DD.md`) às `06:00` e às `23:30` de forma repetível sem duplicar blocos nem apagar registros pessoais?
- **Opções consideradas:**
  1. Utilizar os marcadores HTML `<!-- routine-id:morning-prep -->` em `#### Tarefas` e `<!-- routine-id:nightly-closure -->` em `#### Reflexões do Dia` (logo após `> **Reflexões:**`), alinhados à estrutura real de `templates/Notas Diárias.md`. Na criação de uma nova nota diária a partir do template, `MorningPrepJob` normaliza `- [x]` para `- [ ]` em `#### Cronograma`; em nota já existente, preserva `- [x]` e atualiza apenas as linhas marcadas. `NightlyClosureJob` calcula hábitos em `#### Cronograma` ignorando `<!-- cal-id:` e divide com guarda `0/0 -> 0%`. Ambas as rotinas criam a seção alvo se estiver ausente em notas mínimas.
  2. Reescrever toda a seção `#### Cronograma` ou `#### Reflexões do Dia` em cada execução.
  3. Ignorar atualização caso já exista qualquer texto na seção.
- **Decisão:** Opção 1 — Upsert idempotente por marcador `<!-- routine-id:... -->` respeitando `#### Cronograma` (sem `<!-- cal-id:`), `#### Tarefas` (`- [[Profissional]]`, `- [[Pessoal]]`) e `#### Reflexões do Dia` (`> **Reflexões:**`), com preservação integral do conteúdo escrito pelo usuário.
- **Justificativa:** Garante que reexecuções no mesmo dia atualizem métricas de adesão e tarefas do Kanban sem corromper hábitos já concluídos nem diários/agradecimentos pessoais.

### D3 — Isolamento de Falhas, Auditoria Atômica, Agendador e Segurança HTTP
- **Pergunta:** Como garantir resiliência contra arquivos corrompidos, condições de corrida, interferência de timers em testes de integração e segurança das rotas HTTP?
- **Opções consideradas:**
  1. Serializar execuções em `RoutineExecutionService` via `SemaphoreSlim(1, 1)`; isolar exceções de I/O por arquivo no `InboxTriageJob` (`FailedCount++` + log) propagando apenas `VaultPathViolationException`; persistir logs atomicamente (`.tmp` + `File.Move(..., overwrite: true)`) em `inbox/_archive/routines-audit-log.json` tolerando ausência ou JSON corrompido; expor `RoutinesOptions.EnableBackgroundScheduler` no `RoutineSchedulerHostedService`; e proteger `/api/routines/*` com `.RequireAuthorization()` (JWT Bearer).
  2. Gravar logs fora da pasta do vault em diretório temporário do SO e deixar `BackgroundService` sempre ativo sem flag de desativação.
- **Decisão:** Opção 1 — Controle concorrente por `SemaphoreSlim`, escrita atômica auditável dentro de `_archive/` sob `VaultPathGuard`, flag explícita `EnableBackgroundScheduler` (ligada no `appsettings.json` e desligada no `CreateTestFactory` dos testes HTTP), isolamento por arquivo e endpoints JWT Bearer consistentes com `CalendarEndpoints` e `VaultEndpoints`.
- **Justificativa:** Evita corrupção de logs sob chamadas simultâneas, protege contra path traversal e garante estabilidade determinística das suítes `WebApplicationFactory<Program>`.

### D4 — Escopo Visual `/spec-frontend` (100% Balde A) e Integração Angular (`rail 0005` + `routines-panel`)
- **Pergunta:** Quais classes CSS, fluxos de interação e pontos de acoplamento do mockup `granbrain-redesign.html` compõem a entrega da interface de Rotinas?
- **Opções consideradas:**
  1. Adicionar exatamente as classes faltantes do mockup em `frontend/src/styles.scss` usando apenas tokens `:root` existentes (Balde A): `.routine`, `.routine p`, `.row`, `.row:last-child`, `button.row`, `button.row:last-child`, `button.row[aria-pressed="true"] strong`, `.empty`, `.code`, `.logrow`, `.logrow:last-child`, `.logrow p` e regra mobile em `@media (max-width: 640px)`; ligar o rail `0005` (`Rotinas & Inbox`) no `AppComponent` carregando `GET /api/routines/overview` via `GranBrainApiService`; e renderizar o bloco `routines-panel` no `GenerativeCanvasComponent` com estado in-flight (`disabled` + `Triando…`), feedback em toast e ações individuais `[data-routine-run]`.
  2. Construir estilos inline customizados no template Angular sem reconciliar `.routine`/`.logrow`/`.row` em `styles.scss`.
- **Decisão:** Opção 1 — Reconciliação total via classes globais Balde A em `styles.scss` combinada à renderização generativa (`routines-panel`) e navegação completa no `rail 0005`.
- **Justificativa:** Mantém o `GenerativeCanvasComponent` fiel pixel a pixel ao mockup nos modos claro e escuro e permite reutilização direta tanto pela aba dedicada quanto pelas respostas do chat do agente.
