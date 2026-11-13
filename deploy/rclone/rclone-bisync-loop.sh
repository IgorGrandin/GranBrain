#!/bin/sh
set -u

LOCAL_VAULT="${LOCAL_VAULT:-/data/vault}"
RCLONE_REMOTE="${RCLONE_REMOTE:-gdrive:GranBrainVault}"
STATE_DIR="/var/run/granbrain"
LOCK_FILE="/var/run/granbrain/bisync.lock"
INIT_MARKER="/var/run/granbrain/.bisync-initialized"
STATUS_FILE="/var/run/granbrain/sync-status.json"
TMP_STATUS_FILE="/var/run/granbrain/sync-status.json.tmp"
SYNC_INTERVAL="${RCLONE_SYNC_INTERVAL_SECONDS:-120}"

mkdir -p "$LOCAL_VAULT" "$STATE_DIR"

write_status() {
    status_value="$1"
    synced_at_json="$2"
    printf '{"status":"%s","lastSyncedAtUtc":%s}\n' "$status_value" "$synced_at_json" > "$TMP_STATUS_FILE"
    mv -f "$TMP_STATUS_FILE" "$STATUS_FILE"
}

run_bisync_cycle() {
    resync_arg=""
    if [ ! -f "$INIT_MARKER" ]; then
        resync_arg="--resync"
    fi

    if rclone bisync "$LOCAL_VAULT" "$RCLONE_REMOTE" \
        $resync_arg \
        --resilient \
        --conflict-resolve newer \
        --create-empty-src-dirs \
        --filter "- .granbrain-*" \
        --filter "- .obsidian/workspace*"; then
        touch "$INIT_MARKER"
        write_status "ok" "\"$(date -u +%Y-%m-%dT%H:%M:%SZ)\""
    else
        write_status "error" "null"
    fi
}

if [ ! -f "$STATUS_FILE" ]; then
    write_status "pending" "null"
fi

while true; do
    (
        flock -n 9 || exit 0
        run_bisync_cycle
    ) 9>"$LOCK_FILE"

    sleep "$SYNC_INTERVAL"
done
