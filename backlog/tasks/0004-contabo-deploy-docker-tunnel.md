# 0004 — Autonomia 24/7: Deploy na VPS Contabo, Docker, Domínio e Cloudflare Tunnel

## Objetivo

Publicar o sistema **GranBrain** (`.NET 10 Web API` + `Frontend Ionic Angular`) no servidor VPS do desenvolvedor na Contabo com domínio próprio e separação por subdomínio (`app.<dominio>` para o frontend e `api.<dominio>` para a API), tornando o assistente e o cofre acessíveis 24 horas por dia via Cloudflare Tunnel sem portas públicas expostas na VPS, com guarda fail-fast de segredos em produção, sincronização bidirecional contínua com o Google Drive (`rclone bisync`) e indicadores visuais de conectividade/sincronia no shell fiéis ao redesign canônico.

## Contexto

O usuário possui um servidor VPS na Contabo e um domínio próprio registrado. Atualmente o GranBrain opera apenas localmente. Para que o orquestrador Multi-IA, a agenda e a captura funcionem 24/7 sem depender do PC principal ligado, o monorepo será containerizado (`api`, `frontend`, `cloudflared`, `vault-sync`) e publicado na Contabo replicando a topologia de subdomínios separados do GranHarmony (`app.<dominio>` e `api.<dominio>`) roteados pelo Cloudflare Tunnel na rede interna do Docker. No protótipo canônico aprovado ([granbrain-redesign.html](../../frontend/_imports/claude-design/granbrain-redesign/2026-10-30/original/granbrain-redesign.html)), o rodapé do rail lateral (`.rail-foot`) e o sistema de notificações (`.toasts` / `.toast`) compõem a superfície de telemetria de conexão do shell. Atualmente, `frontend/src/app/app.component.html` exibe apenas um badge estático `Ativo` em `.rail-foot` e `frontend/src/app/services/granbrain-api.service.ts` usa `baseUrl = '/api'` fixo.

#### Reconciliação com o design-system — 2026-11-13

Comparação entre [granbrain-redesign.html](../../frontend/_imports/claude-design/granbrain-redesign/2026-10-30/original/granbrain-redesign.html) (Export: `2026-10-30`, commit base `b85c6c187c84130f4672d98e0af37fabae55a425`) e [styles.scss](../../frontend/src/styles.scss):

| Classe / Padrão no Mockup | Local no Mockup | Status em `styles.scss` | Bucket | Token / Valor Reconciliado |
| :--- | :--- | :--- | :--- | :--- |
| `.rail-foot` | `granbrain-redesign.html:137` | Já implementado em `styles.scss:256` | A | `margin-top: auto; padding: 0 8px; font-size: 12.5px; color: var(--fg-3)` |
| `.user-chip` | `granbrain-redesign.html:152` | Já implementado em `styles.scss:270` | A | `display: flex; align-items: center; gap: 8px; padding: 3px 12px 3px 3px; border: 1px solid var(--line); border-radius: var(--radius-pill); font-size: 13px` |
| `.pill`, `.pill.ok`, `.pill.warn`, `.pill.danger` | `granbrain-redesign.html:177-183` | Já implementado em `styles.scss:159-165` | A | `--surface-2`, `--fg-2`, `--ok-soft`, `--ok`, `--warn-soft`, `--warn`, `--danger-soft`, `--danger`, `--radius-pill` |
| `.toasts`, `.toast`, `.toast.warn`, `.toast.danger` | `granbrain-redesign.html:254-261` | Já implementado em `styles.scss:223-234` | A | `--fg`, `--bg`, `--warn`, `--danger`, `--shadow`, `border-radius: 12px` |

## Escopo

### Especificação visual

Origem: [granbrain-redesign.html](../../frontend/_imports/claude-design/granbrain-redesign/2026-10-30/original/granbrain-redesign.html) (`:137`, `:152`, `:177-185`, `:254-261`, `:552`, `:563-566`, `:630`). Todos os estilos pertencem ao **Bucket A** (tokens `:root` e classes já existentes em `frontend/src/styles.scss` sem criação de novos tokens globais).

