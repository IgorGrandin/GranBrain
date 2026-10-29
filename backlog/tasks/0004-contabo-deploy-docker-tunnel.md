# 0004 — Autonomia 24/7: Deploy na VPS Contabo, Docker, Domínio e Cloudflare Tunnel

## Objetivo
Publicar o sistema **GranBrain** (.NET 10 Web API + Frontend Ionic Angular) no servidor VPS do desenvolvedor na Contabo com domínio próprio, tornando o assistente e o cofre acessíveis 24 horas por dia de qualquer dispositivo, com segurança de nível de produção e sincronização contínua com o Google Drive.

## Contexto
Discutido na concepção do sistema (conversa `8cc6b830-ec40-4eae-b8a7-d22515cab2fe`): o usuário possui um servidor VPS na Contabo e um domínio próprio registrado. Atualmente o GranBrain opera apenas localmente. Para que o orquestrador Multi-IA e a captura funcionem sem depender do PC principal ligado, o monorepo será containerizado e hospedado na Contabo, utilizando Cloudflare Tunnel (ou proxy reverso Nginx com SSL/TLS) para proteção e HTTPS.

## Escopo
**Dentro:**
- Containerização do Monorepo:
  - `Dockerfile` multi-stage para compilação e publicação do backend `.NET 10`.
  - `Dockerfile` / Nginx conf para servir a SPA Ionic Angular compilada em produção.
  - `docker-compose.yml` orquestrando API, Frontend e volumes de persistência.
- Configuração de Domínio e Conectividade:
  - Integração com o domínio do usuário via Cloudflare Tunnel (`cloudflared`) ou Nginx com Certbot (Let's Encrypt).
  - Sem necessidade de abrir portas inseguras diretamente no firewall da VPS.
- Sincronização de Dados com o Google Drive:
  - Configuração do daemon `rclone` ou Google Drive API na VPS para manter o diretório do cofre (`VaultPath`) em sincronia bidirecional com o Google Drive do usuário.
- Segurança de Produção:
  - Carregamento de segredos via variáveis de ambiente (`GRANBRAIN__AUTH__JWTSECRET`, senha de admin alterada, remoção de valores padrão locais).

**Fora:**
- Configuração de clusters Kubernetes (uma stack simples com Docker Compose é suficiente para o MVP pessoal).
- Banco de dados relacional externo (o cofre Obsidian em disco continua sendo a única fonte da verdade).

## Critérios de aceite (Mínimo para /spec-task)
1. **AC1 — Docker Compose Operacional:** `docker compose up -d` sobe a API .NET 10 e o Frontend Ionic Angular com healthcheck positivo e consumo controlado de memória na VPS.
2. **AC2 — Acesso Seguro via Domínio Próprio:** O app é acessível externamente via HTTPS com certificado válido no domínio configurado do usuário.
3. **AC3 — Sincronização Contínua do Cofre:** Alterações no cofre na VPS são replicadas no Google Drive (e vice-versa) sem corromper arquivos Markdown.
4. **AC4 — Segurança e Isolamento de Segredos:** A aplicação em produção não inicia com os segredos padrão do repositório, exigindo variáveis de ambiente válidas.

## Área de impacto esperada
- `Dockerfile` (backend e frontend)
- `docker-compose.yml`
- `deploy/nginx/**` ou `deploy/cloudflare/**`
- `deploy/scripts/setup-vps.sh`
- `backend/src/GranBrain.Api/appsettings.Production.json`
- `CLAUDE.md` e documentação de deploy

## Decisões preliminares do dev
| ID | Resumo |
|---|---|
| D1 | Uso de Cloudflare Tunnel como primeira opção de exposição da VPS, garantindo proteção DDoS e eliminando necessidade de IP público exposto. |
| D2 | `rclone` operando em modo sync seguro para sincronizar o cofre da VPS com a nuvem Google Drive. |
| D3 | Submódulo de build em Angular otimizado para produção (`npm run build`). |
