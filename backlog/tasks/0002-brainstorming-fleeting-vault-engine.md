# 0002 — Motor de Brainstorming, Captura Rápida (Fleeting) e Gestão de Ideias

## Objetivo
Transformar o GranBrain em uma esteira ativa de geração e amadurecimento de ideias, integrando a captura rápida (*Zero-Esforço*) para `zettelkasten/Fleeting.md` ou `inbox/`, sessões socráticas de ideação via skill `/brainstorming` com o template `templates/Brainstorming.md`, e promoção orientada para conceitos maduros (`Permanente.md`) ou execução (`Projetos.md`), com blocos de Generative UI no frontend seguindo à risca o visual do Claude Design.

## Contexto
O usuário possui atualmente no cofre as notas `zettelkasten/Fleeting.md` (para pensamentos efêmeros) e `zettelkasten/Permanente.md` (para conceitos consolidados), além de uma pasta `inbox/`, a central `zettelkasten/Brainstorming.md` e os templates `templates/Brainstorming.md` e `templates/Projetos.md`. No entanto, o fluxo de registrar uma ideia no calor do momento e depois refiná-la com o apoio da IA ainda é manual e fragmentado. Este Work Item formaliza a esteira `Fleeting ➔ Brainstorming (Grilling/Desafio) ➔ Permanente / Projetos` dentro do sistema, com experiência visual socrática inspirada no artefato de design.

#### Reconciliação com o design-system — 2026-11-13

| Elemento | Mockup (`granbrain-redesign.html`) | Design-system GranBrain (`frontend/src/styles.scss`) | Balde | Resolução |
|---|---|---|:---:|---|
| Card de captura rápida (`.card-quick`, `.quick-form`) | `padding: 14px` (`:333`), `display: flex; gap: 10px; align-items: center` (`:328`), `.input.big-in` `min-height: 50px; font-size: 16px` (`:197`) | `.card-quick`, `.quick-form`, `.input.big-in` (`styles.scss:197, 344-348`) | A | Reuso direto das classes já declaradas em `styles.scss` |
| Dica abaixo da captura (`.hint`) | `margin: 10px 2px 0; font-size: 13px; color: var(--fg-3)` (`:332`) | Token `--fg-3` (`styles.scss:25, 63`) + seletor `.hint` | A | Token `--fg-3` existente; seletor `.hint` copiado literalmente de `granbrain-redesign.html:332` |
| Linha de ideia na esteira (`.idea`, `.idea-main`, `.idea-side`) | `display: flex; justify-content: space-between; align-items: center; gap: 14px 18px; padding: 14px 0; border-bottom: 1px solid var(--line); flex-wrap: wrap` (`:342-346`) | Token `--line` (`styles.scss:21, 59`) + seletores `.idea*` | A | Token `--line` existente; regras `.idea`, `.idea-main`, `.idea-side` adicionadas a `styles.scss` |
| Chips de estágio e ações (`.stages`, `.acts`) | `display: flex; gap: 6px; flex-wrap: wrap` (`:347-348`), `.pill.info` (`Fleeting`), `.pill.accent` (`Em brainstorm`), `.pill.ok` (`Promovida`), `.pill` (`Descartada`) (`:765, 1154`) | `.pill`, `.pill.info`, `.pill.accent`, `.pill.ok` (`styles.scss:159-167`) | A | Tokens e variantes `.pill` existentes |
| Cabeçalho da sessão (`.session-h`) | `display: flex; justify-content: space-between; gap: 16px; align-items: center; flex-wrap: wrap` e `h2 { font-size: 21px; margin-top: 4px }` (`:349-350`) | `.card` + `--font-display` (`styles.scss:15, 147, 156`) | A | Regras `.session-h` adicionadas a `styles.scss` |
| Grade de 3 colunas do Brainstorm (`.board3`) | `display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 16px; margin-top: 16px` (`:351`) | Layout utilitário em `styles.scss` | A | Regra `.board3` adicionada a `styles.scss` |
| Linha de hipótese / risco (`.hyp`) | `display: flex; justify-content: space-between; align-items: flex-start; gap: 10px; padding: 11px 0; border-bottom: 1px solid var(--line)` e `p { margin: 0; font-size: 14px }` (`:352-354`) | Token `--line` + `.pill.{ok,warn,danger}` (`styles.scss:21, 163-165`) | A | Regra `.hyp` adicionada a `styles.scss` |
| Painel socrático (`.socratic`, `.socratic .q`) | `display: grid; gap: 10px; margin-top: 16px` e `.q { font: 600 21px/1.3 var(--font-display); margin: 4px 0 0; color: var(--fg); max-width: 40ch; text-wrap: balance }` (`:355-356`) | Tokens `--font-display` e `--fg` (`styles.scss:15, 23`) | A | Regras `.socratic` e `.socratic .q` adicionadas a `styles.scss` |
| Barra de ações de promoção e nota de rodapé (`.bar-actions`, `.note-line`) | `.bar-actions { display: flex; gap: 8px; flex-wrap: wrap; margin-top: 16px }` (`:357`), `.note-line { margin-top: 16px }` (`:298`) | `.btn.primary`, `.btn`, `.btn.ghost`, `.small.muted` (`styles.scss:137-138, 174-184`) | A | Regras `.bar-actions` e `.note-line` adicionadas a `styles.scss` |

