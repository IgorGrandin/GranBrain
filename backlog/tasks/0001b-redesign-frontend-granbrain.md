# 0001b — Redesign e Harmonização do Frontend com Claude Design Artifact

## Objetivo
Reformular a interface do usuário do GranBrain entregue no MVP 0001 para reproduzir com 100% de fidelidade o design visual, tipografia, paleta e microinterações estabelecidos no artefato Claude Design (`https://claude.ai/artifact/1puqYFtE3guy1J6CyvDXHy` e versionado em `frontend/_imports/claude-design/granbrain-redesign/2026-10-30/original/granbrain-redesign.html`), substituindo os componentes brutos do Ionic por uma experiência editorial *Command Center* ("mesa de trabalho calma").

## Contexto
O MVP 0001 entregou a infraestrutura funcional completa (backend .NET 10, conexão com cofre Obsidian, segurança Vault Jail, login JWT, orquestrador Multi-CLI e os 5 blocos de Generative UI). No entanto, o frontend foi construído provisoriamente com componentes genéricos do Ionic sem o polimento estético desenhado no artefato Claude Design.

O artefato canônico estabelece uma mesa de trabalho calma: superfícies claras com leve viés verde-petróleo (Dark Mode profundo correspondente), tipografia combinando `Bricolage Grotesque` (títulos), `IBM Plex Sans` (corpo) e `JetBrains Mono` (dados e caminhos), shell estruturado com trilho de navegação à esquerda (`.rail`), topbar translúcida com efeito blur, dashboard com fita métrica (`.strip`), lista de hábitos com checkboxes táteis, kanban colunado e assistente flutuante de IA com voz pulsante. Esta task intermediária consolida essa base visual antes de qualquer avanço nas features de ideação da 0002.

#### Reconciliação com o design-system — 2026-10-30

| Elemento | Mockup (`granbrain-redesign.html`) | Design-system GranBrain (`tokens.scss`) | Balde | Resolução |
|---|---|---|:---:|---|
| Fundo principal | `--bg: #f3f6f4` (dark: `#0b1210`) | `--bg` / `--ion-background-color` | A | Normalizado como token base de fundo |
| Superfície cartões | `--surface: #ffffff` (dark: `#121c18`) | `--surface` / `--ion-card-background` | A | Normalizado como token de superfícies |
| Superfície secundária | `--surface-2: #e8eeea` (dark: `#18251f`) | `--surface-2` | A | Normalizado para trilho lateral, colunas e inputs |
| Cor de destaque (Ação) | `--accent: #0c7766` (dark: `#51d0b3`) | `--accent` / `--ion-color-primary` | A | Verde-petróleo canônico da marca GranBrain |
| Destaque suave (Hover/Pill) | `--accent-soft: #d5eee7` (dark: `#143b32`) | `--accent-soft` | A | Normalizado para itens de menu ativos e badges |
| Tipografia display (Títulos) | `"Bricolage Grotesque", sans-serif` | `--font-display` | A | Importado do Google Fonts (pesos 500..800) |
| Tipografia corpo (Texto) | `"IBM Plex Sans", sans-serif` | `--font-body` | A | Importado do Google Fonts (pesos 400..600) |
| Tipografia técnica (Mono) | `"JetBrains Mono", monospace` | `--font-mono` | A | Importado do Google Fonts (pesos 400..500) |
| Linhas e divisores | `--line: #d5dfd9` / `--line-strong: #b5c4bc` | `--line` / `--line-strong` | A | Bordas sutis de cartões e colunas |
| Status Ok / Sucesso | `--ok: #17703a` / `--ok-soft: #d8efdf` | `--ok` / `--ok-soft` | A | Hábitos concluídos e status online |
| Status Alerta / Conflito | `--warn: #8a5a00` / `--warn-soft: #fbecc6` | `--warn` / `--warn-soft` | A | Conflitos e badges de atenção |
| Status Perigo / Gravação | `--danger: #b42318` / `--danger-soft: #fde1dc` | `--danger` / `--danger-soft` | A | Microfone em gravação ativa e erros |
| Status Informativo / WI | `--info: #3a52c8` / `--info-soft: #e0e6ff` | `--info` / `--info-soft` | A | Badges pontilhadas de WI (`.wi`) |
| Sombra dos cartões | `0 1px 2px rgba(...), 0 10px 28px rgba(...)` | `--shadow` | A | Sombra suave em cartões e diálogos |
| Raio de borda cartões | `14px` | `--radius-card: 14px` | B | Normalizado como token para cartões e strip |
| Raio de borda botões/inputs | `10px` | `--radius-btn: 10px` | B | Normalizado como token para botões e campos |
| Raio de borda badges/pills | `999px` | `--radius-pill: 999px` | B | Normalizado como token para pills e tags |

