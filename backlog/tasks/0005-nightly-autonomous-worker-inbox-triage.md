# 0005 — Rotinas Autônomas Noturnas e Triagem Inteligente do Inbox

## Objetivo
Implementar um serviço de background autônomo (*Background Worker / Cron Jobs*) no backend GranBrain que opera de madrugada e em horários programados para triar notas brutas no `inbox/`, fechar as métricas diárias, arquivar e categorizar notas órfãs no Zettelkasten e preparar antecipadamente o dia seguinte, com painel visual de rotinas e logs refinado via Claude Design.

## Contexto
Inspirado na tese do *"Obsidian Backend"* (Eric Michaud / Easy Machine AI): para eliminar a fricção humana que leva ao abandono do cofre, a IA deve atuar ativamente como "escriba e arquivista". Em vez de o usuário precisar sentar e organizar pastas todo dia, o GranBrain executa uma rotina noturna autônoma que processa o lixo da caixa de entrada (`inbox/`), gera estatísticas de conclusão de hábitos e deixa a nota da manhã seguinte pré-montada com cronograma e pautas. O frontend exibe o andamento e o histórico dessas rotinas de forma elegante.

## Diretriz de Planejamento de Frontend (/spec-frontend)
**Mandatório na execução de `/spec-task`:**
O planejamento do frontend desta task deve ser realizado utilizando a skill `/spec-frontend`, extraindo o recorte das classes de rotinas e gerando a subseção `### Especificação visual` dentro de `## Escopo`:
- **Origem do Import:** `frontend/_imports/claude-design/granbrain-redesign/2026-10-30/original/granbrain-redesign.html`
- **Artefato Claude:** `https://claude.ai/artifact/1puqYFtE3guy1J6CyvDXHy`
- **Recorte Visual do Mockup:** Telas e componentes de Rotinas & Auditoria (`.routine`, `.logrow`, `.logrow p`, `.code`, botões de disparo de ação rápida e badges de status de execução).
- **Elementos Visuais a Reconciliar:** Lista de rotinas automáticas com horários agendados e status, feed cronológico de logs de execução (`.logrow` com timestamp à esquerda e descrição à direita) e botão de ação para "Triar Inbox Agora" com feedback visual imediato.

## Escopo
**Dentro:**
- Motor de Agendamento no Backend .NET 10:
  - Implementação via `IHostedService` / `BackgroundService` ou agendador leve.
- Rotina 1: Triagem Inteligente do Inbox (`InboxTriageJob`):
  - Varre arquivos `.md` e transcrições soltas na pasta `inbox/`.
  - O agente de IA analisa o conteúdo bruto, extrai entidades, sugere tags e `[[WikiLinks]]` para os MOCs existentes (`zettelkasten/Índices/`).
  - Move a nota limpa para a pasta de destino correspondente (`zettelkasten/`, `reunioes/` ou arquivamento) e esvazia a fila do inbox.
- Rotina 2: Fechamento Noturno (`NightlyClosureJob` - ex.: 23:30):
  - Lê a nota diária do dia (`daily/YYYY-MM-DD.md`), calcula taxa de adesão aos hábitos matinais/noturnos e tarefas concluídas no Kanban.
  - Insere um breve resumo executivo no campo de reflexões da nota diária.
- Rotina 3: Preparação Matinal (`MorningPrepJob` - ex.: 06:00):
  - Cria a nota diária do novo dia a partir do template atualizado `templates/Notas Diárias.md`.
  - Puxa as tarefas pendentes de `assets/kanban/Profissional.md` e `Pessoal.md` e sinaliza prioridades.
- Frontend Ionic Angular (especificado via `/spec-frontend`):
  - Tela/aba "Rotinas" com cartões de rotinas ativas (`.routine`), histórico estruturado de execuções (`.logrow`) e botão de ação imediata "Triar Inbox Agora" (`.btn.primary`).

**Fora:**
- Exclusão irreversível de notas (itens descartados no inbox são movidos para `inbox/_archive/`, nunca deletados, respeitando a regra inegociável de governança).

## Critérios de aceite
1. **AC1 — Triagem Automatizada do Inbox:** Notas brutas colocadas em `inbox/` recebem frontmatter YAML estruturado e são transferidas para `zettelkasten/` com links para os índices sem intervenção manual.
2. **AC2 — Fechamento Noturno sem Sobrescrever Dados:** O job noturno atualiza a nota diária adicionando métricas e resumo sem apagar anotações ou diários feitos pelo usuário.
3. **AC3 — Preparação Matinal Confiável:** Às 06:00 a nota diária do dia nasce pré-configurada e disponível no cofre com hábitos desmarcados prontos para uso.
4. **AC4 — Rastreabilidade e Logs de Execução:** Toda ação autônoma da IA no cofre gera entrada de auditoria em log e respeita estritamente o `VaultPathGuard` (`Vault Jail`).
5. **AC5 — Painel de Rotinas no Frontend Conforme Mockup:** O app Angular exibe o componente de rotinas e logs renderizando com exatidão as classes `.routine` e `.logrow` de `granbrain-redesign.html`.

## Área de impacto esperada
- `backend/src/GranBrain.Infrastructure/Jobs/**`
- `backend/src/GranBrain.Infrastructure/Vault/ObsidianVaultService.cs`
- `backend/src/GranBrain.Api/Program.cs`
- `backend/tests/GranBrain.UnitTests/**`
- `frontend/src/app/models/granbrain.models.ts`
- `frontend/src/app/components/generative-canvas/**`
- Cofre Obsidian: `inbox/`, `daily/`, `zettelkasten/`

## Referências de arquitetura
- [CLAUDE.md](../../CLAUDE.md)
- [Artefato Claude Design](https://claude.ai/artifact/1puqYFtE3guy1J6CyvDXHy)
- [Import Local do Mockup](../../frontend/_imports/claude-design/granbrain-redesign/2026-10-30/original/granbrain-redesign.html)
- [Task 0001b: Redesign Frontend](./0001b-redesign-frontend-granbrain.md)

## Decisões preliminares do dev
| ID | Resumo |
|---|---|
| D1 | Processamento do inbox usa as CLIs locais autenticadas ou fallback contextual, evitando custos de API por token. |
| D2 | Notas triadas nunca são apagadas do disco; o arquivo bruto original é movido para `inbox/_archive/` como histórico de segurança. |
| D3 | Jobs executam com isolamento de falha (uma exceção em um arquivo corrompido não derruba a API). |
| D4 | Painel de rotinas segue layout e classes `.routine`/`.logrow` do mockup via `/spec-frontend`. |
