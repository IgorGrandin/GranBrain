#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
ENV_FILE="${REPO_ROOT}/.env"

if [[ ! -f "${ENV_FILE}" ]]; then
    echo "Erro: arquivo ${ENV_FILE} não encontrado. Copie .env.example para .env e preencha os segredos." >&2
    exit 1
fi

set -a
# shellcheck disable=SC1090
source "${ENV_FILE}"
set +a

if [[ -z "${GRANBRAIN_JWT_SECRET:-}" || ${#GRANBRAIN_JWT_SECRET} -lt 32 || "${GRANBRAIN_JWT_SECRET}" == REPLACE_WITH_* || "${GRANBRAIN_JWT_SECRET}" == "GranBrain-Super-Secret-Key-2026-Obsidian-AI-OS-Min32Chars!" ]]; then
    echo "Erro: GRANBRAIN_JWT_SECRET inválido, menor que 32 chars ou usando valor default/placeholder." >&2
    exit 1
fi

if [[ -z "${GRANBRAIN_PASSWORD_HASH:-}" || "${GRANBRAIN_PASSWORD_HASH}" == REPLACE_WITH_* || "${GRANBRAIN_PASSWORD_HASH}" == "GranBrain@2026" ]]; then
    echo "Erro: GRANBRAIN_PASSWORD_HASH inválido ou usando placeholder/default." >&2
    exit 1
fi

if [[ -z "${GRANBRAIN_ALLOWED_ORIGINS:-}" || "${GRANBRAIN_ALLOWED_ORIGINS}" != https://* || "${GRANBRAIN_ALLOWED_ORIGINS}" == *"*"* ]]; then
    echo "Erro: GRANBRAIN_ALLOWED_ORIGINS deve ser uma URL https:// explícita sem wildcard." >&2
    exit 1
fi

if [[ -z "${CLOUDFLARE_TUNNEL_TOKEN:-}" || "${CLOUDFLARE_TUNNEL_TOKEN}" == REPLACE_WITH_* ]]; then
    echo "Erro: CLOUDFLARE_TUNNEL_TOKEN deve ser configurado no .env." >&2
    exit 1
fi

if command -v ufw >/dev/null 2>&1; then
    ufw default deny incoming
    ufw default allow outgoing
    ufw allow OpenSSH
    ufw --force enable
fi

cd "${REPO_ROOT}"
docker compose up -d --build
