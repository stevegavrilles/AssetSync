using AssetSync.Core.Enums;
using AssetSync.Core.Interfaces;
using AssetSync.Core.Models;
using Microsoft.Extensions.Logging;
using LogLevel = AssetSync.Core.Enums.LogLevel;

namespace AssetSync.Core.Services;

/// <summary>
/// Reconciles Entra group membership with Snipe-IT license seats, PER LICENSE.
///
/// A license can own many groups. Read-only (Entra-authoritative) groups are reconciled together
/// over the UNION of their members: a user in ANY read group is assigned a seat; a seat is checked
/// in only when its user is absent from ALL of the license's read groups. The single write
/// (read_only=OFF, Snipe-authoritative) group provisions/deprovisions Entra membership from the
/// Snipe seat list.
///
/// Guardrails apply at the license level: complete-read gate (EVERY read group must enumerate
/// cleanly before any removal; for the write group the Snipe seat list is the authoritative read),
/// never-act-on-empty, a grace period (removal only after N consecutive successful absences) keyed
/// per (license, subject), and a per-license circuit breaker that halts the license if a single run
/// would remove more than the configured number of users.
///
/// Documented assumption (multi-group): a user is not simultaneously in a read group and the write
/// group of the same license, so the write desired set is simply the Snipe seat-holders (no
/// exclusion logic). A single-group license behaves exactly as the original Phase 1/2 design.
/// </summary>
public class LicenseGroupSyncEngine : ILicenseGroupSyncEngine
{
    private const int DefaultGraceSyncs = 2;
    private const int DefaultCircuitBreaker = 20;
    private const string MatchFieldEmail = "email";

    private readonly IEntraDirectoryService _entra;
    private readonly ISnipeItService _snipe;
    private readonly IMappingRepository _repo;
    private readonly IConfigRepository _config;
    private readonly ILogRepository _log;
    private readonly IWebhookService _webhook;
    private readonly ILogger<LicenseGroupSyncEngine> _logger;

    public LicenseGroupSyncEngine(
        IEntraDirectoryService entra,
        ISnipeItService snipe,
        IMappingRepository repo,
        IConfigRepository config,
        ILogRepository log,
        IWebhookService webhook,
        ILogger<LicenseGroupSyncEngine> logger)
    {
        _entra = entra;
        _snipe = snipe;
        _repo = repo;
        _config = config;
        _log = log;
        _webhook = webhook;
        _logger = logger;
    }

    public async Task<LicenseGroupSyncSummary> RunAsync(bool dryRun, CancellationToken cancellationToken = default)
    {
        var summary = new LicenseGroupSyncSummary { DryRun = dryRun, StartedAtUtc = DateTimeOffset.UtcNow };
        var mappings = await _repo.GetGroupLicenseMappingsAsync(cancellationToken).ConfigureAwait(false);
        // Reconcile per license, not per group: a license's read groups share one union + grace.
        foreach (var perLicense in mappings.GroupBy(m => m.SnipeItLicenseId))
        {
            var results = await ReconcileLicenseAsync(perLicense.Key, perLicense.ToList(), dryRun, cancellationToken).ConfigureAwait(false);
            summary.Mappings.AddRange(results);
        }
        summary.CompletedAtUtc = DateTimeOffset.UtcNow;
        return summary;
    }

