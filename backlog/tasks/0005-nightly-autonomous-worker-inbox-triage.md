# 0005 — Rotinas Autônomas Noturnas e Triagem Inteligente do Inbox

## Objetivo
Implementar um serviço de background autônomo (*Background Worker / Cron Jobs*) no backend GranBrain que opera de madrugada e em horários programados para triar notas brutas no `inbox/`, fechar as métricas diárias, arquivar e categorizar notas órfãs no Zettelkasten e preparar antecipadamente o dia seguinte.

## Contexto
Inspirado na tese do *"Obsidian Backend"* (Eric Michaud / Easy Machine AI, discutido na conversa `8cc6b830-ec40-4eae-b8a7-d22515cab2fe`): para eliminar a fricção humana que leva ao abandono do cofre, a IA deve atuar ativamente como "escriba e arquivista". Em vez de o usuário precisar sentar e organizar pastas todo dia, o GranBrain executa uma rotina noturna autônoma que processa o lixo da caixa de entrada (`inbox/`), gera estatísticas de conclusão de hábitos e deixa a nota da manhã seguinte pré-montada com cronograma e pautas.

## Escopo
**Dentro:**
- Motor de Agendamento no Backend .NET 10:
  - Implementação via `IHostedService` / `BackgroundService` ou agendador leve (Quartz.NET ou Coravel).
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
- Frontend:
  - Exibição de logs das últimas execuções de rotina e botão de "Triar Inbox Agora" sob demanda.

**Fora:**
- Exclusão irreversível de notas (itens descartados no inbox são movidos para `inbox/_archive/`, nunca deletados, respeitando a regra inegociável de governança).

## Critérios de aceite (Mínimo para /spec-task)
1. **AC1 — Triagem Automatizada do Inbox:** Notas brutas colocadas em `inbox/` recebem frontmatter YAML estruturado e são transferidas para `zettelkasten/` com links para os índices sem intervenção manual.
2. **AC2 — Fechamento Noturno sem Sobrescrever Dados:** O job noturno atualiza a nota diária adicionando métricas e resumo sem apagar anotações ou diários feitos pelo usuário.
3. **AC3 — Preparação Matinal Confiável:** Às 06:00 a nota diária do dia nasce pré-configurada e disponível no cofre com hábitos desmarcados prontos para uso.
4. **AC4 — Rastreabilidade e Logs de Execução:** Toda ação autônoma da IA no cofre gera entrada de auditoria em log e respeita estritamente o `VaultPathGuard` (`Vault Jail`).

## Área de impacto esperada
- `backend/src/GranBrain.Infrastructure/Jobs/**`
- `backend/src/GranBrain.Infrastructure/Vault/ObsidianVaultService.cs`
- `backend/src/GranBrain.Api/Program.cs`
- `backend/tests/GranBrain.UnitTests/**`
- Cofre Obsidian: `inbox/`, `daily/`, `zettelkasten/`

## Decisões preliminares do dev
| ID | Resumo |
|---|---|
| D1 | Processamento do inbox usa as CLIs locais autenticadas ou fallback contextual, evitando custos de API por token. |
| D2 | Notas triadas nunca são apagadas do disco; o arquivo bruto original é movido para `inbox/_archive/` como histórico de segurança. |
| D3 | Jobs executam com isolamento de falha (uma exceção em um arquivo corrompido não derruba a API). |
