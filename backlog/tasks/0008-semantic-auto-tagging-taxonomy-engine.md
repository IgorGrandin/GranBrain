# 0008 — Motor de Auto-Tagging Semântico e Taxonomia Inteligente do Cofre

## Objetivo
Implementar no GranBrain um motor de extração semântica de tags e palavras-chave (*Zero-Esforço*) que analisa automaticamente qualquer anotação (seja falada por áudio, digitada no chat ou capturada na caixa de entrada), identifica intenções, projetos, entidades e tópicos, e os injeta normalizados no YAML frontmatter (`tags: [...]`) da nota no cofre Obsidian, disponibilizando endpoints de filtragem taxonômica e componentes de busca por tags no frontend Ionic.

## Contexto
O usuário possui múltiplos domínios de conhecimento e projetos em seu cofre Obsidian (como `GranBrain`, `GranHarmony`, `Confitec / Seguros`, `Fast MBA`, `CSharp / .NET`). Para cumprir a promessa de captura *Zero-Esforço*, o usuário não deve despender esforço cognitivo categorizando ou digitando tags manualmente no calor do momento (ex: dirigindo, em reuniões ou treinando). O GranBrain deve ler o conteúdo bruto da anotação, inferir com precisão as tags relevantes com base no vocabulário do cofre e manter uma taxonomia consistente, evitando dispersão ou duplicação de sinônimos.

## Diretriz de Planejamento de Frontend (/spec-frontend)
**Mandatório na execução de `/spec-task`:**
O planejamento do frontend desta task deve ser realizado utilizando a skill `/spec-frontend`, extraindo o recorte das classes de taxonomia e gerando a subseção `### Especificação visual` dentro de `## Escopo`:
- **Origem do Import:** `frontend/_imports/claude-design/granbrain-redesign/2026-10-30/original/granbrain-redesign.html`
- **Recorte Visual do Mockup:** Filtros de tags em chips interativos (`.chip-tag`, `.tag-cloud`, `.badge-accent`), feed de notas filtrado por tag e indicador visual de tags inferidas pela IA durante a captura rápida.
- **Elementos Visuais a Reconciliar:** Carrossel horizontal de tags populares com contador de ocorrências, chips com cores contextuais por categoria (ex: verde para `ideia`, azul para projetos como `granharmony` e roxo para conceitos técnicos), e badge discreto de "Tags sugeridas pela IA" com opção de remoção em um clique.

## Escopo
**Dentro:**
- Serviço de Auto-Tagging Semântico no Backend `.NET 10` (`ISemanticTaggingService`):
  - Análise semântica rápida via modelo leve configurado (`gpt-6-luna-medium`, `claude-haiku-5-5-medium`, `gemini-3.8-flash-medium`) com fallback determinístico local para evitar latência.
  - Extração em três eixos taxonômicos:
    1. **Tipo/Intenção:** `ideia`, `reuniao`, `estudo`, `tarefa`, `decisao`, `bug`, `melhoria`, `reflexao`.
    2. **Domínio/Projeto:** Identificação de projetos ativos e pastas do cofre (`granharmony`, `granbrain`, `confitec`, `csharp`, `mba`, `pessoal`).
    3. **Tópicos/Palavras-chave:** Conceitos específicos extraídos do texto (ex: `audio`, `latencia`, `bluetooth`, `arquitetura`, `jwt`).
  - Normalização estrita de tags para padrão Obsidian:
    - Tudo em minúsculo (`lowercase`).
    - Remoção de acentos e caracteres especiais (`grân-hârmôny` ➔ `granharmony`).
    - Espaços e hífens múltiplos convertidos para `kebab-case` (`captura rapida` ➔ `captura-rapida`).
  - Injeção atômica no frontmatter YAML da nota gerada:
    ```markdown
    ---
    type: ideia
    created: "2026-10-30"
    tags:
      - ideia
      - granharmony
      - audio
      - bluetooth
    ---
    ```
