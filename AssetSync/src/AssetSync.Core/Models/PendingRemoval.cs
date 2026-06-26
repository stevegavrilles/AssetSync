namespace AssetSync.Core.Models;

/// <summary>Grace-period / soft-delete state: a subject (Snipe-IT user id for read-only mappings)
/// that has gone absent from the authoritative side. Removal only happens once
/// <see cref="ConsecutiveMisses"/> reaches the configured grace count (default 2) across consecutive
/// successful syncs; a reappearance clears the row.</summary>
public class PendingRemoval
{
    /// <summary>The Snipe-IT license the grace state belongs to. Keyed per license (not per group)
    /// so a user present in any sibling read group of the license is not counted as a miss.</summary>
    public int LicenseId { get; set; }
    public string SubjectKey { get; set; } = "";
    public int ConsecutiveMisses { get; set; }
    public DateTimeOffset FirstMissedUtc { get; set; }
}