- **Rodapé do rail lateral (`.rail-foot`)**:
  - Container `.rail-foot` (`margin-top: auto; padding: 0 8px; font-size: 12.5px; color: var(--fg-3)`), estruturado em duas linhas compactas (`display: grid; gap: 6px`):
  - **Linha 1 — Estado do Cofre**: rótulo `.mono` (`font-size: 11.5px`) `Cofre: Igor Grandin` acompanhado do badge `[data-health-vault]`:
    - Online e cofre acessível (`vaultAccessible === true`): `.pill.ok` (`background: var(--ok-soft); color: var(--ok)`) com texto `Ativo`.
    - Online porém degradado (`status === 'degraded'` com cofre acessível): `.pill.warn` (`background: var(--warn-soft); color: var(--warn)`) com texto `Degradado`.
    - API inacessível ou `vaultAccessible === false`: `.pill.danger` (`background: var(--danger-soft); color: var(--danger)`) com texto `Offline`.
  - **Linha 2 — Conectividade Tunnel e Sincronia Google Drive**: container flex (`display: flex; align-items: center; justify-content: space-between; gap: 6px; flex-wrap: wrap`) com dois badges e botão sutil de atualização `[data-health-refresh]`:
    - Badge `[data-health-tunnel]`: `.pill.ok` com `HTTPS · Tunnel` quando `tunnelStatus === 'ok'`; `.pill.warn` com `Local` quando `tunnelStatus === 'local'`; `.pill.danger` com `Sem Rede` quando a chamada a `/api/health` falha.
    - Badge `[data-health-sync]`: `.pill.ok` com `Drive Sync` quando `syncStatus === 'ok'`; `.pill.warn` com `Sync Pendente` quando `syncStatus === 'pending'`; `.pill.danger` com `Erro Sync` quando `syncStatus === 'error'` ou API offline.
- **Toast de queda e reconexão de rede (`.toasts` / `.toast`)**:
  - O primeiro healthcheck bem-sucedido ao iniciar a sessão não emite toast.
  - Quando `refreshHealth()` falha (`isBackendOffline` transita de `false` para `true`), emite `.toast.warn` (`background: var(--warn); color: var(--bg)`) com a mensagem `"Conexão com a API perdida. Tentando reconectar..."`.
  - Quando uma verificação subsequente volta a responder com sucesso (`isBackendOffline` transita de `true` para `false`), emite `.toast` padrão (`ok`, `background: var(--fg); color: var(--bg)`) com a mensagem `"Conexão segura restabelecida com o cofre."`.

**Dentro:**
- Containerização multi-stage do monorepo:
  - `backend/Dockerfile` multi-stage (`mcr.microsoft.com/dotnet/sdk:10.0` -> `mcr.microsoft.com/dotnet/aspnet:10.0` com `curl` instalado para `HEALTHCHECK` local em `http://127.0.0.1:8080/api/health`).
  - `frontend/Dockerfile` multi-stage (`node:22-alpine` executando `npm ci && npm run build` -> `nginx:1.27-alpine` servindo `dist/granbrain-frontend` na porta interna `8080` com `deploy/nginx/default.conf` e `/docker-entrypoint.d/40-generate-runtime-config.sh`).
  - `docker-compose.yml` e `.env.example` orquestrando `api`, `frontend`, `cloudflared` e `vault-sync` na rede interna `granbrain-net`, sem publicar `ports:` de `api` ou `frontend` no host da VPS (`expose: ["8080"]`), com `mem_limit` explícito em cada serviço e volumes compartilhados `vault-data:/data/vault` e `sync-state:/var/run/granbrain`.
- Topologia Cloudflare Tunnel e Segurança de Borda (`D1`):
  - `deploy/cloudflare/config.yml.example` roteando `app.<dominio>` para `http://frontend:8080` e `api.<dominio>` para `http://api:8080`.
  - Injeção de `apiBaseUrl` em runtime no Angular via `assets/runtime-config.js` (`window.__GRANBRAIN_CONFIG__.apiBaseUrl`), normalizado em `GranBrainApiService` (removendo barras finais ou sufixo `/api` duplicado, com fallback `''` para `/api` em desenvolvimento e testes).
  - `UseForwardedHeaders` (`X-Forwarded-For`, `X-Forwarded-Proto` com `KnownIPNetworks`/`KnownNetworks` e `KnownProxies` limpos para a rede interna Docker) e resolução de IP real por `CF-Connecting-IP` / primeiro IP de `X-Forwarded-For` no `RateLimiter` (`login-policy`), além de CORS restrito em produção a `GranBrain:AllowedOrigins` (`https://app.<dominio>`).