*Decisão do dev (Igor Grandin, 2026-10-30):* O mockup `granbrain-redesign.html` é a definição de referência fundacional do design system do GranBrain. Todas as propriedades declaradas no `:root` foram adotadas integralmente como a especificação canônica do frontend.

## Escopo

### Especificação visual

Origem do Import: `frontend/_imports/claude-design/granbrain-redesign/2026-10-30/original/granbrain-redesign.html`
Referência: `https://claude.ai/artifact/1puqYFtE3guy1J6CyvDXHy`

Valores visuais e comportamentais por componente:

- **1. Shell & Layout (`.app`, `.rail`, `.topbar`, `.view`):**
  - **Grid principal:** 2 colunas (`252px minmax(0, 1fr)`), altura mínima de `100vh` / `100dvh`.
  - **Trilho de Navegação (`.rail`):** lateral fixo (`position: sticky; top: 0; height: 100vh`), fundo `var(--surface)`, borda direita `1px solid var(--line)`, padding `22px 14px 18px`.
  - **Logo & Marca (`.brand`):** ícone SVG 32x32px em `var(--accent)`, texto "GranBrain" em `font: 700 18px/1.1 var(--font-display); letter-spacing: -.01em`.
  - **Grupos de Menu (`.nav-group`):** rótulo `.eyebrow` (`font: 500 11.5px/1.2 var(--font-mono); letter-spacing: .06em; text-transform: uppercase; color: var(--fg-3)`).
  - **Itens de Menu (`.nav-item`):** altura mínima `38px`, padding `0 10px`, raio `10px`, cor `var(--fg-2)`, peso `500`, tamanho `14px`. Hover em `var(--surface-2)`. Estado ativo (`.nav-item.on`): fundo `var(--accent-soft)`, texto `var(--accent)`.
  - **Badge de Work Item (`.wi`):** borda `1px dashed var(--info)`, cor `var(--info)`, fonte `500 11.5px/1.4 var(--font-mono)`, padding `1px 6px`, raio `999px`, alinhado à direita no menu.
  - **Barra Superior (`.topbar`):** fixa no topo, altura mínima `60px`, fundo translúcido `color-mix(in srgb, var(--bg) 86%, transparent)`, desfoque `backdrop-filter: blur(10px)`, borda inferior `1px solid var(--line)`.
  - **Breadcrumb (`.crumb`):** texto `14px`, separador `.sep` (`margin: 0 6px; color: var(--fg-3)`), título ativo em negrito `var(--fg)`.
  - **Chip do Usuário (`.user-chip`):** altura `32px`, borda `1px solid var(--line)`, raio `999px`, avatar circular de `28px` com iniciais e botão de ação.

- **2. Tela de Login (`.login`, `.login-art`, `.login-form`):**
  - **Container:** centralizado, largura máxima `1100px`, altura mínima `min(640px, calc(100dvh - 80px))`, raio `20px`, borda `1px solid var(--line)`, fundo `var(--surface)`.
  - **Painel Artístico Esquerdo (`.login-art`):** largura ~56%, fundo `var(--surface-2)`, padding `34px`, título em `700 30px/1.12 var(--font-display); letter-spacing: -.02em`, ilustração interativa do Knowledge Graph em SVG com nós em `var(--accent)` e arestas em `var(--line-strong)`.
  - **Painel de Autenticação Direito (`.login-form`):** largura ~44%, padding `40px`, campo de senha com botão de visualização embutido (`.pw .pw-btn`), alerta de credencial inválida em `.err` (`border: 1px solid var(--danger); background: var(--danger-soft); color: var(--danger)`), botão primário "Entrar no Cofre" com altura `42px` em `var(--accent)`.