## Escopo

### Especificação visual

Origem: `frontend/_imports/claude-design/granbrain-redesign/2026-10-30/original/granbrain-redesign.html` (linhas `298`, `332-333`, `341-358`, `751-766`, `827-832`, `1135-1204`). Valores não especificados aqui seguem o token equivalente do design-system em `frontend/src/styles.scss` — não hardcodar.

- **1. Bloco de Captura Rápida de Ideias (`idea-capture` — `.card.card-quick`, `.quick-form`, `.hint`):**
  - **Container (`.card.card-quick`):** fundo `var(--surface)`, borda `1px solid var(--line)`, raio `var(--radius-card)` (`14px`), padding `14px` (`granbrain-redesign.html:156, 333`).
  - **Formulário (`.quick-form`):** `display: flex; gap: 10px; align-items: center` (`granbrain-redesign.html:328`).
  - **Campo de texto (`.input.big-in`):** `min-height: 50px; font-size: 16px; flex: 1; min-width: 0`, placeholder `"Registre a ideia antes que ela escape"` (`granbrain-redesign.html:197, 329, 1145`).
  - **Botão de ditado por voz (`.btn.icon.mic`):** `width: 50px; min-height: 50px; border-radius: var(--radius-btn)`, `aria-label="Ditar ideia"`; quando em escuta ativa, recebe `.live` com `background: var(--danger); border-color: var(--danger); color: #ffffff; animation: pulse 1s ease-in-out infinite` (`granbrain-redesign.html:188, 330, 1146`).
  - **Botão de submissão (`.btn.primary`):** `min-height: 50px; background: var(--accent); color: var(--accent-fg)`, rótulo `"Capturar"` (`granbrain-redesign.html:181, 331, 1147`).
  - **Texto de dica (`.hint`):** `margin: 10px 2px 0; font-size: 13px; color: var(--fg-3)`, exibindo `"Gravada com horário em zettelkasten/Fleeting.md."` com caminho em `.mono` (`granbrain-redesign.html:332, 1149`).