- Sincronização Bidirecional com Google Drive (`D2`):
  - `deploy/rclone/rclone-bisync-loop.sh` executado pelo container `vault-sync` com lockfile (`/var/run/granbrain/bisync.lock`), bootstrap inicial seguro (`--resync` quando `/var/run/granbrain/.bisync-initialized` não existir) e execuções seguintes incrementais com `rclone bisync --resilient --conflict-resolve newer --create-empty-src-dirs`, excluindo `.granbrain-*` e `.obsidian/workspace*`, e gravando status JSON de forma atômica (`sync-status.json.tmp` -> `mv -f` -> `/var/run/granbrain/sync-status.json`).
  - Endpoint `GET /api/health` estendido com `SystemHealthStatusDto` (`status`, `service`, `environment`, `vaultAccessible`, `tunnelStatus`, `syncStatus`, `lastSyncedAtUtc`).
- Segurança e Isolamento de Segredos em Produção (`D3`):
  - `ProductionConfigurationValidator.ValidateOrThrow` invocado no início de `Program.cs` (antes de `VaultPathGuard` e `AddJwtBearer`), bloqueando o startup em `Production` com `InvalidOperationException` se `JwtSecret`, `DefaultPassword`/`PasswordHash`, `VaultPath` ou `AllowedOrigins` forem inválidos ou usarem valores default do repositório.
  - `backend/src/GranBrain.Api/appsettings.Production.json` com segredos vazios (`JwtSecret: ""`, `DefaultPassword: ""`), caminhos Linux (`/data/vault`, `/var/run/granbrain/sync-status.json`) e guarda em `AuthEndpoints.VerifyPassword` contra senha ou `DefaultPassword` vazia/nula.
  - Script `deploy/scripts/setup-vps.sh` para provisionamento e hardening da VPS Contabo (UFW permitindo apenas SSH, validação de `.env` sem segredos default e subida do `docker compose`).

**Fora:**
- Configuração de clusters Kubernetes ou banco de dados relacional externo.
- Abertura das portas HTTP/HTTPS (80/443/8080) diretamente no firewall público da VPS Contabo (todo o tráfego externo entra pelo Cloudflare Tunnel).

## Critérios de aceite

1. **AC1 — Docker Compose Operacional, Isolamento de Rede e Healthcheck Rico:**
   - `docker-compose.yml` define os 4 serviços (`api`, `frontend`, `cloudflared`, `vault-sync`) na rede `granbrain-net` com `mem_limit` configurado em todos os containers, sem publicar `ports:` no host para `api` ou `frontend` (apenas `expose: ["8080"]`), e montando os volumes `vault-data:/data/vault` e `sync-state:/var/run/granbrain` tanto em `api` quanto em `vault-sync`.
   - `backend/Dockerfile` compila/publica a API `.NET 10` em multi-stage e instala `curl` no estágio runtime para suportar o `HEALTHCHECK` em `http://127.0.0.1:8080/api/health`; `frontend/Dockerfile` compila a SPA Angular em produção e serve via Nginx em `8080` com `/healthz` e `Cache-Control: no-store` em `/index.html` e `/assets/runtime-config.js`.
   - `GET /api/health` retorna `200 OK` com `SystemHealthStatusDto` (`status`, `service`, `environment`, `vaultAccessible`, `tunnelStatus`, `syncStatus`, `lastSyncedAtUtc`).
2. **AC2 — Acesso Seguro via Subdomínios no Cloudflare Tunnel, ForwardedHeaders, CORS e Fidelidade Visual (`/spec-frontend`):**
   - `deploy/cloudflare/config.yml.example` roteia `app.<dominio>` para `http://frontend:8080` e `api.<dominio>` para `http://api:8080`.
   - `Program.cs` aplica `UseForwardedHeaders` preservando `X-Forwarded-Proto: https` e particiona o `RateLimiter` (`login-policy`) pelo header `CF-Connecting-IP` ou pelo primeiro IP de `X-Forwarded-For` (impedindo que falhas de login de um IP bloqueiem outros clientes atrás do mesmo container `cloudflared`), e restringe o CORS (incluindo preflight `OPTIONS` com `Authorization` e `Content-Type`) apenas às origens HTTPS de `GranBrain:AllowedOrigins` (normalizadas com `.TrimEnd('/')`).
   - No Angular, `frontend/src/index.html` carrega `assets/runtime-config.js` antes do bundle, `GranBrainApiService` normaliza `apiBaseUrl` (`https://api.dominio.com`, `https://api.dominio.com/` ou `https://api.dominio.com/api` -> `https://api.dominio.com/api`, e `''` -> `/api`), e `AppComponent` renderiza no `.rail-foot` os 3 badges `[data-health-vault]`, `[data-health-tunnel]` e `[data-health-sync]` (`.pill.ok`, `.pill.warn`, `.pill.danger`) e emite `.toast.warn` na perda de conexão e `.toast` (`ok`) apenas na transição de reconexão.
