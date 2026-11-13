#!/bin/sh
set -eu

TARGET_FILE="/usr/share/nginx/html/assets/runtime-config.js"
RAW_API_BASE_URL="${GRANBRAIN_API_BASE_URL:-}"
ESCAPED_API_BASE_URL=$(printf '%s' "$RAW_API_BASE_URL" | sed 's/\\/\\\\/g; s/"/\\"/g')

mkdir -p "$(dirname "$TARGET_FILE")"
cat <<EOF > "$TARGET_FILE"
window.__GRANBRAIN_CONFIG__ = {
  apiBaseUrl: "${ESCAPED_API_BASE_URL}"
};
EOF
