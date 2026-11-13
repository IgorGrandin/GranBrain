# 0003 — Sincronização Ativa de Calendários (Google + Microsoft Graph / Outlook & Teams)

## Objetivo

Integrar calendários reais (Google Calendar e Microsoft Graph cobrindo Outlook e Microsoft Teams) ao GranBrain com sincronização idempotente para a Nota Diária (`daily/YYYY-MM-DD.md`), geração segura de notas de reunião no vault usando o template oficial (`templates/Reuniões.md`) sob `VaultPathGuard`, suporte no `AgentOrchestratorService` para perguntas e ações de agenda, e renderização fiel do bloco generativo `agenda-timeline` no Angular com alternância Lista / Calendário, filtros por provedor, linha de hora atual (`nowline`/`cal-now`), detecção de conflitos e links diretos de reunião/nota.

## Contexto

O vault real do Igor já organiza compromissos em `daily/YYYY-MM-DD.md` dentro de `#### Cronograma` e `## 🤝🏻 Reuniões` (com bloco `dataview`), e utiliza `templates/Reuniões.md` para atas em `zettelkasten/Reunião - <Título>.md`. No protótipo canônico aprovado ([granbrain-redesign.html](../../frontend/_imports/claude-design/granbrain-redesign/2026-10-30/original/granbrain-redesign.html)), a aba `Agenda & Sincronia` (rail `0003`) já define toda a experiência visual (`Agenda de hoje · 5 eventos`, pills de provedor, alerta `.conflict`, `.timeline` em lista e `.calwrap`/`.cal-grid` em grade horária). Atualmente no app Angular ([app.component.html](../../frontend/src/app/app.component.html#L114-L118)), clicar no rail `0003` dispara apenas o toast `"Módulo de Agenda (0003) em breve"`.

#### Reconciliação com o design-system — 2026-11-13

Comparação entre [granbrain-redesign.html](../../frontend/_imports/claude-design/granbrain-redesign/2026-10-30/original/granbrain-redesign.html) (Export: `2026-10-30`, commit base `818dc09250c7af15da922c6f709a6fc30f5cee8b`) e [styles.scss](../../frontend/src/styles.scss):

| Classe / Padrão no Mockup | Local no Mockup | Status em `styles.scss` | Bucket | Token / Valor Reconciliado |
| :--- | :--- | :--- | :--- | :--- |
| `.seg`, `.seg button`, `[aria-pressed="true"]` | `granbrain-redesign.html:229-237` | Ausente (estilo novo de componente usando tokens existentes) | A | `--surface-2`, `--line`, `--radius-pill`, `--surface`, `--text`, `--text-2`, `--font-sans`, `500` |
| `.chips`, `.chip`, `.chip[aria-pressed="true"]`, `.dot` | `granbrain-redesign.html:214-228` | Já implementado em `styles.scss:204-211` | A | `--surface-2`, `--line`, `--text`, `--text-2`, `--radius-pill` |
| `.conflict`, `.conflict .ico` | `granbrain-redesign.html:359-360` | Ausente (usar alerta de conflito no padrão `.toast.warn`) | A | `--warn-soft`, `--warn`, `--radius-md` |
| `.timeline`, `.ev`, `.ev.now`, `.ev.past`, `.ev .time`, `.ev .dot` | `granbrain-redesign.html:361-370` | Ausente | A | `--line-strong`, `--line`, `--accent`, `--accent-soft`, `--surface`, `--text-2`, `--text-3`, `--font-mono` |
| `.ev-card`, `.ev-top`, `.ev-title`, `.ev-meta`, `.link` | `granbrain-redesign.html:371-376` | Ausente | A | `--surface`, `--surface-2`, `--line`, `--line-strong`, `--radius-lg`, `--accent`, `--accent-soft`, `--text-2` |
| `.nowline` | `granbrain-redesign.html:377-379` | Ausente | A | `--accent`, `--font-mono`, `--accent-soft` (`padding: 2px 10px`, `border-radius: 999px`) |
| `.calwrap`, `.cal-grid`, `.cal-line`, `.cal-hour`, `.cal-now` | `granbrain-redesign.html:380-385, 391` | Ausente | A | `--surface`, `--line`, `--text-3`, `--accent`, `--font-mono`, `--radius-lg` (`grid-template-columns: 56px 1fr`) |
| `.cal-ev`, `.p-google`, `.p-teams`, `.p-outlook`, `.clash` | `granbrain-redesign.html:386-390` | Ausente | A | `--accent`, `--accent-soft`, `--ok`, `--ok-soft`, `--warn`, `--warn-soft`, `--radius-md` |

## Escopo

### Especificação visual

Fonte visual obrigatória: [granbrain-redesign.html](../../frontend/_imports/claude-design/granbrain-redesign/2026-10-30/original/granbrain-redesign.html) (`:229-237`, `:359-391`, `:703-709`, `:853`, `:865-869`, `:893`, `:1206-1274`). Todos os estilos pertencem ao **Bucket A** (tokens `:root` já existentes em `frontend/src/styles.scss` sem alteração de variáveis globais).

- **Cabeçalho da agenda (`agHeader`)**:
  - Título `Agenda` acompanhado de segmented toggle `.seg` (`display: inline-flex; padding: 3px; border: 1px solid var(--line-strong); border-radius: 999px; background: var(--surface)`) com botões `Lista` e `Calendário` (`min-height: 30px; border: 0; background: transparent; border-radius: 999px; padding: 0 10px; font-size: 13px; color: var(--fg-2)`; ativo `[aria-pressed="true"]` com `background: var(--fg); color: var(--bg)`).
  - Grupo `.chips` de filtros por provedor (`Todos`, `Google`, `Teams`, `Outlook`) usando os estilos existentes de `.chips`/`.chip` com `[aria-pressed="true"]`.
- **Banner de conflito (`.conflict`)**:
  - Renderizado quando `conflictsCount > 0` (`display: flex; gap: 10px; align-items: flex-start; padding: 12px 14px; border-radius: 12px; background: var(--warn-soft); color: var(--fg); font-size: 14px; margin-top: 16px`), com ícone `.conflict .ico` (`color: var(--warn); margin-top: 2px`).
- **Modo Lista (`.timeline`)**:
  - Container `.timeline` (`display: grid; gap: 10px; margin-top: 16px`).
  - Marcador `.nowline` inserido imediatamente antes do primeiro evento futuro (`display: grid; grid-template-columns: 56px 14px minmax(0, 1fr); gap: 10px; align-items: center`, linha `var(--danger)` e texto `Agora · HH:mm` em `var(--danger)`).
  - Cada item `.ev` usa grid `display: grid; grid-template-columns: 56px 14px minmax(0, 1fr); align-items: start; gap: 10px`:
    - Dot marcador `.ev .dot` (`width: 10px; height: 10px; border-radius: 50%; background: var(--line-strong); margin-top: 18px; justify-self: center`).
    - Em `.ev.now .dot`: `background: var(--ok); box-shadow: 0 0 0 4px var(--ok-soft)` e `.ev.now .ev-card`: `border-color: var(--ok)`.
    - Em `.ev.past`: `opacity: 0.55`.
    - Coluna `time`: `padding-top: 16px; color: var(--fg-2)` com classe `.mono`.
    - Cartão `.ev-card`: `padding: 14px 16px; display: grid; gap: 8px` sobre `.card`.
    - Cabeçalho `.ev-top`: `display: flex; justify-content: space-between; align-items: flex-start; gap: 10px; flex-wrap: wrap` com título em `<strong>` e pill de status (`Passou`, `Em andamento`, `Próximo`).
    - Rodapé `.ev-meta`: `display: flex; gap: 10px; align-items: center; flex-wrap: wrap`, horário `s–e`, pill de provedor (`Google` `.info`, `Teams` `.accent`, `Outlook` `.warn`), local/participantes e links `.link` (`[data-event-join]` quando houver `JoinUrl` e `[data-event-note]` para abrir/gerar a nota da reunião).
- **Modo Calendário (`.calwrap`)**:
  - Grade de 08:00 às 18:00 (`10h`, `600` min) em altura proporcional (`height: 600px`, escala `60px/h`, container `.calwrap` com `padding: 16px 14px 16px 0; overflow-x: auto; margin-top: 16px`), com `.cal-grid` (`position: relative; margin-left: 60px; min-width: 300px`).
  - Linhas horárias `.cal-line` (`border-top: 1px solid var(--line)`) e rótulos `.cal-hour` (`position: absolute; left: -60px; width: 50px; text-align: right; font-size: 12px; color: var(--fg-3)`).
  - Linha de hora atual `.cal-now` posicionada verticalmente por minuto (`border-top: 2px solid var(--danger)`) com rótulo `HH:mm` em `var(--danger)`.
  - Blocos `.cal-ev` posicionados proporcionalmente por horário e coluna (quando há colisão, split em largura com recuo horizontal entre colunas):
    - `.cal-ev.p-google`: `background: var(--info-soft); color: var(--info)`
    - `.cal-ev.p-teams`: `background: var(--accent-soft); color: var(--accent)`
    - `.cal-ev.p-outlook`: `background: var(--warn-soft); color: var(--warn)`
    - `.cal-ev.clash`: `border: 1.5px dashed var(--danger)`

**Dentro:**
- Modelos, interfaces e opções de configuração no backend (`GranBrain.Core`) para eventos normalizados (`CalendarProvider`, `CalendarEventDto`, `CalendarTodayDto`, `CalendarSyncResultDto`, `CalendarNoteResultDto`, `CalendarOptions`).
- Conectores reais em `GranBrain.Infrastructure` para Google Calendar e Microsoft Graph (`/me/calendar/events`), com renovação automática de access token em memória via OAuth2 `RefreshToken`, fallback determinístico de 5 eventos quando credenciais estiverem ausentes, e parsing/mapeamento de timezone (`America/Sao_Paulo` no Linux/macOS / `E. South America Standard Time` no Windows) respeitando a data local do evento na janela UTC.
- Sincronização idempotente para `daily/YYYY-MM-DD.md` preservando hábitos existentes em `#### Cronograma`, seção `## 🤝🏻 Reuniões` e blocos `dataview` (além de fallback seguro caso a nota diária não contenha `## 🤝🏻 Reuniões`), e criação/atualização segura de notas em `zettelkasten/Reunião - <Título>.md` com `templates/Reuniões.md`.
- Endpoints REST protegidos (`GET /api/calendar/today`, `POST /api/calendar/sync`, `POST /api/calendar/events/{id}/note`) e integração ao `AgentOrchestratorService` emitindo bloco `GenerativeUiBlockTypes.AgendaTimeline` (`agenda-timeline`).
- UI Angular (`GranBrainApiService`, `GenerativeCanvasComponent`, `AppComponent`, `styles.scss`) conectando o rail `0003` (`Agenda & Sincronia`) e renderizando `agenda-timeline` nos modos Lista e Calendário.

**Fora:**
- Escrita bidirecional / criação ou edição de eventos remotos no Google Calendar ou Microsoft Graph (OAuth apenas leitura de eventos neste WI).
- Fluxo interativo de browser OAuth consent screen ou persistência de tokens/credenciais em disco ou no vault.
- Sincronização em background contínua sem gatilho manual ou comando no chat/API.

## Critérios de aceite

- **AC1 (Conectores Google + Microsoft Graph, renovação in-memory e segurança de credenciais):**
  - `GET /api/calendar/today` e `POST /api/calendar/sync` exigem autenticação JWT Bearer (`.RequireAuthorization()`, retornando `401 Unauthorized` sem token válido, igual a `VaultEndpoints` e `AgentEndpoints`).
  - Quando `ClientId`/`ClientSecret`/`RefreshToken` não estão configurados no provedor, os conectores não quebram e retornam o conjunto determinístico de eventos de fallback (5 eventos compatíveis com o mockup, cobrindo `Google`, `Teams` e `Outlook` com 1 par conflitante e 1 par adjacente sem falso positivo de conflito).
  - Quando credenciais OAuth estão configuradas, cada conector renova o access token apenas em memória (`tokenUri` configurado), sem persistir tokens ou segredos em `appsettings.json`, no vault (`tempVault`) ou em disco; falha OAuth `401 Unauthorized` ou configuração parcial (`ClientId` sem `RefreshToken`) retorna `VaultValidationException("calendar_auth_failed", ...)` mapeado em HTTP `400 Bad Request`.
  - Conversão de timestamps UTC (`2026-10-30T13:30:00Z`) e filtragem por data consideram o fuso configurado (`America/Sao_Paulo` / fallback Windows `E. South America Standard Time`), classificando corretamente eventos próximos da meia-noite UTC no dia local correto.
  - Eventos adjacentes sem sobreposição real (`EndTime == next.StartTime`, ex.: `09:00–09:30` e `09:30–10:30`) não recebem `HasConflict = true`; apenas interseção estrita (`a.Start < b.End && b.Start < a.End`) marca conflito.
- **AC2 (Sincronização idempotente na Nota Diária sem quebrar hábitos):**
  - `POST /api/calendar/sync` insere/atualiza itens do calendário de forma aditiva em `daily/YYYY-MM-DD.md` com marcador `<!-- cal-id:<id> -->` (sem checkbox `[ ]`) em ordem cronológica na seção `#### Cronograma` e referencia a reunião em `## 🤝🏻 Reuniões` antes do bloco ```` ```dataview ```` (ou anexa a seção `## 🤝🏻 Reuniões` ao final do documento caso ela ainda não exista).
  - Reexecutar `SyncAsync` duas vezes no mesmo dia não duplica linhas e preserva integralmente anotações livres do usuário e o estado (`[ ]`/`[x]`) de todos os hábitos existentes.
  - `ObsidianVaultService.ParseHabitsFromLines` e `ToggleHabitAsync` ignoram qualquer linha com `<!-- cal-id:` e operam estritamente dentro de `#### Cronograma` (corrigindo inclusive a leitura indevida fora da seção quando `habits.Count == 0`); tentar alternar o título de um evento sincronizado como hábito lança `VaultEntityNotFoundException("habit_not_found", ...)`.
- **AC3 (Template e geração/preservação de notas de reunião no vault):**
  - `VaultInitializer` garante de forma não destrutiva a existência de `templates/Reuniões.md` quando ausente (sem sobrescrever arquivo já existente no vault).
  - `POST /api/calendar/events/{id}/note` cria `zettelkasten/Reunião - <TítuloSanitizado>.md` preenchido com dados do evento (data, horário, provedor, link, participantes); se a nota já existir, atualiza apenas o cabeçalho/metadados e preserva todo o conteúdo manual sob `## 📝 Discussão & Anotações` e `## ✅ Plano de Ação & Tarefas`.
  - Evento inexistente retorna `VaultEntityNotFoundException("calendar_event_not_found", ...)` mapeado em HTTP `404 Not Found`.
- **AC4 (Vault Jail e validação de entrada nos endpoints de agenda):**
  - Toda leitura/escrita de notas de reunião e notas diárias passa por `VaultPathGuard.ResolveSafePath`; tentativa de path traversal no parâmetro `TargetFolder` (validado em seu valor bruto antes de qualquer sanitização de nome de arquivo) ou nome de arquivo malicioso dispara `VaultPathViolationException` mapeado em HTTP `400 Bad Request`.
  - Parâmetro `date` inválido em `/api/calendar/today` ou `/api/calendar/sync` retorna `VaultValidationException("invalid_calendar_date", ...)` mapeado em HTTP `400 Bad Request`.
- **AC5 (Orquestrador, bloco `agenda-timeline` e fidelidade visual Lista/Calendário no Angular):**
  - `AgentOrchestratorService` reconhece intenções de agenda (`agenda`, `reunião`, `calendário`, `sincronizar`, `conflito`, `hoje`) e emite um `GenerativeUiBlock` com `Type = "agenda-timeline"` (`GenerativeUiBlockTypes.AgendaTimeline`) preenchido com os dados de `CalendarTodayDto`.
  - Na UI Angular (`AppComponent` + `GenerativeCanvasComponent`), o item de navegação `0003` (`Agenda & Sincronia`) aciona o carregamento da agenda real via `GranBrainApiService.getCalendarToday()`, exibindo estados `.ev.past`, `.ev.now`, `.ev.next`, `.nowline`, `.cal-now`, botões `[data-event-join]` e `[data-event-note]` (com feedback de nota gerada/aberta), alerta `.conflict` quando houver conflitos, alternância `.seg` (`Lista` <-> `Calendário`) e filtros `.chips` por provedor atualizando `[aria-pressed="true"]` e os elementos `.ev`/`.cal-ev` visíveis no DOM.
  - Contrato DTO (`CalendarTodayDto` <-> `CalendarToday`) alinhado em nomes de propriedades camelCase no JSON, e estilos críticos do mockup `granbrain-redesign.html` (`.ev` em grid `56px 14px minmax(0, 1fr)`, `.ev.now .dot` com `var(--ok)`, `.cal-ev.p-google`, `.cal-ev.p-teams`, `.cal-ev.p-outlook`, `.cal-ev.clash` com borda `dashed` em `var(--danger)` e `.conflict`) verificados em `app.spec.ts` com `getComputedStyle`.

## Área de impacto esperada

- `backend/src/GranBrain.Core/Configuration/`
- `backend/src/GranBrain.Core/Interfaces/`
- `backend/src/GranBrain.Core/Models/`
- `backend/src/GranBrain.Infrastructure/Calendar/`
- `backend/src/GranBrain.Infrastructure/Vault/`
- `backend/src/GranBrain.Infrastructure/Agents/`
- `backend/src/GranBrain.Api/Endpoints/`
- `backend/src/GranBrain.Api/Program.cs`
- `backend/src/GranBrain.Api/appsettings.json`
- `backend/tests/GranBrain.UnitTests/`
- `frontend/src/styles.scss`
- `frontend/src/app/models/`
- `frontend/src/app/services/`
- `frontend/src/app/components/generative-canvas/`
- `frontend/src/app/app.component.ts` e `app.component.html`
- `frontend/src/app/app.spec.ts`

## Referências de arquitetura

- [CLAUDE.md](../../CLAUDE.md)
- [ADR-0001 — GranBrain Architecture](../../docs/adr/0001-granbrain-architecture.md)
- [granbrain-redesign.html](../../frontend/_imports/claude-design/granbrain-redesign/2026-10-30/original/granbrain-redesign.html)
- Template do vault real: `templates/Reuniões.md` e `daily/2026-11-13.md`

## Decisões do dev

| ID | Decisão | Alternativas consideradas | Impacto |
| :--- | :--- | :--- | :--- |
| D1 | Conectores `GoogleCalendarConnector` + `MicrosoftGraphCalendarConnector` unificado com fallback determinístico e refresh em memória | Conectores separados Outlook vs Teams; persistência de tokens OAuth no disco/vault | Mantém zero segredos em disco e classifica Teams vs Outlook via `isOnlineMeeting`/`teams.microsoft.com` |
| D2 | Marcador idempotente `<!-- cal-id:<id> -->` em lista sem checkbox dentro de `#### Cronograma` + `## 🤝🏻 Reuniões`, protegendo parser de hábitos | Usar checkboxes `[ ]` para reuniões; sobrescrever `#### Cronograma` inteiro | Preserva 100% dos hábitos e anotações manuais em re-syncs |
| D3 | `VaultInitializer` garante `templates/Reuniões.md` e `SyncAsync`/`CreateMeetingNoteAsync` geram `zettelkasten/Reunião - <SafeTitle>.md` preservando seções de notas | Criar nota em `daily/` ou sobrescrever a nota inteira no re-sync | Permite atas ricas no Zettelkasten sem perder anotações humanas |
| D4 | Fidelidade completa de `/spec-frontend` para o bloco `agenda-timeline` usando tokens `:root` existentes (Bucket A) e classes de `granbrain-redesign.html` | Criar tabela simples Angular ou novos tokens CSS globais | Reutiliza tema claro/escuro e entrega Lista, Calendário, filtros, conflitos e links idênticos ao mockup |
| D5 | Autenticação JWT Bearer (`.RequireAuthorization()`) em `/api/calendar/*` | `X-Api-Key` customizada ou autenticação dupla | Mantém 100% de paridade com `Program.cs`, `VaultEndpoints`, `AgentEndpoints` e `GranBrainApiService` |
| D6 | Reconciliação CSS canônica via `granbrain-redesign.html` + `styles.scss` | Valores literais divergentes na prosa inicial (`84px 1fr`, tokens `--text-2`/`--radius-lg`) | Garante fidelidade fiel ao artefato Claude Design e ao tema claro/escuro de `styles.scss` |

### D1 — Arquitetura de Conectores Google + Microsoft Graph e Fallback Determinístico
- **Pergunta:** Como os conectores Google Calendar e Microsoft Graph (Outlook + Teams) devem ser estruturados, autenticados e tratados quando não houver credenciais configuradas?
- **Opções consideradas:**
  1. Implementar `GoogleCalendarConnector` e um `MicrosoftGraphCalendarConnector` unificado (`/me/calendar/events`), renovando access token apenas em memória a partir de `ClientId`/`ClientSecret`/`RefreshToken` e retornando um fallback determinístico de 5 eventos quando as credenciais estiverem vazias.
  2. Separar `OutlookConnector` e `TeamsConnector` como clientes HTTP independentes contra o mesmo Graph API.
  3. Persistir `RefreshToken` e `AccessToken` criptografados dentro de uma nota oculta no vault.
- **Decisão:** Opção 1 — `GoogleCalendarConnector` + `MicrosoftGraphCalendarConnector` unificado (classificando evento como `Teams` quando `isOnlineMeeting == true` ou o URL contém `teams.microsoft.com`, e `Outlook` caso contrário), refresh apenas em memória via `IHttpClientFactory`, zero segredos em `appsettings.json` ou disco, e fallback determinístico de 5 eventos alinhado ao mockup. Falha OAuth `401` ou configuração parcial dispara `VaultValidationException("calendar_auth_failed", ...)`.
- **Justificativa:** Evita duplicação de cliente Microsoft Graph, cumpre a regra de nunca persistir credenciais ou tokens e permite desenvolvimento, testes e navegação UI completos sem conta externa conectada.

### D2 — Sincronização Idempotente na Nota Diária e Isolamento de Hábitos
- **Pergunta:** Como gravar eventos sincronizados em `daily/YYYY-MM-DD.md` sem duplicar itens em execuções repetidas e sem corromper os checkboxes de hábitos monitorados por `ObsidianVaultService`?
- **Opções consideradas:**
  1. Gravar eventos como itens de lista sem checkbox marcados com `<!-- cal-id:<id> -->`, mesclados cronologicamente em `#### Cronograma` e listados em `## 🤝🏻 Reuniões` antes do bloco `dataview`, alterando `ParseHabitsFromLines` e `ToggleHabitAsync` para ignorar linhas com `<!-- cal-id:` e restringir a busca a `#### Cronograma`.
  2. Inserir eventos como checkboxes `- [ ] HH:mm - Título` em `#### Cronograma`.
  3. Criar uma seção nova exclusiva fora do template padrão do usuário (`templates/Notas Diárias.md`).
- **Decisão:** Opção 1 — Linhas de lista sem checkbox com marcador HTML `<!-- cal-id:<id> -->` em `#### Cronograma` + seção `## 🤝🏻 Reuniões` (antes de ```` ```dataview ```` ou criada ao final em notas sem a seção), com guarda explícita no parser e toggle de hábitos.
- **Justificativa:** Preserva o layout real de `daily/YYYY-MM-DD.md` do Igor e impede que reuniões sejam confundidas com hábitos diários mesmo após múltiplos syncs e toggles.

### D3 — Template `templates/Reuniões.md` e Geração Segura de Atas
- **Pergunta:** Como criar e atualizar notas de reunião no vault preservando conteúdo editado manualmente pelo usuário?
- **Opções consideradas:**
  1. `VaultInitializer` garante `templates/Reuniões.md` se ausente (sem sobrescrever se existir); `CreateMeetingNoteAsync` resolve `zettelkasten/Reunião - <SafeTitle>.md` via `VaultPathGuard.ResolveSafePath` (validando `TargetFolder` bruto antes de sanitizar o título), cria a partir do template na primeira execução e, nas seguintes, preserva tudo em `## 📝 Discussão & Anotações` e `## ✅ Plano de Ação & Tarefas`.
  2. Sobrescrever o arquivo inteiro a cada sincronização de metadados do evento.
  3. Permitir gravação direta usando `TargetFolder` sem validação prévia de caminho relativo.
- **Decisão:** Opção 1 — Criação não destrutiva de `templates/Reuniões.md`, `VaultPathGuard` obrigatório no caminho completo e preservação das seções manuais da ata.
- **Justificativa:** Respeita integralmente o invariante de `Vault Jail` e evita perda de anotações humanas do Obsidian.

### D4 — Experiência Visual do Bloco Generativo `agenda-timeline`
- **Pergunta:** Quais elementos visuais do mockup `granbrain-redesign.html` devem entrar no escopo do componente Angular da WI 0003?
- **Opções consideradas:**
  1. Implementação completa conforme `/spec-frontend` usando tokens `:root` existentes em `styles.scss` (Bucket A): cabeçalho com `.seg` (`Lista` / `Calendário`), filtros `.chips` (`Todos`, `Google`, `Teams`, `Outlook`), alerta `.conflict`, timeline `.ev`/`.ev.past`/`.ev.now`/`.ev.next` com `.nowline` e ações `.link` (`[data-event-join]`, `[data-event-note]`), grade horária `.calwrap`/`.cal-grid`/`.cal-line`/`.cal-hour`/`.cal-now`/`.cal-ev` com cores por provedor e destaque `.clash`, além de fiação do rail `0003` em `AppComponent`.
  2. Apenas uma lista simplificada de eventos sem modo grade horária `.calwrap` nem `.conflict`.
- **Decisão:** Opção 1 — Fidelidade completa Lista + Calendário, filtros, conflitos, `nowline`/`cal-now` e fiação do rail `0003`.
- **Justificativa:** Entrega a aba `Agenda & Sincronia` completa para uso real no GranBrain seguindo todos os critérios do mockup aprovado sem introduzir tokens globais fora do Bucket A.

### D5 — Autenticação JWT Bearer em `/api/calendar/*` (Decisão durante execução)
- **Pergunta:** O rascunho inicial da AC1 citava `X-Api-Key`, mas `Program.cs`, `VaultEndpoints`, `AgentEndpoints` e `GranBrainApiService` usam exclusivamente JWT Bearer (`.RequireAuthorization()`). Qual mecanismo adotar?
- **Decisão:** Usar exclusivamente JWT Bearer (`.RequireAuthorization()`) igual a `VaultEndpoints` e `AgentEndpoints`, retornando `401 Unauthorized` sem token válido, e atualizar AC1/plano.

### D6 — Prevalência do CSS canônico de `granbrain-redesign.html` + `styles.scss` (Decisão durante execução)
- **Pergunta:** A prosa inicial da spec citava `84px 1fr` e tokens inexistentes (`--text-2`, `--radius-lg`), divergindo de `granbrain-redesign.html` (`56px 14px minmax(0, 1fr)`, `--fg`/`--info`/`--ok`/`--warn`/`--danger`). Qual deve prevalecer?
- **Decisão:** Seguir exatamente os tokens e CSS de `granbrain-redesign.html` + `styles.scss` (`56px 14px minmax(0, 1fr)`, `--fg`/`--info`/`--ok`/`--warn`/`--danger`) e alinhar AC5 e `app.spec.ts` a eles.