3. **AC3 — Sincronização Bidirecional Contínua do Cofre (`rclone bisync`) e Telemetria Atômica:**
   - `deploy/rclone/rclone-bisync-loop.sh` utiliza lock (`/var/run/granbrain/bisync.lock`) para impedir execuções concorrentes, executa bootstrap inicial com `--resync` somente quando o marcador `/var/run/granbrain/.bisync-initialized` não existir e `rclone bisync` incremental (`--resilient --conflict-resolve newer --create-empty-src-dirs --filter "- .granbrain-*" --filter "- .obsidian/workspace*"`) nos ciclos seguintes.
   - O script atualiza `/var/run/granbrain/sync-status.json` de forma atômica via arquivo temporário (`sync-status.json.tmp` + `mv -f`), e `GET /api/health` reflete `syncStatus` (`ok`, `pending`, `error`) e `lastSyncedAtUtc` sem falhar caso o arquivo ainda não exista ou contenha JSON inválido/truncado.
4. **AC4 — Segurança e Isolamento Fail-Fast de Segredos em Produção:**
   - `backend/src/GranBrain.Api/appsettings.Production.json` remove segredos default (`JwtSecret: ""`, `DefaultPassword: ""`) e define `VaultPath: "/data/vault"` e `SyncStatusFilePath: "/var/run/granbrain/sync-status.json"`.
   - Quando `ASPNETCORE_ENVIRONMENT=Production`, `ProductionConfigurationValidator.ValidateOrThrow` é executado antes de `VaultPathGuard` e `AddJwtBearer`, lançando `InvalidOperationException` se: (a) `JwtSecret` for vazio, menor que 32 caracteres ou igual a `"GranBrain-Super-Secret-Key-2026-Obsidian-AI-OS-Min32Chars!"`; (b) `PasswordHash` for vazio e `DefaultPassword` for vazio ou igual a `"GranBrain@2026"`; (c) `VaultPath` for vazio ou iniciar com `G:\Meu Drive`; (d) `AllowedOrigins` for vazio, contiver `*` ou URL que não seja `https://`.
   - `AuthEndpoints.VerifyPassword` retorna `false` imediatamente se `inputPassword` for vazio ou se tanto `PasswordHash` quanto `DefaultPassword` forem vazios/nulos.

## Área de impacto esperada

- `backend/Dockerfile`
- `frontend/Dockerfile`
- `docker-compose.yml` e `.env.example`
- `deploy/nginx/default.conf` e `deploy/nginx/40-generate-runtime-config.sh`
- `deploy/cloudflare/config.yml.example`
- `deploy/rclone/rclone-bisync-loop.sh`
- `deploy/scripts/setup-vps.sh`
- `backend/src/GranBrain.Core/Configuration/GranBrainOptions.cs`
- `backend/src/GranBrain.Core/Configuration/ProductionConfigurationValidator.cs`
- `backend/src/GranBrain.Core/Models/SystemHealthModels.cs`
- `backend/src/GranBrain.Api/Program.cs`
- `backend/src/GranBrain.Api/Endpoints/AuthEndpoints.cs`
- `backend/src/GranBrain.Api/appsettings.Production.json`
- `backend/tests/GranBrain.UnitTests/`
- `frontend/angular.json`
- `frontend/src/index.html` e `frontend/src/assets/runtime-config.js`
- `frontend/src/app/models/granbrain.models.ts`
- `frontend/src/app/services/granbrain-api.service.ts`
- `frontend/src/app/app.component.ts`, `frontend/src/app/app.component.html` e `frontend/src/app/app.spec.ts`

## Referências de arquitetura

- [CLAUDE.md](../../CLAUDE.md)
- [ADR-0001 — GranBrain Architecture](../../docs/adr/0001-granbrain-architecture.md)
- [granbrain-redesign.html](../../frontend/_imports/claude-design/granbrain-redesign/2026-10-30/original/granbrain-redesign.html)
- [Task 0001b: Redesign Frontend](./0001b-redesign-frontend-granbrain.md)

## Decisões do dev

