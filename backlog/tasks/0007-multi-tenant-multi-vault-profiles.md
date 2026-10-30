# 0007 — Multi-Tenant & Multi-Vault: Perfis de Usuário e Isolamento Seguro por Cofre

## Objetivo
Evoluir a arquitetura do GranBrain para suportar múltiplos perfis/usuários (Multi-Tenant), onde cada pessoa (ex.: o desenvolvedor e sua esposa) possui sua própria conta, seu próprio Cofre Obsidian no disco e suas próprias automações, com isolamento absoluto de dados (*Zero Cross-Tenant Leakage*), provisionamento de novos cofres baseado em blueprint limpo de templates e interface de gestão de perfis no frontend alinhada ao Claude Design.

## Contexto
O usuário deseja que sua esposa possa se beneficiar da mesma inteligência de IA, comandos de voz e organização em Markdown do GranBrain, porém com privacidade total: as notas diárias, tarefas, finanças e anotações pessoais de um não devem ser visíveis nem acessíveis pelo outro. Como o GranBrain já utiliza `VaultPathGuard` (`Vault Jail`) para confinamento estrito de caminhos, essa guarda pode ser resolvida dinamicamente com base nas credenciais JWT de quem está autenticado. O frontend expõe uma tela de gestão de perfis com cartões de isolamento e comutação segura.

## Diretriz de Planejamento de Frontend (/spec-frontend)
**Mandatório na execução de `/spec-task`:**
O planejamento do frontend desta task deve ser realizado utilizando a skill `/spec-frontend`, extraindo o recorte das classes de perfis e gerando a subseção `### Especificação visual` dentro de `## Escopo`:
- **Origem do Import:** `frontend/_imports/claude-design/granbrain-redesign/2026-10-30/original/granbrain-redesign.html`
- **Artefato Claude:** `https://claude.ai/artifact/1puqYFtE3guy1J6CyvDXHy`
- **Recorte Visual do Mockup:** Telas e componentes de Perfis & Multi-Tenant (`.prof`, `.prof-side`, `.isolate`, `.isolate .ico`, `.isolate p`, `.avatar.sm`, botões de troca de perfil e criação de novo cofre).
- **Elementos Visuais a Reconciliar:** Lista de perfis ativos com avatares e badges de cofre (`.prof`), card explicativo de isolamento de segurança (`.isolate`) e modal/painel de criação de perfil.

## Escopo
**Dentro:**
- Resolução Dinâmica de Cofre no Backend `.NET 10`:
  - Modelo de Perfil de Usuário (`UserProfile`: id, username, passwordHash, vaultRootPath, role).
  - O token JWT emitido em `/api/auth/login` carrega o identificador do perfil e o caminho raiz autorizado.
  - `VaultPathGuard` é injetado com escopo (`Scoped`) por requisição HTTP, ancorando a raiz do confinamento estritamente no cofre do usuário logado.
  - Tentativas de acessar caminhos de outro cofre disparam `VaultPathViolationException` (`vault_path_violation` -> HTTP 403).
- Provisionamento Não-Destrutivo de Novos Cofres (`VaultBlueprintService`):
  - Ao cadastrar um novo perfil, o sistema cria a pasta do novo cofre (ex.: `G:\Meu Drive\Softwares\Obsidian\Esposa`) e injeta:
    - As pastas padrão (`daily/`, `templates/`, `zettelkasten/`, `zettelkasten/Índices/`, `inbox/`, `assets/kanban/`).
    - Todos os templates profissionais sanitizados (`Reuniões.md`, `Projetos.md`, `Notas.md`, `Literatura.md`, `Brainstorming.md`, `Notas Diárias.md`).
    - `AGENTS.md` e `Home.md` personalizados.
    - **Zero cópia de dados:** nenhuma nota existente do usuário principal é clonada. O novo cofre nasce pronto, estruturado e 100% limpo.
- Confinamento do Orquestrador de IA:
  - As CLIs (`Codex`, `Claude`, `Gemini`) executam tendo como diretório de trabalho (`WorkingDirectory`) o cofre exclusivo do usuário autenticado.
- Frontend Ionic Angular (especificado via `/spec-frontend`):
  - Indicação do perfil ativo no cabeçalho do app com avatar e nome.
  - Tela/aba "Perfis & Cofres" com listagem de perfis (`.prof`), indicador de isolamento (`.isolate`) e modal para provisionar novo cofre de familiar.

**Fora:**
- Sistema de faturamento/SaaS comercial para cobrança por cartão (o foco é uso familiar e privado).
- Compartilhamento cruzado de notas entre cofres (o foco do Multi-Vault é o isolamento estrito).

## Critérios de aceite
1. **AC1 — Isolamento Rigoroso de Cofres:** O usuário A nunca consegue ler ou escrever arquivos no cofre do usuário B. O `VaultPathGuard` bloqueia e mapeia qualquer tentativa para HTTP 403 (`vault_path_violation`).
2. **AC2 — Provisionamento Automático de Novo Cofre (Blueprint):** Criar um novo usuário gera automaticamente no disco o cofre completo com a estrutura de templates e `Home.md` sem nenhum arquivo pessoal de outros usuários.
3. **AC3 — Contexto de IA Isolado:** Ao enviar prompts por chat ou voz, a CLI convocada opera sobre os arquivos e regras do cofre do usuário que está chamando.
4. **AC4 — Autenticação e Gestão de Perfis:** Login direciona para o cofre correto; usuário admin pode cadastrar novos perfis com seus respectivos caminhos de cofre.
5. **AC5 — Painel de Perfis no Frontend Conforme Mockup:** O app Angular renderiza a tela de gestão de perfis respeitando as classes `.prof` e `.isolate` do artefato Claude Design.

## Área de impacto esperada
- `backend/src/GranBrain.Core/Security/VaultPathGuard.cs`
- `backend/src/GranBrain.Core/Configuration/GranBrainOptions.cs`
- `backend/src/GranBrain.Infrastructure/Vault/VaultInitializer.cs`
- `backend/src/GranBrain.Infrastructure/Vault/ObsidianVaultService.cs`
- `backend/src/GranBrain.Infrastructure/Agents/AgentOrchestratorService.cs`
- `backend/src/GranBrain.Api/Endpoints/AuthEndpoints.cs`
- `backend/src/GranBrain.Api/Endpoints/ProfileEndpoints.cs`
- `backend/tests/GranBrain.UnitTests/**`
- `frontend/src/app/models/granbrain.models.ts`
- `frontend/src/app/app.component.ts`
- `frontend/src/app/components/**`

## Referências de arquitetura
- [CLAUDE.md](../../CLAUDE.md)
- [Artefato Claude Design](https://claude.ai/artifact/1puqYFtE3guy1J6CyvDXHy)
- [Import Local do Mockup](../../frontend/_imports/claude-design/granbrain-redesign/2026-10-30/original/granbrain-redesign.html)
- [Task 0001b: Redesign Frontend](./0001b-redesign-frontend-granbrain.md)

## Decisões preliminares do dev
| ID | Resumo |
|---|---|
| D1 | Cada usuário possui um diretório físico de cofre separado no disco (ex.: `.../Igor Grandin` e `.../Esposa`). |
| D2 | `VaultPathGuard` atua como barreira de segurança estrita por claim de JWT (`sub` / `vault_path`). |
| D3 | Templates e governança são copiados do Blueprint na inicialização, garantindo que novos cofres tenham o mesmo padrão profissional. |
| D4 | Frontend de gestão de perfis segue o layout `.prof`/`.isolate` do mockup via `/spec-frontend`. |
