using AssetSync.Core.Enums;
using AssetSync.Core.Interfaces;
using AssetSync.Core.Models;
using Microsoft.Extensions.Logging;
using LogLevel = AssetSync.Core.Enums.LogLevel;

namespace AssetSync.Core.Services;

/// <summary>
/// Phase 1 (read-only / Entra-authoritative) reconciliation of Entra group membership into Snipe-IT
/// license seats. For each read-only mapping: members are assigned free seats, and seats held by
/// users no longer in the group are checked in — but only behind the guardrails:
/// complete-read gate, never-act-on-empty, a grace period (removal only after N consecutive
/// successful absences), and a per-mapping circuit breaker that halts the mapping if a single run
/// would remove more than the configured number of users.
///
/// Read-only-OFF (Snipe-authoritative, writes Entra membership) mappings are intentionally NOT
/// processed here — that is the deferred Phase 2 write path.
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
        foreach (var mapping in mappings)
        {
            var result = await RunMappingAsync(mapping, dryRun, cancellationToken).ConfigureAwait(false);
            summary.Mappings.Add(result);
        }
        summary.CompletedAtUtc = DateTimeOffset.UtcNow;
        return summary;
    }

    public async Task<LicenseGroupMappingResult> RunMappingAsync(GroupLicenseMapping mapping, bool dryRun, CancellationToken cancellationToken = default)
    {
        var runId = Guid.NewGuid().ToString();
        var result = new LicenseGroupMappingResult { MappingId = mapping.Id, GroupName = mapping.EntraGroupName };

        // read_only OFF = Snipe authoritative -> the app WRITES Entra group membership (Phase 2).
        // Requires GroupMember.ReadWrite.All; only ever exercised on this branch.
        if (!mapping.ReadOnly)
            return await RunWriteMappingAsync(runId, mapping, result, dryRun, cancellationToken).ConfigureAwait(false);

        try
        {
            // 1. Read Entra members. Failed-page = thrown FAILED enumeration -> complete-read gate fails.
            IReadOnlyList<EntraUser> members;
            try
            {
                members = await _entra.GetGroupMembersAsync(mapping.EntraGroupId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return await FailAsync(runId, mapping, result, $"Entra group read failed (incomplete) — refusing to act: {ex.Message}", dryRun, cancellationToken).ConfigureAwait(false);
            }

            // 2. Never act on the absence of data: an empty/missing group is an error, never a mass-removal trigger.
            if (members.Count == 0)
                return await FailAsync(runId, mapping, result, "Entra group returned no members — refusing to assign or remove (possible missing/renamed group or read failure).", dryRun, cancellationToken).ConfigureAwait(false);

            // 3. Read the target license seats (defensive: a hard read failure is also an error).
            IReadOnlyList<LicenseSeat> seats;
            try
            {
                seats = await _snipe.GetLicenseSeatsAsync(mapping.SnipeItLicenseId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return await FailAsync(runId, mapping, result, $"Snipe-IT seat read failed: {ex.Message}", dryRun, cancellationToken).ConfigureAwait(false);
            }

            // 4. Resolve the desired set: Entra members matched to Snipe-IT user ids.
            var users = await _snipe.GetUsersAsync(cancellationToken).ConfigureAwait(false);
            var userMappings = await _repo.GetUserMappingsAsync(cancellationToken).ConfigureAwait(false);
            var matchField = (await _config.GetAsync(ConfigKeys.LicenseUserMatchField, cancellationToken).ConfigureAwait(false))?.Trim().ToLowerInvariant();

            var desired = new HashSet<int>();
            foreach (var member in members)
            {
                var snipeUserId = MatchUser(member, users, userMappings, matchField);
                if (snipeUserId == null)
                {
                    result.NoMatch++;
                    await LogAsync(runId, LogLevel.Warning, "license_skip", mapping.EntraGroupName, true,
                        $"No matching Snipe-IT user for '{member.UserPrincipalName ?? member.Mail ?? member.Id}'", cancellationToken).ConfigureAwait(false);
                    continue;
                }
                desired.Add(snipeUserId.Value);
            }

            // 5. Assign phase: grant a free seat to desired users who hold none. (Non-destructive.)
            // Local mutable copy so a seat assigned this run isn't handed out twice.
            var seatState = seats.Select(s => new LicenseSeat { Id = s.Id, AssignedToUserId = s.AssignedToUserId }).ToList();
            var heldBy = new HashSet<int>(seatState.Where(s => s.AssignedToUserId.HasValue).Select(s => s.AssignedToUserId!.Value));

            foreach (var userId in desired)
            {
                if (heldBy.Contains(userId)) continue;
                var free = seatState.FirstOrDefault(s => s.AssignedToUserId == null);
                if (free == null)
                {
                    result.NoFreeSeat++;
                    await LogAsync(runId, LogLevel.Warning, "license_skip", mapping.EntraGroupName, true,
                        $"No free seat on license {mapping.SnipeItLicenseId} for user {userId}", cancellationToken).ConfigureAwait(false);
                    continue;
                }
                if (!dryRun)
                    await _snipe.CheckoutSeatAsync(mapping.SnipeItLicenseId, free.Id, userId, cancellationToken).ConfigureAwait(false);
                free.AssignedToUserId = userId;
                heldBy.Add(userId);
                result.Assigned++;
                await LogAsync(runId, LogLevel.Info, "license_assign", mapping.EntraGroupName, true,
                    $"{(dryRun ? "[DRY RUN] would assign" : "Assigned")} seat to user {userId}", cancellationToken).ConfigureAwait(false);
            }

            // 6. Removal phase — grace period + circuit breaker.
            var grace = await GetIntConfigAsync(ConfigKeys.LicenseRemovalGraceSyncs, DefaultGraceSyncs, cancellationToken).ConfigureAwait(false);
            var breaker = await GetIntConfigAsync(ConfigKeys.LicenseRemovalCircuitBreaker, DefaultCircuitBreaker, cancellationToken).ConfigureAwait(false);

            var pending = (await _repo.GetPendingRemovalsAsync(mapping.Id, cancellationToken).ConfigureAwait(false))
                .ToDictionary(p => p.SubjectKey, StringComparer.Ordinal);

            // Candidates: seats currently assigned to a user no longer in the desired set.
            var candidates = seats
                .Where(s => s.AssignedToUserId.HasValue && !desired.Contains(s.AssignedToUserId.Value))
                .ToList();
            var candidateKeys = new HashSet<string>(candidates.Select(s => s.AssignedToUserId!.Value.ToString()), StringComparer.Ordinal);

            // Subjects that reappeared (were pending, now desired/absent-from-candidates) get cleared.
            var reappeared = pending.Keys.Where(k => !candidateKeys.Contains(k)).ToList();

            // Decide removals: a candidate whose miss count would reach the grace threshold this run.
            var prospective = candidates
                .Select(s =>
                {
                    var key = s.AssignedToUserId!.Value.ToString();
                    var newCount = (pending.TryGetValue(key, out var p) ? p.ConsecutiveMisses : 0) + 1;
                    return (Seat: s, Key: key, NewCount: newCount);
                })
                .ToList();
            var wouldRemove = prospective.Where(p => p.NewCount >= grace).ToList();

            // Circuit breaker: per-mapping absolute cap on removals in a single run.
            if (wouldRemove.Count > breaker)
            {
                result.Status = LicenseGroupRunStatus.Halted;
                result.Message = $"Circuit breaker: {wouldRemove.Count} seat removals would exceed the per-mapping limit of {breaker}. Halted — no seats removed; pending state left intact. Investigate, then Rerun.";
                await LogAsync(runId, LogLevel.Error, "license_halt", mapping.EntraGroupName, false, result.Message, cancellationToken).ConfigureAwait(false);
                await _webhook.SendConnectivityFailureNotificationAsync("License sync", $"Group '{mapping.EntraGroupName}': {result.Message}", cancellationToken).ConfigureAwait(false);
                await PersistStatusAsync(mapping.Id, result, dryRun, cancellationToken).ConfigureAwait(false);
                return result;
            }

            if (!dryRun)
            {
                foreach (var key in reappeared)
                    await _repo.ClearPendingRemovalAsync(mapping.Id, key, cancellationToken).ConfigureAwait(false);

                foreach (var p in prospective)
                {
                    if (p.NewCount >= grace)
                    {
                        await _snipe.CheckinSeatAsync(mapping.SnipeItLicenseId, p.Seat.Id, cancellationToken).ConfigureAwait(false);
                        await _repo.ClearPendingRemovalAsync(mapping.Id, p.Key, cancellationToken).ConfigureAwait(false);
                        result.CheckedIn++;
                        await LogAsync(runId, LogLevel.Info, "license_checkin", mapping.EntraGroupName, true,
                            $"Checked in seat {p.Seat.Id} (user {p.Key}) — absent for {p.NewCount} consecutive syncs", cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await _repo.UpsertPendingRemovalAsync(mapping.Id, p.Key, cancellationToken).ConfigureAwait(false);
                        result.PendingNew++;
                        await LogAsync(runId, LogLevel.Info, "license_pending", mapping.EntraGroupName, true,
                            $"Seat {p.Seat.Id} (user {p.Key}) pending removal — miss {p.NewCount}/{grace}", cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            else
            {
                result.CheckedIn = wouldRemove.Count;
                result.PendingNew = prospective.Count - wouldRemove.Count;
                foreach (var p in prospective)
                    await LogAsync(runId, LogLevel.Info, "license_checkin", mapping.EntraGroupName, true,
                        $"[DRY RUN] seat {p.Seat.Id} (user {p.Key}) miss {p.NewCount}/{grace}{(p.NewCount >= grace ? " — would check in" : " — would stay pending")}", cancellationToken).ConfigureAwait(false);
            }

            result.Status = LicenseGroupRunStatus.Ok;
            result.Message = $"{result.Assigned} assigned, {result.CheckedIn} checked in, {result.PendingNew} pending, {result.NoMatch} unmatched, {result.NoFreeSeat} no-seat.";
            await PersistStatusAsync(mapping.Id, result, dryRun, cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "License-group sync failed for mapping {MappingId}", mapping.Id);
            return await FailAsync(runId, mapping, result, ex.Message, dryRun, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Write direction (read_only OFF, Snipe authoritative): add Snipe-licensed users to the Entra
    /// group and remove members who no longer hold a seat. Removing a directory member is higher
    /// stakes than checking in a Snipe seat, so the same guardrails apply, with the AUTHORITATIVE
    /// read being the Snipe seat list — a partial/failed seat read must never drive Entra removals.
    /// </summary>
    private async Task<LicenseGroupMappingResult> RunWriteMappingAsync(string runId, GroupLicenseMapping mapping, LicenseGroupMappingResult result, bool dryRun, CancellationToken cancellationToken)
    {
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

            // 1. Authoritative read: Snipe seat list. Complete-read gate — a thrown/partial read must
            //    NOT lead to Entra removals.
            IReadOnlyList<LicenseSeat> seats;
            try
            {
                seats = await _snipe.GetLicenseSeatsAsync(mapping.SnipeItLicenseId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return await FailAsync(runId, mapping, result, $"Snipe-IT seat read failed (incomplete) — refusing to write Entra membership: {ex.Message}", dryRun, cancellationToken).ConfigureAwait(false);
            }
            var desiredSnipeUserIds = seats.Where(s => s.AssignedToUserId.HasValue).Select(s => s.AssignedToUserId!.Value).Distinct().ToList();

            // 2. Current Entra membership (needed to diff adds/removes). Thrown read = error.
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

            // Never act on the absence of data: refuse to empty a non-empty group. An empty resolved
            // desired set (no seats, or none resolved) must never mass-remove existing members.
            if (desiredObjectIds.Count == 0 && currentObjectIds.Count > 0)
                return await FailAsync(runId, mapping, result, "Resolved desired membership is empty but the group has members — refusing mass removal (no seats assigned, or none resolved to Entra users).", dryRun, cancellationToken).ConfigureAwait(false);

            // 4. Add phase: desired users not currently in the group. (Add is non-destructive.)
            foreach (var objectId in desiredObjectIds)
            {
                if (currentObjectIds.Contains(objectId)) continue;
                if (!dryRun)
                    await _entra.AddGroupMemberAsync(mapping.EntraGroupId, objectId, cancellationToken).ConfigureAwait(false);
                result.Added++;
                await LogAsync(runId, LogLevel.Info, "license_add", mapping.EntraGroupName, true,
                    $"{(dryRun ? "[DRY RUN] would add" : "Added")} member {objectId}", cancellationToken).ConfigureAwait(false);
            }

            // 5. Remove phase — grace period + circuit breaker (directory removal is the destructive side).
            var grace = await GetIntConfigAsync(ConfigKeys.LicenseRemovalGraceSyncs, DefaultGraceSyncs, cancellationToken).ConfigureAwait(false);
            var breaker = await GetIntConfigAsync(ConfigKeys.LicenseRemovalCircuitBreaker, DefaultCircuitBreaker, cancellationToken).ConfigureAwait(false);

            var pending = (await _repo.GetPendingRemovalsAsync(mapping.Id, cancellationToken).ConfigureAwait(false))
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
                result.Message = $"Circuit breaker: {wouldRemove.Count} Entra group removals would exceed the per-mapping limit of {breaker}. Halted — no members removed; pending state left intact. Investigate, then Rerun.";
                await LogAsync(runId, LogLevel.Error, "license_halt", mapping.EntraGroupName, false, result.Message, cancellationToken).ConfigureAwait(false);
                await _webhook.SendConnectivityFailureNotificationAsync("License sync", $"Group '{mapping.EntraGroupName}': {result.Message}", cancellationToken).ConfigureAwait(false);
                await PersistStatusAsync(mapping.Id, result, dryRun, cancellationToken).ConfigureAwait(false);
                return result;
            }

            if (!dryRun)
            {
                foreach (var key in reappeared)
                    await _repo.ClearPendingRemovalAsync(mapping.Id, key, cancellationToken).ConfigureAwait(false);

                foreach (var p in prospective)
                {
                    if (p.NewCount >= grace)
                    {
                        await _entra.RemoveGroupMemberAsync(mapping.EntraGroupId, p.Key, cancellationToken).ConfigureAwait(false);
                        await _repo.ClearPendingRemovalAsync(mapping.Id, p.Key, cancellationToken).ConfigureAwait(false);
                        result.Removed++;
                        await LogAsync(runId, LogLevel.Info, "license_remove", mapping.EntraGroupName, true,
                            $"Removed member {p.Key} — absent from license for {p.NewCount} consecutive syncs", cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await _repo.UpsertPendingRemovalAsync(mapping.Id, p.Key, cancellationToken).ConfigureAwait(false);
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