| ID | Resumo | Data |
|---|---|---|
| D1 | Roteamento por hostname no Cloudflare Tunnel (`app.<dominio>` -> container Nginx `frontend:8080`, `api.<dominio>` -> container .NET `api:8080`) na rede interna do Docker sem portas publicadas na VPS, `apiBaseUrl` apontando para `https://api.<dominio>`, CORS restrito a `https://app.<dominio>` e `UseForwardedHeaders` + `CF-Connecting-IP`. | 2026-11-13 |
| D2 | Container dedicado `vault-sync` (`rclone/rclone`) no `docker-compose.yml` executando loop seguro com `rclone bisync` (`--resilient --conflict-resolve newer` + lockfile + bootstrap `--resync`), gravando status atômico em `/var/run/granbrain/sync-status.json` lido por `GET /api/health`. | 2026-11-13 |
| D3 | Validação fail-fast no startup em `Production` (`ProductionConfigurationValidator`) + `appsettings.Production.json` zerando segredos, bloqueando inicialização com `InvalidOperationException` se `JwtSecret`, senha admin, `VaultPath` ou `AllowedOrigins` usarem defaults ou forem inválidos. | 2026-11-13 |
| D4 | Reconciliação `/spec-frontend` 100% Bucket A em `styles.scss` com badges `[data-health-vault]`, `[data-health-tunnel]` e `[data-health-sync]` (`.pill.ok`/`.warn`/`.danger`) no `.rail-foot` + toast de queda/reconexão, e injeção de `apiBaseUrl` via `assets/runtime-config.js` no Nginx (fallback `''` para `/api` em dev/testes). | 2026-11-13 |

### D1 — Topologia de Subdomínios no Cloudflare Tunnel, Rede Interna Docker, CORS e ForwardedHeaders
- **Problema:** Definir como expor o frontend Angular e a API `.NET 10` na VPS Contabo sob o domínio próprio sem abrir portas públicas no firewall da VPS e sem quebrar o rate-limiting por IP nem o isolamento entre origens.
- **Decisão:** Replicar a separação por hostname usada no GranHarmony: o Cloudflare Tunnel (`cloudflared`) roteia `app.<dominio>` para `http://frontend:8080` (container Nginx servindo a SPA Angular) e `api.<dominio>` para `http://api:8080` (container da API `.NET 10`). Nenhum dos dois containers publica `ports:` no host da VPS (`expose: ["8080"]` apenas na rede interna `granbrain-net`). O Angular usa `apiBaseUrl` apontando para `https://api.<dominio>` em produção, a API permite no CORS de produção apenas `https://app.<dominio>`, e `Program.cs` configura `UseForwardedHeaders` (`X-Forwarded-For`, `X-Forwarded-Proto`) e leitura de `CF-Connecting-IP` / primeiro IP de `X-Forwarded-For` no `RateLimiter` (`login-policy`).
- **Motivo:** Mantém frontend e API desacoplados em subdomínios próprios sob proteção DDoS/TLS da Cloudflare, evita exposição de portas na Contabo e garante que o `RateLimiter` distinga clientes reais em vez de bloquear o IP interno do container `cloudflared`.
- **Alternativas descartadas:** Gateway único via `/api` no mesmo host do Nginx (descartado pelo dev por divergir do padrão GranHarmony); Nginx no host com portas 80/443 abertas no firewall (descartado por expor o IP público da VPS).
- **Impacto e dependências:** Afeta `docker-compose.yml`, `deploy/cloudflare/config.yml.example`, `backend/src/GranBrain.Api/Program.cs` e `frontend/src/app/services/granbrain-api.service.ts`.
- **Incertezas:** nenhuma.

### D2 — Sincronização Bidirecional `rclone bisync` em Container e Telemetria de Estado
- **Problema:** Garantir que alterações feitas no cofre Obsidian na VPS sejam replicadas no Google Drive e vice-versa (AC3) sem sobrescrever notas concorrentes e permitindo que a API e a UI saibam o estado da sincronização.
- **Decisão:** Incluir o serviço `vault-sync` (`rclone/rclone`) no `docker-compose.yml` montando os mesmos volumes `vault-data:/data/vault` e `sync-state:/var/run/granbrain` que a `api`, executando `deploy/rclone/rclone-bisync-loop.sh` com lockfile (`/var/run/granbrain/bisync.lock`), bootstrap inicial `--resync` quando `/var/run/granbrain/.bisync-initialized` não existir e `rclone bisync --resilient --conflict-resolve newer --create-empty-src-dirs` nos ciclos seguintes, gravando `/var/run/granbrain/sync-status.json` de forma atômica (`tmp` + `mv -f`) para leitura em `GET /api/health`.
- **Motivo:** `rclone bisync` preserva edições dos dois lados (ao contrário de `rclone sync` unidirecional), o container dedicado mantém toda a stack autossuficiente no `docker-compose.yml`, e a escrita atômica do JSON evita leitura truncada pela API.
- **Alternativas descartadas:** Timer `systemd` fora do Docker (descartado por espalhar estado fora do Compose); `rclone sync` unidirecional (descartado por violar a bidirecionalidade do AC3).
- **Impacto e dependências:** Afeta `docker-compose.yml`, `deploy/rclone/rclone-bisync-loop.sh` e `GET /api/health` em `Program.cs`.
- **Incertezas:** nenhuma.

