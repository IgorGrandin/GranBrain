# 0007 — Multi-Tenant & Multi-Vault: Perfis de Usuário e Isolamento Seguro por Cofre

## Objetivo
Evoluir a arquitetura do GranBrain para suportar múltiplos perfis/usuários (Multi-Tenant), onde cada pessoa (ex.: o desenvolvedor e sua esposa) possui sua própria conta, seu próprio Cofre Obsidian no disco e suas próprias automações, com isolamento absoluto de dados (*Zero Cross-Tenant Leakage*) e provisionamento de novos cofres baseado em um blueprint limpo de templates profissionais.

## Contexto
O usuário deseja que sua esposa possa se beneficiar da mesma inteligência de IA, comandos de voz e organização em Markdown do GranBrain, porém com privacidade total: as notas diárias, tarefas, finanças e anotações pessoais de um não devem ser visíveis nem acessíveis pelo outro. Como o GranBrain já utiliza `VaultPathGuard` (`Vault Jail`) para confinamento estrito de caminhos, essa guarda pode ser resolvida dinamicamente com base nas credenciais JWT de quem está autenticado.

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
- Frontend Ionic Angular:
  - Indicação do perfil ativo no cabeçalho do app.
  - Tela simples de administração (acessível pelo admin) para criar e gerenciar perfis familiares.

**Fora:**
- Sistema de faturamento/SaaS comercial para cobrança por cartão (o foco é uso familiar e privado).
- Compartilhamento cruzado de notas entre cofres (pode ser desenhado futuramente se desejado, mas o MVP de Multi-Vault prioriza o isolamento estrito).

## Critérios de aceite (Mínimo para /spec-task)
1. **AC1 — Isolamento Rigoroso de Cofres:** O usuário A nunca consegue ler ou escrever arquivos no cofre do usuário B. O `VaultPathGuard` bloqueia e mapeia qualquer tentativa para HTTP 403 (`vault_path_violation`).
2. **AC2 — Provisionamento Automático de Novo Cofre (Blueprint):** Criar um novo usuário gera automaticamente no disco o cofre completo com a estrutura de templates e `Home.md` sem nenhum arquivo pessoal de outros usuários.
3. **AC3 — Contexto de IA Isolado:** Ao enviar prompts por chat ou voz, a CLI convocada opera sobre os arquivos e regras do cofre do usuário que está chamando.
4. **AC4 — Autenticação e Gestão de Perfis:** Login direciona para o cofre correto; usuário admin pode cadastrar novos perfis com seus respectivos caminhos de cofre.

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

## Decisões preliminares do dev
| ID | Resumo |
|---|---|
| D1 | Cada usuário possui um diretório físico de cofre separado no disco (ex.: `.../Igor Grandin` e `.../Esposa`). |
| D2 | `VaultPathGuard` atua como barreira de segurança estrita por claim de JWT (`sub` / `vault_path`). |
| D3 | Templates e governança são copiados do Blueprint na inicialização, garantindo que novos cofres tenham o mesmo padrão profissional. |
