# 0003 — Sincronização Ativa de Calendários (Google + Microsoft Graph / Outlook & Teams)

## Objetivo
Conectar o GranBrain aos calendários do Google e do Microsoft 365 (abrangendo tanto Outlook quanto reuniões do Teams via Microsoft Graph), agregando eventos em tempo real, populando de forma não-destrutiva as seções `#### Cronograma` e `## 🤝🏻 Reuniões` na nota diária do Obsidian (`daily/YYYY-MM-DD.md`) e renderizando uma linha do tempo dinâmica de compromissos no frontend com fidelidade ao Claude Design.

## Contexto
O usuário possui múltiplos calendários ativos (Google Calendar para vida pessoal e Microsoft 365 / Outlook / Teams para trabalho e compromissos profissionais). Atualmente, a cópia de horários e pautas para a nota diária do Obsidian depende de digitação manual, o que gera esquecimentos e atrito. Com a integração ativa, o sistema puxa os eventos das duas fontes, mescla cronologicamente, gera notas pré-formatadas para reuniões com participantes e pauta via `templates/Reuniões.md` e alerta visualmente sobre conflitos de horários conforme prototipado no redesign.

## Diretriz de Planejamento de Frontend (/spec-frontend)
**Mandatório na execução de `/spec-task`:**
O planejamento do frontend desta task deve ser realizado utilizando a skill `/spec-frontend`, extraindo o recorte das classes de agenda e gerando a subseção `### Especificação visual` dentro de `## Escopo`:
- **Origem do Import:** `frontend/_imports/claude-design/granbrain-redesign/2026-10-30/original/granbrain-redesign.html`
- **Artefato Claude:** `https://claude.ai/artifact/1puqYFtE3guy1J6CyvDXHy`
- **Recorte Visual do Mockup:** Telas e componentes de Agenda & Calendário (`.conflict`, `.timeline`, `.ev`, `.ev.now`, `.ev.past`, `.ev-card`, `.ev-top`, `.ev-meta`, `.nowline`, `.calwrap`, `.cal-grid`, `.cal-line`, `.cal-hour`, `.cal-ev`, `.cal-ev.p-google`, `.cal-ev.p-teams`, `.cal-ev.p-outlook`, `.cal-ev.clash`, `.cal-now`).
- **Elementos Visuais a Reconciliar:** Alerta de conflito de agenda estilizado com ícone de aviso (`.conflict`), linha do tempo com indicador do evento atual e linha vermelha com horário corrente (`.nowline`), e grade horária de calendário com badges codificados por cor por provedor (`.cal-ev.p-google` em `--info-soft`, `.cal-ev.p-teams` em `--accent-soft`, `.cal-ev.p-outlook` em `--warn-soft`).

## Escopo
**Dentro:**
- Conectores de calendário no Backend `.NET 10`:
  - `GoogleCalendarConnector`: integração via Google Calendar API (OAuth2) com escopo `calendar.readonly`.
  - `MicrosoftGraphCalendarConnector`: integração via Microsoft Graph API (`/me/calendar/events`) com autenticação MSAL (abrange Outlook e chamadas Teams com link de reunião).
- Serviço de Sincronização (`CalendarSyncService`):
  - Normalização e mesclagem cronológica dos eventos de ambos os provedores.
  - Atualização automática da nota diária do dia (`daily/YYYY-MM-DD.md`):
    - Inserção dos horários no `#### Cronograma` intercalando com os hábitos existentes.
    - Inserção dos links de reunião em `## 🤝🏻 Reuniões`.
  - Geração automática (opcional ou sob demanda) de notas estruturadas de reunião usando `templates/Reuniões.md`.
- Endpoints HTTP no backend:
  - `GET /api/calendar/today`: retorna a agenda agregada do dia com links, horários e participantes.
  - `POST /api/calendar/sync`: dispara a sincronização manual e atualização da nota diária.