- **2. Bloco de Esteira e Sessão Socrática (`brainstorm-canvas` — `.stages`, `.idea`, `.session-h`, `.board3`, `.hyp`, `.socratic`, `.bar-actions`):**
  - **Cabeçalho de ideias ativas (`.card-h` + `.stages`):** título `h2` `"Ideias ativas"` à esquerda e container `.stages` (`display: flex; gap: 6px; flex-wrap: wrap`) à direita com 4 pills de contagem por estágio (`granbrain-redesign.html:348, 765, 1154`):
    - `Fleeting <n>`: `.pill.info` (`background: var(--info-soft); color: var(--info)`)
    - `Em brainstorm <n>`: `.pill.accent` (`background: var(--accent-soft); color: var(--accent)`)
    - `Promovida <n>`: `.pill.ok` (`background: var(--ok-soft); color: var(--ok)`)
    - `Descartada <n>`: `.pill` (`background: var(--surface-2); color: var(--fg-2)`)
  - **Linha de ideia (`.idea`):** `display: flex; justify-content: space-between; align-items: center; gap: 14px 18px; padding: 14px 0; border-bottom: 1px solid var(--line); flex-wrap: wrap`, última linha sem borda inferior (`.idea:last-child { border-bottom: 0; padding-bottom: 0 }`) (`granbrain-redesign.html:342-343`).
  - **Coluna principal da ideia (`.idea-main`):** `min-width: 0; flex: 1 1 260px`, texto em `<p>` (`margin: 0 0 4px; font-weight: 500`) e horário em `<span class="mono muted">` (`granbrain-redesign.html:344-345, 1157`).
  - **Coluna lateral da ideia (`.idea-side` e `.acts`):** `display: flex; gap: 10px; align-items: center; flex-wrap: wrap`; exibe a `.pill` do status atual e, dentro de `.acts` (`display: flex; gap: 6px; flex-wrap: wrap`), exibe:
    - botão `.btn.sm` `"Brainstorm"` apenas quando `status === 'fleeting'`;
    - botões `.btn.sm` `"Promover"` e `.btn.sm.ghost` `"Descartar"` quando `status === 'fleeting'` ou `status === 'brainstorm'`;
    - nenhum botão de ação quando `status === 'promovida'` ou `status === 'descartada'` (`granbrain-redesign.html:1160-1163`).
  - **Cabeçalho da sessão socrática (`.card.session-h`):** `display: flex; justify-content: space-between; gap: 16px; align-items: center; flex-wrap: wrap`; à esquerda `<span class="eyebrow">Ideia em revisão</span>` e `<h2>` (`font-size: 21px; margin-top: 4px`); à direita a `.pill` de status da ideia em revisão (`granbrain-redesign.html:349-350, 1177-1180`).
  - **Quadro de 3 colunas (`.board3`):** `display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 16px; margin-top: 16px` (`granbrain-redesign.html:351, 1181-1191`):
    - **Coluna 1 — `Problema` (`.card`):** `.card-h > h2` `"Problema"` e `<p class="muted" style="margin:0">`.
    - **Coluna 2 — `Hipóteses` (`.card`):** `.card-h > h2` `"Hipóteses"` e lista de `.hyp` (`display: flex; justify-content: space-between; align-items: flex-start; gap: 10px; padding: 11px 0; border-bottom: 1px solid var(--line)`, `<p>` com `margin: 0; font-size: 14px`) acompanhada de `.pill` de confiança (`alta` → `.pill.ok`, `média` → `.pill.warn`, `baixa` → `.pill`).
    - **Coluna 3 — `Riscos levantados` (`.card`):** `.card-h > h2` `"Riscos levantados"` e lista de `.hyp` acompanhada de `.pill` de severidade (`alto` → `.pill.danger`, `médio` → `.pill.warn`, `baixo` → `.pill`).
  - **Painel Socrático (`.card.socratic`):** `display: grid; gap: 10px; margin-top: 16px`; contém `<span class="eyebrow">Próxima pergunta da IA</span>`, pergunta `<p class="q">` em `font: 600 21px/1.3 var(--font-display); margin: 4px 0 0; color: var(--fg); max-width: 40ch; text-wrap: balance`, e `<textarea class="input" rows="3" placeholder="Responda com suas palavras…">` (`granbrain-redesign.html:355-356, 1192-1197`).
  - **Barra de ações de promoção (`.bar-actions` e `.note-line`):** `display: flex; gap: 8px; flex-wrap: wrap; margin-top: 16px` com 3 botões: `.btn.primary` `"Promover para Permanente"`, `.btn` `"Promover para Projeto"`, `.btn.ghost` `"Descartar"`, seguidos por `<p class="small muted note-line">Ao promover, a nota de origem recebe o status <span class="mono">promovida</span> e mantém os backlinks.</p>` (`granbrain-redesign.html:298, 357, 1198-1203`).

