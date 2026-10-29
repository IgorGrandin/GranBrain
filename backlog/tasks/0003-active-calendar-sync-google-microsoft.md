# 0003 — Sincronização Ativa de Calendários (Google + Microsoft Graph / Outlook & Teams)

## Objetivo
Conectar o GranBrain aos calendários do Google e do Microsoft 365 (abrangendo tanto Outlook quanto reuniões do Teams via Microsoft Graph), agregando eventos em tempo real, populando de forma não-destrutiva as seções `#### Cronograma` e `## 🤝🏻 Reuniões` na nota diária do Obsidian (`daily/YYYY-MM-DD.md`) e renderizando uma linha do tempo dinâmica de compromissos no dashboard do frontend.

## Contexto
O usuário possui múltiplos calendários ativos (Google Calendar para vida pessoal e Microsoft 365 / Outlook / Teams para trabalho e compromissos profissionais). Atualmente, a cópia de horários e pautas para a nota diária do Obsidian depende de digitação manual, o que gera esquecimentos e atrito. Com a integração ativa, o sistema puxa os eventos das duas fontes, mescla cronologicamente, gera notas pré-formatadas para reuniões com participantes e pauta via `templates/Reuniões.md` e alerta sobre conflitos de horários.

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
- Frontend Ionic Angular:
  - Bloco de Generative UI `agenda-timeline`: linha do tempo moderna visualizando as reuniões do dia (passadas, em andamento e futuras), com links diretos para entrar na chamada e para a nota no Obsidian.
  - Botão de sincronização rápida na barra de comandos.

**Fora:**
- Escrita de eventos reversos do Obsidian para o Google/Outlook (neste estágio apenas leitura e alimentação do cofre).
- Sincronização de calendários do Apple iCloud ou CalDAV de terceiros.

## Critérios de aceite (Mínimo para /spec-task)
1. **AC1 — Conexão Segura aos Provedores:** Suporte configurável a credenciais de API para Google Calendar e Microsoft Graph, armazenadas de forma segura e renovadas via refresh token.
2. **AC2 — Agregação Unificada e Cronológica:** Consulta combinada de Google + Microsoft Graph traz eventos do dia ordenados por horário de início, tratando fusos horários locais e identificando se o evento veio do Teams, Outlook ou Google.
3. **AC3 — Atualização Não-Destrutiva da Nota Diária:** O `CalendarSyncService` injeta as reuniões no `#### Cronograma` e em `## 🤝🏻 Reuniões` na nota do dia preservando todos os hábitos marcados, tarefas e anotações manuais preexistentes.
4. **AC4 — Geração de Notas com `templates/Reuniões.md`:** Criação de nota em `zettelkasten/` ou pasta de reuniões pré-preenchida com data, título, participantes e pauta da reunião.
5. **AC5 — Bloco Generative UI `agenda-timeline` no Frontend:** O app Angular exibe o componente visual de linha do tempo com badges identificando o provedor (`Google` / `Teams` / `Outlook`) e status de andamento.

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
- Cofre Obsidian: `daily/YYYY-MM-DD.md`, `templates/Reuniões.md`

## Decisões preliminares do dev
| ID | Resumo |
|---|---|
| D1 | Outlook e Teams tratados como um único provedor Microsoft Graph (`/me/calendar/events`), eliminando redundância de APIs. |
| D2 | Sincronização no cofre é estritamente aditiva: reuniões são inseridas sem remover hábitos ou anotações manuais do usuário. |
| D3 | Bloco `agenda-timeline` integrado ao catálogo de Generative UI para renderização dinâmica via chat, voz ou dashboard. |