- Endpoints HTTP no Backend (`VaultEndpoints.cs`):
  - `GET /api/vault/tags`: lista todas as tags existentes no cofre com totalizador de notas vinculadas.
  - `GET /api/vault/notes?tag={tag}`: busca notas que contêm determinada tag no frontmatter.
  - `POST /api/vault/tags/extract`: endpoint utilitário que recebe um texto bruto e retorna a lista de tags inferidas em JSON.
- Frontend Ionic Angular (especificado via `/spec-frontend`):
  - Bloco de Generative UI `tag-cloud`: nuvem e barra de chips com as tags mais relevantes para filtragem dinâmica com um toque.
  - Exibição de tags sugeridas em tempo real abaixo do campo de captura de voz/texto.
  - Atualização do componente `GenerativeCanvasComponent` para filtrar blocos de notas ao clicar em uma tag.

**Fora:**
- Renomeação em massa destrutiva de notas antigas sem comando explícito do usuário.
- Alteração da sintaxe nativa de tags do Obsidian (mantém estritamente o padrão YAML `tags: [...]` e inline `#tag`).

## Critérios de aceite
1. **AC1 — Extração Semântica Precisa:** Ao submeter um texto como *"Ideia pro GranHarmony para calibrar áudio bluetooth"*, o serviço infere no mínimo as tags `ideia`, `granharmony` e `audio`.
2. **AC2 — Normalização Estrita Obsidian:** Nenhuma tag gerada contém acentos, maiúsculas ou caracteres inválidos (ex.: `#áudio` vira `audio`, `C#` vira `csharp`).
3. **AC3 — Frontmatter YAML Preservado:** A adição automática de tags não duplica nem corrompe blocos YAML existentes em notas do cofre.
4. **AC4 — Pesquisabilidade por Tag:** O endpoint `/api/vault/notes?tag=granharmony` retorna todas as notas marcadas com a respectiva tag.
5. **AC5 — Interatividade Visual no Frontend:** O app permite filtrar notas clicando nos chips de tags gerados na Generative UI, com layout e cores coerentes com a identidade visual do projeto.

## Área de impacto esperada
- `backend/src/GranBrain.Core/Interfaces/ISemanticTaggingService.cs`
- `backend/src/GranBrain.Core/Models/TagModels.cs`
- `backend/src/GranBrain.Infrastructure/Services/SemanticTaggingService.cs`
- `backend/src/GranBrain.Infrastructure/Vault/ObsidianVaultService.cs`
- `backend/src/GranBrain.Api/Endpoints/VaultEndpoints.cs`
- `backend/tests/GranBrain.UnitTests/**`
- `frontend/src/app/models/granbrain.models.ts`
- `frontend/src/app/components/generative-canvas/**`
- `frontend/src/app/app.component.ts`

## Referências de arquitetura
- [CLAUDE.md](../../CLAUDE.md)
- [AGENTS.md (Regras do Cofre)](file:///g:/Meu%20Drive/Softwares/Obsidian/Igor%20Grandin/AGENTS.md)
- [0002 — Motor de Brainstorming e Ideias](./0002-brainstorming-fleeting-vault-engine.md)
- [0005 — Rotinas Autônomas Noturnas](./0005-nightly-autonomous-worker-inbox-triage.md)
- [0006 — Mobile Capture PWA Walkie-Talkie](./0006-mobile-capture-telegram-pwa.md)

## Decisões preliminares do dev
| ID | Resumo |
|---|---|
| D-01 | **Estrutura Híbrida de Extração:** Usar um dicionário local de projetos e MOCs conhecidos em memória (instantâneo, 0ms) combinado com chamada ao modelo leve (`GPT 6 Luna`, `Claude Haiku`, `Gemini Flash`) para conceitos novos ou nuanced, garantindo resposta imediata no app. |
| D-02 | **Semântica Obsidian Pura:** As tags são escritas no formato padrão de lista YAML (`tags:\n  - tag1\n  - tag2`), permitindo compatibilidade direta com Dataview, Obsidian Search e Obsidian Graph View. |