**Dentro:**
- Endpoints HTTP no backend `.NET 10`:
  - `POST /api/vault/ideas/fleeting`: inclusão atômica de ideias rápidas (com timestamp, ID e links) em `zettelkasten/Fleeting.md` ou criação de nota em `inbox/`.
  - `POST /api/vault/ideas/brainstorm`: inicialização de sessão de brainstorming a partir de uma ideia efêmera, criando nota via `templates/Brainstorming.md` com problema, hipóteses (com confiança), riscos (com severidade) e pergunta de grilling socrático.
  - `POST /api/vault/ideas/promote`: promoção de uma ideia para `zettelkasten/Permanente.md` (e nota atômica em `zettelkasten/`), criação de nota de projeto via `templates/Projetos.md`, ou descarte (`descartada`), atualizando o status na nota de origem e preservando backlinks.
  - `GET /api/vault/ideas/overview`: listagem consolidada de ideias ativas em Fleeting, sessões de Brainstorming, ideias promovidas e descartadas, mais a sessão ativa detalhada (`.board3` + `.socratic`).
- Bootstrap não-destrutivo em `VaultInitializer`: garantir existência de `templates/Brainstorming.md`, `templates/Projetos.md`, `zettelkasten/Fleeting.md`, `zettelkasten/Permanente.md` e `zettelkasten/Brainstorming.md` (central Dataview) e sanitização de `---` duplicado em `Fleeting.md` e `Permanente.md` sem perder conteúdo existente.
- Frontend Ionic Angular (especificado via `/spec-frontend`):
  - Bloco de Generative UI `idea-capture`: formulário com captura rápida e gatilho de voz (`.quick-form`, `.card-quick`, `.btn.icon.mic`, `.hint`).
  - Bloco de Generative UI `brainstorm-canvas`: visualização de esteira de ideias (`.stages`, `.idea`), cabeçalho de revisão (`.session-h`), quadro de 3 colunas de problema/hipóteses/riscos (`.board3`, `.hyp`), canvas socrático com pergunta desafiadora (`.socratic`, `.socratic .q`) e barra de promoção/descarte (`.bar-actions`, `.note-line`).
  - Ativação do item de navegação `.rail` `"Ideias & Fleeting"` (`WI 0002`) no `AppComponent`.
- Integração com a skill `/brainstorming` no `AgentOrchestratorService`:
  - Intenções determinísticas (`"ideia: ..."`, `"fleeting: ..."`) roteadas para `CaptureFleetingIdeaAsync`, geração dos blocos `idea-capture` e `brainstorm-canvas`, e enriquecimento socrático nas chamadas às CLIs locais (`Codex`, `Claude`, `Gemini`).

**Fora:**
- Sincronização externa com ferramentas de mapas mentais como Miro ou Figma.
- Publicação pública de notas de brainstorming na web.

## Critérios de aceite
1. **AC1 — Captura Rápida Atômica (`Fleeting`):** Requisição para `/api/vault/ideas/fleeting` insere a ideia com timestamp, identificador e links sob `#### Ideias Rápidas` em `zettelkasten/Fleeting.md` (ou cria nota estruturada com YAML válido em `inbox/` quando `target="inbox"`), sanitizando eventual delimitador `---` duplicado no frontmatter sem sobrescrever nem truncar anotações existentes, e rejeitando texto vazio com HTTP 400 (`idea_text_required`) ou path traversal com HTTP 400 (`vault_path_violation`).
2. **AC2 — Criação de Sessão de Brainstorming Estruturada e Central Dataview:** O `VaultInitializer` garante de forma não-destrutiva a central `zettelkasten/Brainstorming.md` (com blocos `dataview`) e os templates `templates/Brainstorming.md` e `templates/Projetos.md`; o endpoint `/api/vault/ideas/brainstorm` gera uma nota em `zettelkasten/Brainstorming - <SafeTitle>.md` usando `templates/Brainstorming.md` com YAML único válido (`type: brainstorming`, `status: em_analise`, `origem: fleeting`), contendo problema, hipóteses (com confiança), riscos levantados (com severidade) e pergunta de grilling socrático, atualizando o status da ideia de origem para `brainstorm` e retornando HTTP 404 (`idea_not_found`) quando o `ideaId` informado não existir.
3. **AC3 — Promoção, Descarte e Transição de Estado com Preservação de Backlinks:** O endpoint `/api/vault/ideas/promote` valida o destino antes de mutar qualquer arquivo (retornando HTTP 400 `invalid_promotion_target` para destino inválido e HTTP 404 `idea_not_found` para ideia inexistente), atualiza a nota de origem (`status: promovida` ou `status: descartada` e checkbox de veredicto), e: (a) em `permanente`, insere idempotentemente o link `[[<SafeTitle>]]` sob `#### Ideias Maduras` em `zettelkasten/Permanente.md` sem apagar notas existentes e cria/atualiza `zettelkasten/<SafeTitle>.md` com backlinks; (b) em `projeto`, cria `zettelkasten/Projeto - <SafeTitle>.md` via `templates/Projetos.md` (`type: projeto`, `status: ativo`) preservando backlinks; (c) em `descartada`, registra o descarte sem criar nota em `Permanente` ou `Projetos`. O endpoint `GET /api/vault/ideas/overview` retorna a lista consolidada, contagens por estágio e sessão ativa.
4. **AC4 — Blocos de Generative UI (`idea-capture` e `brainstorm-canvas`) no Frontend e Orquestrador Conforme Mockup:** `styles.scss` declara as regras CSS canônicas do recorte de `granbrain-redesign.html` (`.idea`, `.idea-main`, `.idea-side`, `.acts`, `.stages`, `.session-h`, `.board3`, `.hyp`, `.socratic`, `.socratic .q`, `.bar-actions`, `.hint`, `.note-line`); `<app-generative-canvas>` e `AppComponent` renderizam `idea-capture` (com input `.big-in` e botão de voz `.btn.icon.mic`) e `brainstorm-canvas` (com `.stages`, `.idea`, `.session-h`, `.board3` de 3 colunas Problema/Hipóteses/Riscos, `.socratic .q` e `.bar-actions`), permitindo navegar pelo item `0002` do `.rail`, capturar ideias por texto ou voz, iniciar brainstorm e promover/descartar com feedback imediato em `.toast`.

