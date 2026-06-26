using AssetSync.Core.Models;

namespace AssetSync.Core.Interfaces;

/// <summary>Reconciles Entra group membership with Snipe-IT license seats. Phase 1 handles only
/// read-only (Entra-authoritative) mappings: members are assigned seats and absentees are checked in
/// (subject to the grace-period and circuit-breaker guardrails).</summary>
public interface ILicenseGroupSyncEngine
{
    /// <summary>Run every license (each license reconciled over all its groups; one halting never
    /// blocks others).</summary>
    Task<LicenseGroupSyncSummary> RunAsync(bool dryRun, CancellationToken cancellationToken = default);

    /// <summary>Re-run all groups of a single license — the per-line "Rerun" re-runs the whole
    /// license reconcile (read union + the single write group).</summary>
    Task<IReadOnlyList<LicenseGroupMappingResult>> RunLicenseAsync(int licenseId, bool dryRun, CancellationToken cancellationToken = default);

    /// <summary>Run a single mapping. A single-group license behaves exactly as before; a read group
    /// is reconciled as a one-element union.</summary>
    Task<LicenseGroupMappingResult> RunMappingAsync(GroupLicenseMapping mapping, bool dryRun, CancellationToken cancellationToken = default);
}
