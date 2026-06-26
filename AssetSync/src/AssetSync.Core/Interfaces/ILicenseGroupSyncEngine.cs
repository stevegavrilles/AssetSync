using AssetSync.Core.Models;

namespace AssetSync.Core.Interfaces;

/// <summary>Reconciles Entra group membership with Snipe-IT license seats. Phase 1 handles only
/// read-only (Entra-authoritative) mappings: members are assigned seats and absentees are checked in
/// (subject to the grace-period and circuit-breaker guardrails).</summary>
public interface ILicenseGroupSyncEngine
{
    /// <summary>Run every configured mapping (each independently, so one halting never blocks others).</summary>
    Task<LicenseGroupSyncSummary> RunAsync(bool dryRun, CancellationToken cancellationToken = default);

    /// <summary>Run a single mapping — used by the per-line "Rerun" affordance after a halt/error.</summary>
    Task<LicenseGroupMappingResult> RunMappingAsync(GroupLicenseMapping mapping, bool dryRun, CancellationToken cancellationToken = default);
}
