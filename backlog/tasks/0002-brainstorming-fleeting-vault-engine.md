# 0002 — Motor de Brainstorming, Captura Rápida (Fleeting) e Gestão de Ideias

## Objetivo
Transformar o GranBrain em uma esteira ativa de geração e amadurecimento de ideias, integrando a captura rápida (*Zero-Esforço*) para `zettelkasten/Fleeting.md` ou `inbox/`, sessões socráticas de ideação via skill `/brainstorming` com o novo template `templates/Brainstorming.md`, e promoção orientada para conceitos maduros (`Permanente.md`) ou execução (`Projetos.md`), com blocos de Generative UI no frontend.

## Contexto
O usuário possui atualmente no cofre as notas `zettelkasten/Fleeting.md` (para pensamentos efêmeros) e `zettelkasten/Permanente.md` (para conceitos consolidados), além de uma pasta `inbox/`. No entanto, o fluxo de registrar uma ideia no calor do momento e depois refiná-la com o apoio da IA ainda é manual e fragmentado. Este Work Item formaliza a esteira `Fleeting ➔ Brainstorming (Grilling/Desafio) ➔ Permanente / Projetos` dentro do sistema.

## Escopo
**Dentro:**
- Endpoints HTTP no backend `.NET 10`:
  - `POST /api/vault/ideas/fleeting`: inclusão atômica de ideias rápidas (com timestamp) em `zettelkasten/Fleeting.md` ou criação de nota em `inbox/`.
  - `POST /api/vault/ideas/brainstorm`: inicialização de sessão de brainstorming a partir de uma ideia efêmera, criando nota via `templates/Brainstorming.md`.
  - `POST /api/vault/ideas/promote`: promoção de uma ideia para `zettelkasten/Permanente.md` ou criação de nota de projeto via `templates/Projetos.md`.
  - `GET /api/vault/ideas/overview`: listagem consolidada de ideias ativas em Fleeting, sessões de Brainstorming e ideias promovidas.
- Frontend Ionic Angular:
  - Bloco de Generative UI `idea-capture` (campo de captura rápida com atalho de voz).
  - Bloco de Generative UI `brainstorm-canvas` (cards de ideação com hipóteses, riscos levantados pelo grilling e opções de promoção/descarte).
- Integração com a skill `/brainstorming`:
  - Prompts e templates contextualizados para conduzir sessões de ideação com as CLIs locais (`Codex`, `Claude`, `Gemini`), desafiando premissas antes de salvar a nota definitiva.

**Fora:**
- Sincronização externa com ferramentas de mapas mentais como Miro ou Figma.
- Publicação pública de notas de brainstorming na web.

## Critérios de aceite (Mínimo para /spec-task)
1. **AC1 — Captura Rápida Atômica (`Fleeting`):** Requisição para `/api/vault/ideas/fleeting` insere a ideia com timestamp e links sem quebrar o frontmatter nem sobrescrever anotações existentes em `zettelkasten/Fleeting.md` ou `inbox/`.
2. **AC2 — Criação de Sessão de Brainstorming Estruturada:** O endpoint `/api/vault/ideas/brainstorm` gera uma nota em `zettelkasten/` usando o template `templates/Brainstorming.md` com YAML válido contendo problema, hipóteses e campos de grilling.
3. **AC3 — Promoção e Transição de Estado:** A promoção para permanente ou projeto atualiza a nota de origem (status `promovida`) e cria/atualiza `zettelkasten/Permanente.md` ou `templates/Projetos.md` preservando backlinks.
4. **AC4 — Blocos de Generative UI no Frontend:** O Ionic Angular renderiza os blocos `idea-capture` e `brainstorm-canvas`, permitindo cadastrar ideias por voz ou texto e disparar a promoção com feedback visual imediato.

## Área de impacto esperada
- `backend/src/GranBrain.Core/Interfaces/IVaultService.cs`
- `backend/src/GranBrain.Core/Models/VaultModels.cs`
- `backend/src/GranBrain.Infrastructure/Vault/ObsidianVaultService.cs`
- `backend/src/GranBrain.Api/Endpoints/VaultEndpoints.cs`
- `backend/tests/GranBrain.UnitTests/**`
- `frontend/src/app/models/granbrain.models.ts`
- `frontend/src/app/components/generative-canvas/generative-canvas.component.ts`
- `frontend/src/app/app.component.ts`
- Cofre Obsidian: `zettelkasten/Fleeting.md`, `zettelkasten/Permanente.md`, `zettelkasten/Brainstorming.md`, `templates/Brainstorming.md`

## Decisões preliminares do dev
| ID | Resumo |
|---|---|
| D1 | Esteira em 3 fases: `Fleeting` (ideias rápidas brutas) ➔ `Brainstorming` (refinamento socrático com IA) ➔ `Permanente` ou `Projeto` (entrega madura). |
| D2 | Persistência sempre direta no Markdown do cofre, respeitando o `VaultPathGuard` (`Vault Jail`). |
| D3 | Uso nativo do `dataview` do cofre para agregar as sessões ativas na central `zettelkasten/Brainstorming.md`. |