- Frontend Ionic Angular (especificado via `/spec-frontend`):
  - Bloco de Generative UI `agenda-timeline`: linha do tempo moderna (`.timeline`, `.ev`) visualizando as reuniões do dia (passadas, em andamento com `.ev.now` e futuras), com links diretos para entrar na chamada e para a nota no Obsidian.
  - Grade horária de calendário e banner de conflito de reuniões (`.conflict`).
  - Botão de sincronização rápida na barra de comandos.

**Fora:**
- Escrita de eventos reversos do Obsidian para o Google/Outlook (neste estágio apenas leitura e alimentação do cofre).
- Sincronização de calendários do Apple iCloud ou CalDAV de terceiros.

## Critérios de aceite
1. **AC1 — Conexão Segura aos Provedores:** Suporte configurável a credenciais de API para Google Calendar e Microsoft Graph, armazenadas de forma segura e renovadas via refresh token.
2. **AC2 — Agregação Unificada e Cronológica:** Consulta combinada de Google + Microsoft Graph traz eventos do dia ordenados por horário de início, tratando fusos horários locais e identificando se o evento veio do Teams, Outlook ou Google.
3. **AC3 — Atualização Não-Destrutiva da Nota Diária:** O `CalendarSyncService` injeta as reuniões no `#### Cronograma` e em `## 🤝🏻 Reuniões` na nota do dia preservando todos os hábitos marcados, tarefas e anotações manuais preexistentes.
4. **AC4 — Geração de Notas com `templates/Reuniões.md`:** Criação de nota em `zettelkasten/` ou pasta de reuniões pré-preenchida com data, título, participantes e pauta da reunião.
5. **AC5 — Bloco Generative UI `agenda-timeline` Conforme Mockup:** O app Angular exibe o componente visual de linha do tempo e calendário reproduzindo exatamente as classes e tokens do recorte de `granbrain-redesign.html` (`.timeline`, `.ev`, `.nowline`, `.cal-ev`), com cores por provedor e alerta de conflitos.

## Área de impacto esperada
- `backend/src/GranBrain.Core/Interfaces/ICalendarProvider.cs`
- `backend/src/GranBrain.Core/Models/CalendarModels.cs`
- `backend/src/GranBrain.Infrastructure/Calendar/**`
- `backend/src/GranBrain.Infrastructure/Vault/ObsidianVaultService.cs`
- `backend/src/GranBrain.Api/Endpoints/CalendarEndpoints.cs`
- `backend/src/GranBrain.Api/appsettings.json`
- `backend/tests/GranBrain.UnitTests/**`
- `frontend/src/app/models/granbrain.models.ts`
- `frontend/src/app/components/generative-canvas/generative-canvas.component.ts`
- `frontend/src/app/components/generative-canvas/generative-canvas.component.html`
- `frontend/src/app/components/generative-canvas/generative-canvas.component.scss`
- Cofre Obsidian: `daily/YYYY-MM-DD.md`, `templates/Reuniões.md`

## Referências de arquitetura
- [CLAUDE.md](../../CLAUDE.md)
- [Artefato Claude Design](https://claude.ai/artifact/1puqYFtE3guy1J6CyvDXHy)
- [Import Local do Mockup](../../frontend/_imports/claude-design/granbrain-redesign/2026-10-30/original/granbrain-redesign.html)
- [Task 0001b: Redesign Frontend](./0001b-redesign-frontend-granbrain.md)

## Decisões preliminares do dev
| ID | Resumo |
|---|---|
| D1 | Outlook e Teams tratados como um único provedor Microsoft Graph (`/me/calendar/events`), eliminando redundância de APIs. |
| D2 | Sincronização no cofre é estritamente aditiva: reuniões são inseridas sem remover hábitos ou anotações manuais do usuário. |
| D3 | Bloco `agenda-timeline` integrado ao catálogo de Generative UI e planejado com `/spec-frontend` a partir de `.timeline`/`.calwrap`. |
