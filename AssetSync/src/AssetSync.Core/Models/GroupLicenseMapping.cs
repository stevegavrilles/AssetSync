namespace AssetSync.Core.Models;

/// <summary>Correlates an Entra group with a Snipe-IT software license.
/// <para><see cref="ReadOnly"/> selects the single authoritative side:
/// ON (default) = Entra authoritative; the app reflects membership into Snipe-IT seats and never
/// writes the directory. OFF = Snipe authoritative; the app provisions/deprovisions Entra group
/// membership (the write direction — deferred to Phase 2, not yet wired).</para></summary>
public class GroupLicenseMapping
{
    public int Id { get; set; }
    public string EntraGroupId { get; set; } = "";
    public string EntraGroupName { get; set; } = "";
    public int SnipeItLicenseId { get; set; }
    public bool ReadOnly { get; set; } = true;
    public string? LastRunStatus { get; set; }
    public string? LastError { get; set; }
}