- **3. Fita de Métricas do Dashboard (`.strip`, `.cell`):**
  - **Grade:** 4 colunas (`repeat(4, minmax(0, 1fr))`), gap de `1px` em `var(--line)`, borda `1px solid var(--line)`, raio `14px`, overflow oculto.
  - **Célula (`.cell`):** fundo `var(--surface)`, padding `16px 18px`, display grid com gap de `8px`.
  - **Número em Destaque (`.big`):** tipografia `700 30px/1 var(--font-display); letter-spacing: -.02em; font-variant-numeric: tabular-nums`.

- **4. Bloco de Hábitos (`.habit`, `.bar`):**
  - **Linha do Hábito (`.habit`):** display flex, padding `10px 0`, borda inferior `1px solid var(--line)`, cursor pointer.
  - **Caixa de Seleção (`.habit .check`):** tamanho `24x24px`, raio `8px`, borda `1.5px solid var(--line-strong)`, centralizado. Quando concluído (`.habit.done .check`): fundo `var(--accent)`, borda `var(--accent)`, ícone check branco.
  - **Texto do Hábito (`.lbl-h`):** peso `500`. Quando concluído (`.habit.done .lbl-h`): cor `var(--fg-3)`, riscado (`text-decoration: line-through`).
  - **Barra de Progresso (`.bar`):** altura `10px`, raio `999px`, fundo `var(--surface-2)`. Barra interna preenchida (`.bar i`) com fundo `var(--accent)` e transição suave de largura.

- **5. Bloco de Kanban (`.board-wrap`, `.board`, `.col`, `.kcard`):**
  - **Grade do Quadro (`.board`):** 4 colunas com largura mínima de `190px` (`repeat(4, minmax(190px, 1fr))`), gap de `12px`.
  - **Coluna (`.col`):** fundo `var(--surface-2)`, raio `14px`, padding `12px`, cabeçalho com nome e contagem mono.
  - **Card de Tarefa (`.kcard`):** fundo `var(--surface)`, borda `1px solid var(--line)`, raio `10px`, padding `12px`, texto em negrito `500`, seletor de coluna/ação rápida com altura `34px`.

- **6. Bloco de Notas Zettelkasten (`.notes-split`, `.notes-tools`, `.note`):**
  - **Split View:** layout de 2 colunas (`minmax(250px, .85fr) minmax(0, 1.4fr)`), gap `16px`.
  - **Busca (`.search`):** campo com ícone de lupa posicionado absolutamente à esquerda, padding esquerdo de `40px`.
  - **Lista lateral (`.list`):** itens `.nrow` com padding `10px 12px`, hover em `var(--surface-2)` e item ativo com fundo `var(--accent-soft)`.
  - **Visualizador de Nota (`.note`):** título h2 com `font-size: 24px`, parágrafos com `color: var(--fg-2); max-width: 68ch`, links `[[WikiLinks]]` destacados com `color: var(--accent); border-bottom: 1px dashed var(--accent)`, seção de `.backlinks` delimitada por linha.

- **7. Barra de Captura Rápida (`.card-quick`, `.quick-form`):**
  - **Container:** padding `14px`, display flex com gap de `10px`.
  - **Input flexível:** altura mínima de `50px`, padding `0 14px`, fonte `16px`.
  - **Botão de Microfone:** quadrado `50x50px`, raio `10px`, ícone centralizado de 18px.
  - **Botão Capturar:** altura `50px`, fundo `var(--accent)`, cor `var(--accent-fg)`, peso `500`.

- **8. Assistente Flutuante de Voz e Toasts:**
  - **Botão de Microfone Ao Vivo (`.btn.mic.live`):** fundo `var(--danger)`, borda `var(--danger)`, cor branca, animação `@keyframes pulse` com expansão de sombra em `var(--danger-soft)`.
  - **Sistema de Toasts (`.toasts`, `.toast`):** fixo no topo, z-index `80`, animação suave de descida `@keyframes toast-in`, fundo `var(--fg)` e texto `var(--bg)` (ou `var(--warn)` para alertas).

