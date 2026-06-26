namespace AssetSync.Core.Models;

/// <summary>Grace-period / soft-delete state: a subject (Snipe-IT user id for read-only mappings)
/// that has gone absent from the authoritative side. Removal only happens once
/// <see cref="ConsecutiveMisses"/> reaches the configured grace count (default 2) across consecutive
/// successful syncs; a reappearance clears the row.</summary>
public class PendingRemoval
{
    public int MappingId { get; set; }
    public string SubjectKey { get; set; } = "";
    public int ConsecutiveMisses { get; set; }
    public DateTimeOffset FirstMissedUtc { get; set; }
}
