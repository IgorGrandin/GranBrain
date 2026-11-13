namespace GranBrain.Core.Models;

public sealed record SystemHealthStatusDto(
    string Status,
    string Service,
    string Environment,
    bool VaultAccessible,
    string TunnelStatus,
    string SyncStatus,
    string? LastSyncedAtUtc);

public sealed record RcloneSyncStatePayload(
    string? Status,
    string? LastSyncedAtUtc);