**Dentro:**
- Substituição do `styles.scss` genérico pelo sistema completo de tokens CSS e utilitários de `granbrain-redesign.html`.
- Importação das Google Fonts canônicas (`Bricolage Grotesque`, `IBM Plex Sans`, `JetBrains Mono`) em `index.html`.
- Reformulação do Shell (`app.component.html`/`app.component.scss`) com o trilho lateral `.rail`, a topbar translúcida e navegação reativa.
- Reformulação da tela de Login com layout split screen e gráfico de cofre em SVG.
- Reformulação dos 5 blocos de Generative UI no componente `<app-generative-canvas>` para usar a nova marcação CSS (`.strip`, `.habit`, `.board`, `.notes-split`, `.quick-form`).
- Manutenção integral dos serviços de API (`GranBrainApiService`) e comandos de voz (`VoiceService`).

**Fora:**
- Novas rotas ou regras de negócio das tasks 0002 a 0007 (apenas a moldura visual e navegação são preparadas).
- Alterações em contratos e endpoints do backend `.NET 10`.

## Critérios de aceite
1. **AC1 — Design Tokens e Tipografia Canônica:** A aplicação carrega as fontes `Bricolage Grotesque`, `IBM Plex Sans` e `JetBrains Mono` e aplica a paleta verde-petróleo nos temas claro e escuro respeitando as variáveis `:root` de `granbrain-redesign.html`.
2. **AC2 — Tela de Login Split-Screen:** A tela inicial deslogada exibe o layout split-screen com a ilustração em SVG do Knowledge Graph à esquerda e o formulário de login à direita, autenticando via JWT contra `/api/auth/login` e exibindo mensagens de erro no padrão `.err`.
3. **AC3 — Shell com Rail Lateral e Topbar Blur:** O usuário autenticado visualiza o layout com rail lateral contendo logo, itens de navegação com badges pontilhadas de WI (`.wi`), indicador de cofre no rodapé e topbar translúcida com breadcrumb.
4. **AC4 — Generative UI — Fita de KPIs e Hábitos:** A fita de KPIs renderiza 4 células no padrão `.strip`/`.cell`, e a lista de hábitos exibe a checkbox estilizada (`.habit .check`) de 24px com preenchimento verde-petróleo ao marcar e barra de progresso animada (`.bar`).
5. **AC5 — Generative UI — Kanban e Notas Zettelkasten:** O quadro Kanban exibe as 4 colunas no padrão `.col`/`.kcard`, e as notas são apresentadas no formato split-screen (`.notes-split`) com busca integrada e realce estilizado de `[[WikiLinks]]`.
6. **AC6 — Microfone Pulsante e Toasts:** O botão de captura de voz exibe animação pulsante `.btn.mic.live` durante o reconhecimento de fala, e as confirmações de ação aparecem em toasts no canto superior (`.toasts`, `.toast`).

## Área de impacto esperada
- `frontend/src/index.html`
- `frontend/src/styles.scss`
- `frontend/src/app/app.component.ts`
- `frontend/src/app/app.component.html`
- `frontend/src/app/app.component.scss`
- `frontend/src/app/components/generative-canvas/generative-canvas.component.ts`
- `frontend/src/app/components/generative-canvas/generative-canvas.component.html`
- `frontend/src/app/components/generative-canvas/generative-canvas.component.scss`

## Referências de arquitetura
- [CLAUDE.md](../../CLAUDE.md)
- [Artefato Claude Design](https://claude.ai/artifact/1puqYFtE3guy1J6CyvDXHy)
- [Import Local do Mockup](../../frontend/_imports/claude-design/granbrain-redesign/2026-10-30/original/granbrain-redesign.html)
- [ADR-0001: Arquitetura Monorepo GranBrain](../../docs/adr/0001-granbrain-architecture.md)
- [0001 — GranBrain MVP](./0001-granbrain-mvp-obsidian-ai-os.md)

## Decisões do dev
| ID | Resumo | Data |
|---|---|---|
| D1 | Criação da task intermediária 0001b antes da 0002 para harmonização estética completa com o Claude Design Artifact | 2026-10-30 |
| D2 | Versionamento do arquivo fonte `granbrain-redesign.html` em `frontend/_imports/claude-design/` para uso de `/spec-frontend` | 2026-10-30 |
| D3 | Preservação total da API .NET 10 do MVP 0001; refatoração restrita à camada de apresentação Angular | 2026-10-30 |
| D4 | Adoção integral dos design tokens e classes de `granbrain-redesign.html` como a referência de design system do GranBrain | 2026-10-30 |