## Área de impacto esperada
- `backend/src/GranBrain.Core/Interfaces/IVaultService.cs`
- `backend/src/GranBrain.Core/Models/VaultModels.cs`
- `backend/src/GranBrain.Core/Models/GenerativeUiModels.cs`
- `backend/src/GranBrain.Core/Security/VaultPathGuard.cs`
- `backend/src/GranBrain.Infrastructure/Vault/VaultInitializer.cs`
- `backend/src/GranBrain.Infrastructure/Vault/ObsidianVaultService.cs`
- `backend/src/GranBrain.Infrastructure/Agents/AgentOrchestratorService.cs`
- `backend/src/GranBrain.Api/Program.cs`
- `backend/src/GranBrain.Api/Endpoints/VaultEndpoints.cs`
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
- Cofre Obsidian: `zettelkasten/Fleeting.md`, `zettelkasten/Permanente.md`, `zettelkasten/Brainstorming.md`, `templates/Brainstorming.md`, `templates/Projetos.md`

## Referências de arquitetura
- [CLAUDE.md](../../CLAUDE.md)
- [Import Local do Mockup](../../frontend/_imports/claude-design/granbrain-redesign/2026-10-30/original/granbrain-redesign.html)
- [ADR-0001: Arquitetura Monorepo GranBrain](../../docs/adr/0001-granbrain-architecture.md)
- [Task 0001b: Redesign Frontend](./0001b-redesign-frontend-granbrain.md)

## Decisões do dev
| ID | Resumo | Data |
|---|---|---|
| D1 | Esteira em 3 fases (`Fleeting` ➔ `Brainstorming` ➔ `Permanente` / `Projeto` / `Descartada`) com transição de estado rastreável no Markdown | 2026-11-13 |
| D2 | Persistência direta nos arquivos Markdown do cofre sob `VaultPathGuard` (`Vault Jail`) e sanitização não-destrutiva de frontmatter | 2026-11-13 |
| D3 | Uso nativo do `dataview` na central `zettelkasten/Brainstorming.md` e templates `templates/Brainstorming.md` e `templates/Projetos.md` garantidos pelo `VaultInitializer` | 2026-11-13 |
| D4 | Especificação visual estrita via `/spec-frontend` adotando 100% dos seletores e tokens de `granbrain-redesign.html` (`.idea`, `.stages`, `.session-h`, `.board3`, `.hyp`, `.socratic`, `.bar-actions`) | 2026-11-13 |