    public async Task<IReadOnlyList<LicenseGroupMappingResult>> RunLicenseAsync(int licenseId, bool dryRun, CancellationToken cancellationToken = default)
    {
        var groups = (await _repo.GetGroupLicenseMappingsAsync(cancellationToken).ConfigureAwait(false))
            .Where(m => m.SnipeItLicenseId == licenseId).ToList();
        return await ReconcileLicenseAsync(licenseId, groups, dryRun, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LicenseGroupMappingResult> RunMappingAsync(GroupLicenseMapping mapping, bool dryRun, CancellationToken cancellationToken = default)
    {
        if (mapping.ReadOnly)
        {
            var results = await ReconcileReadAsync(mapping.SnipeItLicenseId, new[] { mapping }, dryRun, cancellationToken).ConfigureAwait(false);
            return results[0];
        }
        return await ReconcileWriteAsync(mapping.SnipeItLicenseId, mapping, dryRun, cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<LicenseGroupMappingResult>> ReconcileLicenseAsync(int licenseId, IReadOnlyList<GroupLicenseMapping> groups, bool dryRun, CancellationToken cancellationToken)
    {
        var results = new List<LicenseGroupMappingResult>();
        var readGroups = groups.Where(g => g.ReadOnly).ToList();
        var writeGroup = groups.FirstOrDefault(g => !g.ReadOnly); // at most one (enforced by the repo/index)

        if (readGroups.Count > 0)
            results.AddRange(await ReconcileReadAsync(licenseId, readGroups, dryRun, cancellationToken).ConfigureAwait(false));
        if (writeGroup != null)
            results.Add(await ReconcileWriteAsync(licenseId, writeGroup, dryRun, cancellationToken).ConfigureAwait(false));
        return results;
    }

    // ===== Read direction: Entra (union of read groups) -> Snipe seats =====

    private async Task<List<LicenseGroupMappingResult>> ReconcileReadAsync(int licenseId, IReadOnlyList<GroupLicenseMapping> readGroups, bool dryRun, CancellationToken cancellationToken)
    {
        var runId = Guid.NewGuid().ToString();
        var primaryName = readGroups[0].EntraGroupName;

        // 1. Union of matched Snipe user ids across ALL read groups; track complete-read.
        var users = await _snipe.GetUsersAsync(cancellationToken).ConfigureAwait(false);
        var userMappings = await _repo.GetUserMappingsAsync(cancellationToken).ConfigureAwait(false);
        var matchField = (await _config.GetAsync(ConfigKeys.LicenseUserMatchField, cancellationToken).ConfigureAwait(false))?.Trim().ToLowerInvariant();

        var desired = new HashSet<int>();
        var noMatch = 0;
        var completeRead = true;
        string? readError = null;
        foreach (var g in readGroups)
        {
            IReadOnlyList<EntraUser> members;
            try
            {
                members = await _entra.GetGroupMembersAsync(g.EntraGroupId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                completeRead = false;
                readError = $"Read of group '{g.EntraGroupName}' failed (incomplete) — removals skipped: {ex.Message}";
                await LogAsync(runId, LogLevel.Error, "license_error", g.EntraGroupName, false, readError, cancellationToken).ConfigureAwait(false);
                continue;
            }
            foreach (var member in members)
            {
                var uid = MatchUser(member, users, userMappings, matchField);
                if (uid == null)
                {
                    noMatch++;
                    await LogAsync(runId, LogLevel.Warning, "license_skip", g.EntraGroupName, true,
                        $"No matching Snipe-IT user for '{member.UserPrincipalName ?? member.Mail ?? member.Id}'", cancellationToken).ConfigureAwait(false);
                    continue;
                }
                desired.Add(uid.Value);
            }
        }

        // Incomplete read with nothing to assign -> stop (also avoids a needless seat read).
        if (!completeRead && desired.Count == 0)
            return await BuildReadResultsAsync(readGroups, LicenseGroupRunStatus.Error,
                readError ?? "Entra read failed — refusing to act.", 0, 0, 0, noMatch, 0, dryRun, cancellationToken).ConfigureAwait(false);

        // 2. Target seats.
        IReadOnlyList<LicenseSeat> seats;
        try
        {
            seats = await _snipe.GetLicenseSeatsAsync(licenseId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return await BuildReadResultsAsync(readGroups, LicenseGroupRunStatus.Error,
                $"Snipe-IT seat read failed: {ex.Message}", 0, 0, 0, noMatch, 0, dryRun, cancellationToken).ConfigureAwait(false);
        }

        // 3. Never act on the absence of data: a clean read yielding an empty union while seats are
        //    assigned would mass check-in — refuse.
        if (completeRead && desired.Count == 0 && seats.Any(s => s.AssignedToUserId.HasValue))
            return await BuildReadResultsAsync(readGroups, LicenseGroupRunStatus.Error,
                "No members across any read group for this license, but seats are assigned — refusing mass check-in (possible missing/renamed group or read failure).",
                0, 0, 0, noMatch, 0, dryRun, cancellationToken).ConfigureAwait(false);

        // 4. Assign phase (union; under-assign on a partial read is safe).
        var seatState = seats.Select(s => new LicenseSeat { Id = s.Id, AssignedToUserId = s.AssignedToUserId }).ToList();
        var heldBy = new HashSet<int>(seatState.Where(s => s.AssignedToUserId.HasValue).Select(s => s.AssignedToUserId!.Value));
        var assigned = 0;
        var noFreeSeat = 0;
        foreach (var userId in desired)
        {
            if (heldBy.Contains(userId)) continue;
            var free = seatState.FirstOrDefault(s => s.AssignedToUserId == null);
            if (free == null)
            {
                noFreeSeat++;
                await LogAsync(runId, LogLevel.Warning, "license_skip", primaryName, true,
                    $"No free seat on license {licenseId} for user {userId}", cancellationToken).ConfigureAwait(false);
                continue;
            }
            if (!dryRun)
                await _snipe.CheckoutSeatAsync(licenseId, free.Id, userId, cancellationToken).ConfigureAwait(false);
            free.AssignedToUserId = userId;
            heldBy.Add(userId);
            assigned++;
            await LogAsync(runId, LogLevel.Info, "license_assign", primaryName, true,
                $"{(dryRun ? "[DRY RUN] would assign" : "Assigned")} seat to user {userId}", cancellationToken).ConfigureAwait(false);
        }

        // 5. Removal phase — ONLY on a complete read of every group. Grace + breaker, keyed per license.
        if (!completeRead)
            return await BuildReadResultsAsync(readGroups, LicenseGroupRunStatus.Error,
                readError ?? "One or more read groups failed; assigns applied, removals skipped.",
                assigned, 0, 0, noMatch, noFreeSeat, dryRun, cancellationToken).ConfigureAwait(false);

        var grace = await GetIntConfigAsync(ConfigKeys.LicenseRemovalGraceSyncs, DefaultGraceSyncs, cancellationToken).ConfigureAwait(false);
        var breaker = await GetIntConfigAsync(ConfigKeys.LicenseRemovalCircuitBreaker, DefaultCircuitBreaker, cancellationToken).ConfigureAwait(false);

        var pending = (await _repo.GetPendingRemovalsAsync(licenseId, cancellationToken).ConfigureAwait(false))
            .ToDictionary(p => p.SubjectKey, StringComparer.Ordinal);

        // Candidate = a seat held by a user absent from the union of ALL read groups.
        var candidates = seats
            .Where(s => s.AssignedToUserId.HasValue && !desired.Contains(s.AssignedToUserId.Value))
            .ToList();
        var candidateKeys = new HashSet<string>(candidates.Select(s => s.AssignedToUserId!.Value.ToString()), StringComparer.Ordinal);
        var reappeared = pending.Keys.Where(k => !candidateKeys.Contains(k)).ToList();

        var prospective = candidates
            .Select(s =>
            {
                var key = s.AssignedToUserId!.Value.ToString();
                var newCount = (pending.TryGetValue(key, out var p) ? p.ConsecutiveMisses : 0) + 1;
                return (Seat: s, Key: key, NewCount: newCount);
            })
            .ToList();
        var wouldRemove = prospective.Where(p => p.NewCount >= grace).ToList();

        if (wouldRemove.Count > breaker)
        {
            var msg = $"Circuit breaker: {wouldRemove.Count} seat removals would exceed the per-license limit of {breaker}. Halted — no seats removed; pending state left intact. Investigate, then Rerun.";
            await LogAsync(runId, LogLevel.Error, "license_halt", primaryName, false, msg, cancellationToken).ConfigureAwait(false);
            await _webhook.SendConnectivityFailureNotificationAsync("License sync", $"License {licenseId}: {msg}", cancellationToken).ConfigureAwait(false);
            return await BuildReadResultsAsync(readGroups, LicenseGroupRunStatus.Halted, msg, assigned, 0, 0, noMatch, noFreeSeat, dryRun, cancellationToken).ConfigureAwait(false);
        }

        var checkedIn = 0;
        var pendingNew = 0;
        if (!dryRun)
        {
            foreach (var key in reappeared)
                await _repo.ClearPendingRemovalAsync(licenseId, key, cancellationToken).ConfigureAwait(false);
            foreach (var p in prospective)
            {
                if (p.NewCount >= grace)
                {
                    await _snipe.CheckinSeatAsync(licenseId, p.Seat.Id, cancellationToken).ConfigureAwait(false);
                    await _repo.ClearPendingRemovalAsync(licenseId, p.Key, cancellationToken).ConfigureAwait(false);
                    checkedIn++;
                    await LogAsync(runId, LogLevel.Info, "license_checkin", primaryName, true,
                        $"Checked in seat {p.Seat.Id} (user {p.Key}) — absent from all read groups for {p.NewCount} consecutive syncs", cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await _repo.UpsertPendingRemovalAsync(licenseId, p.Key, cancellationToken).ConfigureAwait(false);
                    pendingNew++;
                    await LogAsync(runId, LogLevel.Info, "license_pending", primaryName, true,
                        $"Seat {p.Seat.Id} (user {p.Key}) pending removal — miss {p.NewCount}/{grace}", cancellationToken).ConfigureAwait(false);
                }
            }
        }
        else
        {
            checkedIn = wouldRemove.Count;
            pendingNew = prospective.Count - wouldRemove.Count;
            foreach (var p in prospective)
                await LogAsync(runId, LogLevel.Info, "license_checkin", primaryName, true,
                    $"[DRY RUN] seat {p.Seat.Id} (user {p.Key}) miss {p.NewCount}/{grace}{(p.NewCount >= grace ? " — would check in" : " — would stay pending")}", cancellationToken).ConfigureAwait(false);
        }

        var okMsg = $"{assigned} assigned, {checkedIn} checked in, {pendingNew} pending, {noMatch} unmatched, {noFreeSeat} no-seat.";
        return await BuildReadResultsAsync(readGroups, LicenseGroupRunStatus.Ok, okMsg, assigned, checkedIn, pendingNew, noMatch, noFreeSeat, dryRun, cancellationToken).ConfigureAwait(false);
    }

    // One result per read group: counts on the first row (so the summary isn't inflated across the
    // license's group rows), all rows share the license-level status + message. Persists per group.
    private async Task<List<LicenseGroupMappingResult>> BuildReadResultsAsync(IReadOnlyList<GroupLicenseMapping> readGroups, LicenseGroupRunStatus status, string message, int assigned, int checkedIn, int pendingNew, int noMatch, int noFreeSeat, bool dryRun, CancellationToken ct)
    {
        var results = new List<LicenseGroupMappingResult>();
        for (int i = 0; i < readGroups.Count; i++)
        {
            var g = readGroups[i];
            var r = new LicenseGroupMappingResult
            {
                MappingId = g.Id,
                GroupName = g.EntraGroupName,
                Status = status,
                Assigned = i == 0 ? assigned : 0,
                CheckedIn = i == 0 ? checkedIn : 0,
                PendingNew = i == 0 ? pendingNew : 0,
                NoMatch = i == 0 ? noMatch : 0,
                NoFreeSeat = i == 0 ? noFreeSeat : 0,
                Message = message
            };
            results.Add(r);
            await PersistStatusAsync(g.Id, r, dryRun, ct).ConfigureAwait(false);
        }
        return results;
    }

    // ===== Write direction: Snipe seats -> the single Entra write group =====

    /// <summary>
    /// Write direction (read_only OFF, Snipe authoritative): add Snipe-licensed users to the Entra
    /// group and remove members who no longer hold a seat. Higher stakes than a Snipe check-in, so
    /// the same guardrails apply, with the Snipe seat list as the authoritative read — a
    /// partial/failed seat read must never drive Entra removals. Grace is keyed per license.
    /// </summary>
    private async Task<LicenseGroupMappingResult> ReconcileWriteAsync(int licenseId, GroupLicenseMapping mapping, bool dryRun, CancellationToken cancellationToken)
    {
        var runId = Guid.NewGuid().ToString();
        var result = new LicenseGroupMappingResult { MappingId = mapping.Id, GroupName = mapping.EntraGroupName };
        try
        {
            // 0. The group must exist and be membership-writable (not dynamic).
            EntraGroupInfo info;
            try
            {
                info = await _entra.GetGroupInfoAsync(mapping.EntraGroupId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return await FailAsync(runId, mapping, result, $"Entra group read failed: {ex.Message}", dryRun, cancellationToken).ConfigureAwait(false);
            }
            if (!info.Exists)
                return await FailAsync(runId, mapping, result, "Entra group not found / deleted — refusing to write membership.", dryRun, cancellationToken).ConfigureAwait(false);
            if (!info.IsMembershipWritable)
                return await FailAsync(runId, mapping, result, "Entra group membership is not writable (dynamic group) — refusing to write.", dryRun, cancellationToken).ConfigureAwait(false);

            // 1. Authoritative read: Snipe seat list. Complete-read gate.
            IReadOnlyList<LicenseSeat> seats;
            try
            {
                seats = await _snipe.GetLicenseSeatsAsync(licenseId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return await FailAsync(runId, mapping, result, $"Snipe-IT seat read failed (incomplete) — refusing to write Entra membership: {ex.Message}", dryRun, cancellationToken).ConfigureAwait(false);
            }
            var desiredSnipeUserIds = seats.Where(s => s.AssignedToUserId.HasValue).Select(s => s.AssignedToUserId!.Value).Distinct().ToList();

            // 2. Current Entra membership (to diff adds/removes). Thrown read = error.
            IReadOnlyList<EntraUser> currentMembers;
            try
            {
                currentMembers = await _entra.GetGroupMembersAsync(mapping.EntraGroupId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return await FailAsync(runId, mapping, result, $"Entra membership read failed — refusing to write: {ex.Message}", dryRun, cancellationToken).ConfigureAwait(false);
            }
            var currentObjectIds = new HashSet<string>(currentMembers.Select(m => m.Id), StringComparer.OrdinalIgnoreCase);

            // 3. Resolve each Snipe-licensed user to an Entra object id (by UPN, then email).
            var users = await _snipe.GetUsersAsync(cancellationToken).ConfigureAwait(false);
            var usersById = users.GroupBy(u => u.Id).ToDictionary(g => g.Key, g => g.First());
            var desiredObjectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var snipeUserId in desiredSnipeUserIds)
            {
                usersById.TryGetValue(snipeUserId, out var su);
                string? objectId = null;
                try
                {
                    objectId = await _entra.ResolveUserObjectIdAsync(su?.Username, su?.Email, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await LogAsync(runId, LogLevel.Warning, "license_skip", mapping.EntraGroupName, true,
                        $"Entra resolve failed for Snipe user {snipeUserId}: {ex.Message}", cancellationToken).ConfigureAwait(false);
                }
                if (string.IsNullOrEmpty(objectId))
                {
                    result.NoMatch++;
                    await LogAsync(runId, LogLevel.Warning, "license_skip", mapping.EntraGroupName, true,
                        $"No Entra user for Snipe user {snipeUserId} ('{su?.Username ?? su?.Email}')", cancellationToken).ConfigureAwait(false);
                    continue;
                }
                desiredObjectIds.Add(objectId);
            }

            // Never act on the absence of data: refuse to empty a non-empty group.
            if (desiredObjectIds.Count == 0 && currentObjectIds.Count > 0)
                return await FailAsync(runId, mapping, result, "Resolved desired membership is empty but the group has members — refusing mass removal (no seats assigned, or none resolved to Entra users).", dryRun, cancellationToken).ConfigureAwait(false);

            // 4. Add phase: desired users not currently in the group.
            foreach (var objectId in desiredObjectIds)
            {
                if (currentObjectIds.Contains(objectId)) continue;
                if (!dryRun)
                    await _entra.AddGroupMemberAsync(mapping.EntraGroupId, objectId, cancellationToken).ConfigureAwait(false);
                result.Added++;
                await LogAsync(runId, LogLevel.Info, "license_add", mapping.EntraGroupName, true,
                    $"{(dryRun ? "[DRY RUN] would add" : "Added")} member {objectId}", cancellationToken).ConfigureAwait(false);
            }

            // 5. Remove phase — grace + circuit breaker (directory removal is the destructive side).
            var grace = await GetIntConfigAsync(ConfigKeys.LicenseRemovalGraceSyncs, DefaultGraceSyncs, cancellationToken).ConfigureAwait(false);
            var breaker = await GetIntConfigAsync(ConfigKeys.LicenseRemovalCircuitBreaker, DefaultCircuitBreaker, cancellationToken).ConfigureAwait(false);

            var pending = (await _repo.GetPendingRemovalsAsync(licenseId, cancellationToken).ConfigureAwait(false))
                .ToDictionary(p => p.SubjectKey, StringComparer.Ordinal);

            var candidates = currentMembers.Where(m => !desiredObjectIds.Contains(m.Id)).ToList();
            var candidateKeys = new HashSet<string>(candidates.Select(m => m.Id), StringComparer.Ordinal);
            var reappeared = pending.Keys.Where(k => !candidateKeys.Contains(k)).ToList();

            var prospective = candidates
                .Select(m =>
                {
                    var key = m.Id;
                    var newCount = (pending.TryGetValue(key, out var p) ? p.ConsecutiveMisses : 0) + 1;
                    return (Member: m, Key: key, NewCount: newCount);
                })
                .ToList();
            var wouldRemove = prospective.Where(p => p.NewCount >= grace).ToList();

            if (wouldRemove.Count > breaker)
            {
                result.Status = LicenseGroupRunStatus.Halted;
                result.Message = $"Circuit breaker: {wouldRemove.Count} Entra group removals would exceed the per-license limit of {breaker}. Halted — no members removed; pending state left intact. Investigate, then Rerun.";
                await LogAsync(runId, LogLevel.Error, "license_halt", mapping.EntraGroupName, false, result.Message, cancellationToken).ConfigureAwait(false);
                await _webhook.SendConnectivityFailureNotificationAsync("License sync", $"Group '{mapping.EntraGroupName}': {result.Message}", cancellationToken).ConfigureAwait(false);
                await PersistStatusAsync(mapping.Id, result, dryRun, cancellationToken).ConfigureAwait(false);
                return result;
            }

            if (!dryRun)
            {
                foreach (var key in reappeared)
                    await _repo.ClearPendingRemovalAsync(licenseId, key, cancellationToken).ConfigureAwait(false);

                foreach (var p in prospective)
                {
                    if (p.NewCount >= grace)
                    {
                        await _entra.RemoveGroupMemberAsync(mapping.EntraGroupId, p.Key, cancellationToken).ConfigureAwait(false);
                        await _repo.ClearPendingRemovalAsync(licenseId, p.Key, cancellationToken).ConfigureAwait(false);
                        result.Removed++;
                        await LogAsync(runId, LogLevel.Info, "license_remove", mapping.EntraGroupName, true,
                            $"Removed member {p.Key} — absent from license for {p.NewCount} consecutive syncs", cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await _repo.UpsertPendingRemovalAsync(licenseId, p.Key, cancellationToken).ConfigureAwait(false);
                        result.PendingNew++;
                        await LogAsync(runId, LogLevel.Info, "license_pending", mapping.EntraGroupName, true,
                            $"Member {p.Key} pending removal — miss {p.NewCount}/{grace}", cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            else
            {
                result.Removed = wouldRemove.Count;
                result.PendingNew = prospective.Count - wouldRemove.Count;
                foreach (var p in prospective)
                    await LogAsync(runId, LogLevel.Info, "license_remove", mapping.EntraGroupName, true,
                        $"[DRY RUN] member {p.Key} miss {p.NewCount}/{grace}{(p.NewCount >= grace ? " — would remove" : " — would stay pending")}", cancellationToken).ConfigureAwait(false);
            }

            result.Status = LicenseGroupRunStatus.Ok;
            result.Message = $"{result.Added} added, {result.Removed} removed, {result.PendingNew} pending, {result.NoMatch} unresolved.";
            await PersistStatusAsync(mapping.Id, result, dryRun, cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "License-group write sync failed for mapping {MappingId}", mapping.Id);
            return await FailAsync(runId, mapping, result, ex.Message, dryRun, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<LicenseGroupMappingResult> FailAsync(string runId, GroupLicenseMapping mapping, LicenseGroupMappingResult result, string message, bool dryRun, CancellationToken ct)
    {
        result.Status = LicenseGroupRunStatus.Error;
        result.Message = message;
        await LogAsync(runId, LogLevel.Error, "license_error", mapping.EntraGroupName, false, message, ct).ConfigureAwait(false);
        await PersistStatusAsync(mapping.Id, result, dryRun, ct).ConfigureAwait(false);
        return result;
    }

    private async Task PersistStatusAsync(int mappingId, LicenseGroupMappingResult result, bool dryRun, CancellationToken ct)
    {
        // Don't persist status for transient mappings (id 0, e.g. unit tests / not-yet-saved) or dry runs.
        if (mappingId <= 0 || dryRun) return;
        await _repo.UpdateGroupLicenseRunStatusAsync(mappingId, result.StatusText,
            result.Status == LicenseGroupRunStatus.Ok ? null : result.Message, ct).ConfigureAwait(false);
    }

    private static int? MatchUser(EntraUser member, IReadOnlyList<SnipeItLookup> users, IReadOnlyList<UserMapping> userMappings, string? matchField)
    {
        if (matchField == MatchFieldEmail)
        {
            var key = (member.Mail ?? member.UserPrincipalName)?.Trim();
            if (!string.IsNullOrEmpty(key))
            {
                var u = users.FirstOrDefault(x => !string.IsNullOrEmpty(x.Email) && string.Equals(x.Email!.Trim(), key, StringComparison.OrdinalIgnoreCase));
                if (u != null) return u.Id;
            }
        }
        else // default: upn-to-username
        {
            var key = member.UserPrincipalName?.Trim();
            if (!string.IsNullOrEmpty(key))
            {
                var u = users.FirstOrDefault(x => !string.IsNullOrEmpty(x.Username) && string.Equals(x.Username!.Trim(), key, StringComparison.OrdinalIgnoreCase));
                if (u != null) return u.Id;
            }
        }

        // Fallback: explicit user_mappings override keyed by the Entra UPN.
        var upn = member.UserPrincipalName?.Trim();
        if (!string.IsNullOrEmpty(upn))
        {
            var um = userMappings.FirstOrDefault(x => string.Equals(x.MdmUserIdentifier.Trim(), upn, StringComparison.OrdinalIgnoreCase));
            if (um != null) return um.SnipeItUserId;
        }
        return null;
    }

    private async Task<int> GetIntConfigAsync(string key, int fallback, CancellationToken ct)
    {
        var raw = await _config.GetAsync(key, ct).ConfigureAwait(false);
        return int.TryParse(raw, out var v) && v > 0 ? v : fallback;
    }

    private async Task LogAsync(string runId, LogLevel level, string action, string groupName, bool success, string? detail, CancellationToken ct)
    {
        await _log.AppendAsync(new LogEntry
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Level = level,
            SourceSystem = SourceSystem.Application,
            Action = action,
            DeviceName = groupName,
            Success = success,
            ErrorDetail = detail,
            SyncRunId = runId
        }, ct).ConfigureAwait(false);
    }
}