### D3 — Guarda Fail-Fast de Segredos e Configuração em Produção (`ProductionConfigurationValidator`)
- **Problema:** Atualmente `GranBrainOptions.cs` e `appsettings.json` trazem `DefaultPassword = "GranBrain@2026"`, `JwtSecret` default público e `VaultPath` Windows (`G:\Meu Drive\...`), o que permitiria subir a imagem em produção com credenciais conhecidas se variáveis de ambiente fossem esquecidas (violando AC4).
- **Decisão:** Criar `ProductionConfigurationValidator.ValidateOrThrow` invocado no início de `Program.cs` (antes de `VaultPathGuard` e `AddJwtBearer`) quando `builder.Environment.IsProduction() == true`, lançando `InvalidOperationException` se `JwtSecret` for vazio/`<32` chars/igual ao default do repo, se `PasswordHash` for vazio e `DefaultPassword` for vazio ou igual a `"GranBrain@2026"`, se `VaultPath` for vazio ou iniciar com `G:\Meu Drive`, ou se `AllowedOrigins` não contiver apenas origens `https://` explícitas; adicionar `appsettings.Production.json` com segredos vazios e proteger `AuthEndpoints.VerifyPassword` contra senha ou `DefaultPassword` vazia/nula.
- **Motivo:** Cumpre estritamente o AC4 em produção sem quebrar o fluxo de desenvolvimento local (`Development`) nem os testes existentes.
- **Alternativas descartadas:** Remover defaults também de `Development` (descartado por quebrar a ergonomia local); emitir apenas warning no log (descartado por violar o AC4).
- **Impacto e dependências:** Afeta `GranBrainOptions.cs`, `ProductionConfigurationValidator.cs`, `Program.cs`, `AuthEndpoints.cs`, `appsettings.Production.json` e `ProductionSecurityTests.cs`.
- **Incertezas:** nenhuma.

### D4 — Especificação Visual (`/spec-frontend`) do `.rail-foot`, Toasts de Reconexão e `runtime-config.js`
- **Problema:** Reconciliar os indicadores de conectividade e sincronização no rodapé `.rail-foot` e os toasts de reconexão com os tokens do mockup `granbrain-redesign.html`, além de permitir configurar `https://api.<dominio>` no container Nginx sem recompilar o bundle Angular para cada domínio.
- **Decisão:** Reutilizar 100% os estilos Bucket A já presentes em `frontend/src/styles.scss` (`.rail-foot`, `.user-chip`, `.pill.ok`, `.pill.warn`, `.pill.danger`, `.toasts`, `.toast.warn`), renderizar os badges `[data-health-vault]`, `[data-health-tunnel]` e `[data-health-sync]` alimentados por `GET /api/health`, emitir `.toast.warn` quando a conexão cair e `.toast` (`ok`) apenas quando reconectar após queda, e injetar `window.__GRANBRAIN_CONFIG__.apiBaseUrl` via `assets/runtime-config.js` gerado no boot do container Nginx (`40-generate-runtime-config.sh`), com fallback `''` (`/api`) em desenvolvimento e testes.
- **Motivo:** Mantém fidelidade integral ao Claude Design sem criar tokens CSS novos e permite trocar o domínio via `.env` do `docker-compose.yml` sem novo build da imagem Angular.
- **Alternativas descartadas:** Hardcodar o domínio em `environment.prod.ts` (descartado por acoplar a imagem Docker a um domínio fixo); manter `.rail-foot` estático (descartado por não atender à diretriz visual da WI 0004).
- **Impacto e dependências:** Afeta `frontend/src/index.html`, `frontend/src/assets/runtime-config.js`, `frontend/angular.json`, `deploy/nginx/default.conf`, `deploy/nginx/40-generate-runtime-config.sh`, `granbrain.models.ts`, `granbrain-api.service.ts`, `app.component.ts`, `app.component.html` e `app.spec.ts`.
- **Incertezas:** nenhuma.