### D1 — Esteira de ideação em 3 fases com transição de estado no Markdown
- **Problema:** Ideias capturadas em `zettelkasten/Fleeting.md` ou `inbox/` ficavam estáticas, sem fluxo estruturado para estresse socrático ou promoção para notas permanentes/projetos.
- **Decisão:** Formalizar 4 estados canônicos (`fleeting`, `brainstorm`, `promovida`, `descartada`) operados pelos endpoints `/api/vault/ideas/fleeting`, `/api/vault/ideas/brainstorm`, `/api/vault/ideas/promote` e `/api/vault/ideas/overview`.
- **Motivo:** Reproduz fielmente o modelo mental Zettelkasten do cofre do usuário e o fluxo interativo desenhado no mockup `granbrain-redesign.html`.
- **Alternativas descartadas:** Banco de dados SQLite separado para ideias — descartado porque viola o ADR-0001 (Obsidian Markdown como fonte única de verdade).
- **Impacto e dependências:** Estende `IVaultService`, `VaultEndpoints`, `AgentOrchestratorService` e `GenerativeCanvasComponent`.
- **Incertezas:** nenhuma.

### D2 — Persistência direta no cofre sob `VaultPathGuard` e sanitização não-destrutiva
- **Problema:** `zettelkasten/Fleeting.md` e `zettelkasten/Permanente.md` no cofre real possuem anotações preexistentes e delimitador `---` duplicado no frontmatter.
- **Decisão:** Toda leitura e escrita passa por `VaultPathGuard.ResolveSafePath`; a inserção em `Fleeting.md` e `Permanente.md` preserva integralmente as linhas existentes, remove apenas o segundo `---` duplicado do frontmatter e adiciona novas entradas de forma atômica e idempotente.
- **Motivo:** Cumpre a regra inegociável de segurança (`Vault Jail`) e de preservação do cofre (`CLAUDE.md` §2).
- **Alternativas descartadas:** Sobrescrever `Fleeting.md` a partir de um template limpo — descartado por destruir notas reais do usuário.
- **Impacto e dependências:** Afeta `VaultInitializer` e `ObsidianVaultService`.
- **Incertezas:** nenhuma.

### D3 — Central `zettelkasten/Brainstorming.md` com `dataview` e bootstrap de templates
- **Problema:** Ambientes novos ou cofres de teste temporários podem não conter `templates/Brainstorming.md`, `templates/Projetos.md` ou a central `zettelkasten/Brainstorming.md`.
- **Decisão:** `VaultInitializer.EnsureInitializedAsync` garante de forma idempotente e não-destrutiva a criação de `templates/Brainstorming.md`, `templates/Projetos.md`, `zettelkasten/Fleeting.md`, `zettelkasten/Permanente.md` e `zettelkasten/Brainstorming.md` (com queries `dataview` para sessões ativas, inbox e projetos promovidos) quando ausentes.
- **Motivo:** Mantém compatibilidade nativa com o plugin Dataview no Obsidian e garante funcionamento determinístico em qualquer cofre inicializado.
- **Alternativas descartadas:** Exigir criação manual prévia dos templates — descartado porque quebraria cofres recém-inicializados e testes automatizados.
- **Impacto e dependências:** Afeta `VaultInitializer.cs` e `VaultInitializerTests.cs`.
- **Incertezas:** nenhuma.

### D4 — Fidelidade visual integral ao recorte Ideias & Brainstorm de `granbrain-redesign.html`
- **Problema:** Os blocos `idea-capture` e `brainstorm-canvas` precisam conviver em harmonia visual com o redesign entregue na WI 0001b.
- **Decisão:** Adotar literalmente os seletores `.idea`, `.idea-main`, `.idea-side`, `.acts`, `.stages`, `.session-h`, `.board3`, `.hyp`, `.socratic`, `.socratic .q`, `.bar-actions`, `.hint` e `.note-line` do arquivo versionado `frontend/_imports/claude-design/granbrain-redesign/2026-10-30/original/granbrain-redesign.html`, todos mapeados para os tokens `:root` existentes (Balde A).
- **Motivo:** Zero divergência estética com o artefato aprovado pelo dev (`CLAUDE.md` §2).
- **Alternativas descartadas:** Componentes genéricos `ion-card`/`ion-list` do Ionic — descartado na WI 0001b.
- **Impacto e dependências:** Afeta `frontend/src/styles.scss`, `generative-canvas.component.*`, `app.component.*` e `app.spec.ts`.
- **Incertezas:** nenhuma.
