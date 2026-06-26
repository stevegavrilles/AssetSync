# Snipe-IT License ⇄ Entra Group Membership Sync — Implementation Plan

**Status:** Draft / design only (no feature code yet)
**Date:** 2026-06-25 (revised — finalized model + guardrails)
**Author:** Engineering

> **Revision note (finalized model).** Each group ↔ license mapping has a per-mapping **"Read only"**
> checkbox, **default ON** for newly created mappings:
> - **Read only ON (default):** Microsoft/Entra is authoritative. The app **never writes to Entra** —
>   it reads Entra group membership and reflects it into Snipe-IT (assigns/checks-in license seats).
>   Any change made on the Snipe side to a read-only mapping is overwritten from Entra on the next sync.
> - **Read only OFF:** Snipe-IT is authoritative. The app provisions/deprovisions **Entra group
>   membership** (the write direction requiring `GroupMember.ReadWrite.All`).
>
> Directory writes are therefore **opt-in** — you must explicitly turn Read only **OFF** for a mapping
> before the app will ever add/remove Entra group members. This supersedes the earlier "Read from
> Entra" switch; naming and semantics below are "Read only", default ON.

## Summary

Correlate a Snipe-IT software license with an Entra (Azure AD) security/M365 group and keep their
membership in sync. Each mapping runs in one of two directions, chosen by its **Read only** flag:

- **Read only ON (default, safe):** Entra is the source of truth; the app reflects group membership
  into Snipe-IT license seats and never touches the directory. This is the default posture and the
  natural first stage — populate and verify in Snipe-IT before granting any directory-write power.
- **Read only OFF (opt-in, powerful):** Snipe-IT is the source of truth; the app provisions and
  deprovisions Entra group membership, and group-based licensing / app-assignment in Entra
  provisions the account.

This reuses the app's established shape (Microsoft Graph auth + Snipe-IT HTTP client + SQLite
mapping tables + `SyncEngine` orchestration/logging/webhook). When a mapping is read-only it only
reads the Microsoft side; when read-only is off it **writes** Entra group membership.

Grounded building blocks it builds on:

- Graph auth: `IntuneService` uses `ClientSecretCredential` + the `.default` scope and pages on
  `OdataNextLink` ([src/AssetSync.Infrastructure/Api/IntuneService.cs](../src/AssetSync.Infrastructure/Api/IntuneService.cs)).
- Snipe-IT client: `SnipeItService` — `HttpClient` + Bearer, limit/offset paging, retry-on-429,
  and "200-with-error-body" defensive parsing
  ([src/AssetSync.Infrastructure/Api/SnipeItService.cs](../src/AssetSync.Infrastructure/Api/SnipeItService.cs)).
- Mapping pattern: `SqliteMappingRepository` + `DatabaseInitializer`; the **Mappings** tab
  (`Views/MappingsView.xaml` + `ViewModels/MappingsViewModel.cs`).
- Orchestration/logging/webhook: `SyncEngine` + `WebhookService` + the `logs` table.

## Scope

Per-mapping **Read only** flag (default ON) selects the single authoritative side; reconciliation
(including removal) always flows from the authoritative source to the subordinate side. A mapping
only ever writes its subordinate side — the authoritative side is read-only to this app — so no
mapping reconciles both directions, and no user can be removed on one side and re-added from the
other within the same mapping.

- **Read only ON → Entra authoritative.** The Entra group drives the Snipe-IT license: members are
  assigned a free seat, and a user removed from the Entra group has their Snipe-IT seat **checked
  in**. The app writes **only Snipe-IT**; it never adds or removes Entra group members. Snipe-side
  edits to this mapping's license are overwritten from Entra next sync.
- **Read only OFF → Snipe authoritative.** The Snipe-IT seat list drives the Entra group: assigned
  users are **added** to the group, and a user removed from the license in Snipe-IT is **removed
  from the Entra group**. The app writes **Entra directory membership** (and never assigns/checks-in
  Snipe seats for this mapping). Requires `GroupMember.ReadWrite.All`.

**Design principle (settled — Open Decision #1).** One authoritative side per mapping, selected by
the Read only flag. The rejected alternative (a single mapping reconciling both directions
simultaneously) would let a removal on one side race the add logic on the other; it is not built.

Because read-only-OFF mappings write/remove **directory** group membership, and both directions
de-provision real users, the guardrails in [§7](#7-guardrails--never-act-on-the-absence-of-data) are
**mandatory** from day one.

## 1. Entra directory service (read always; write only when a mapping is read-only **OFF**)

Group membership read/write does **not** belong on the device-centric `IIntuneService`
(`GetManagedDevicesAsync`, `WriteBackAssetTagAsync`). Add a separate directory service.

**New file:** `src/AssetSync.Core/Interfaces/IEntraDirectoryService.cs`

```csharp
public interface IEntraDirectoryService
{
    /// <summary>Transitive user members (nested groups expanded) of an Entra group.
    /// Throws on any failed page fetch — a partial enumeration must surface as FAILED,
    /// never be returned as a short/empty list.</summary>
    Task<IReadOnlyList<EntraUser>> GetGroupMembersAsync(string groupId, CancellationToken ct = default);

    /// <summary>Resolve a Snipe-assigned user (by UPN, then email) to an Entra user object id.
    /// Returns null when no directory user matches (caller logs + skips).</summary>
    Task<string?> ResolveUserObjectIdAsync(string? upn, string? email, CancellationToken ct = default);

    /// <summary>Add a user to a group (idempotent — already-member is treated as success).
    /// POST /groups/{id}/members/$ref. Only used by read-only-OFF mappings.</summary>
    Task<bool> AddGroupMemberAsync(string groupId, string userObjectId, CancellationToken ct = default);

    /// <summary>Remove a user from a group. DELETE /groups/{id}/members/{userId}/$ref.
    /// Only used by read-only-OFF mappings.</summary>
    Task<bool> RemoveGroupMemberAsync(string groupId, string userObjectId, CancellationToken ct = default);

    /// <summary>Group metadata for the UI / safety checks: display name, whether membership is
    /// manually writable (e.g. not dynamic), and whether the group currently exists.</summary>
    Task<EntraGroupInfo> GetGroupInfoAsync(string groupId, CancellationToken ct = default);
}
```

**New models:**
- `src/AssetSync.Core/Models/EntraUser.cs` — `{ string Id; string? UserPrincipalName; string? Mail; string? DisplayName; }`.
- `src/AssetSync.Core/Models/EntraGroupInfo.cs` — `{ string Id; string DisplayName; bool Exists; bool IsMembershipWritable; string? MembershipType; }`.

**Implementation:** `src/AssetSync.Infrastructure/Api/EntraDirectoryService.cs`, constructed like
`IntuneService` (tenant/client id + `Func<string>` secret accessor), reusing the
`ClientSecretCredential` + `AzureIdentityAuthenticationProvider` + `GraphServiceClient` setup
([IntuneService.cs:40-43](../src/AssetSync.Infrastructure/Api/IntuneService.cs)). Optionally factor
the inline Graph client construction into a shared `GraphClientFactory`.

**Graph endpoints:**
- Read members: `GET /groups/{id}/transitiveMembers`, filter to `#microsoft.graph.user`, read
  `userPrincipalName` + `mail`, full `OdataNextLink` pagination.
- Resolve user: `GET /users/{upn}` then fall back to `GET /users?$filter=mail eq '...'`.
- **Add member (read-only OFF only):** `POST /groups/{id}/members/$ref` with body
  `{"@odata.id":"https://graph.microsoft.com/v1.0/directoryObjects/{userObjectId}"}`.
- **Remove member (read-only OFF only):** `DELETE /groups/{id}/members/{userObjectId}/$ref`.

**Behavior requirements:**
- **Failed-page = FAILED enumeration** (do not return partial member lists) — load-bearing for the
  complete-read gate and the never-act-on-empty rule (§7).
- **Missing/deleted group:** `GetGroupInfoAsync` returns `Exists = false`; the caller treats this as
  an ERROR state (hold + alert), never as "group is empty" (§7).
- **Idempotency:** add is a no-op if already a member; remove is a no-op if already absent.
- **Writability guard:** detect **dynamic-membership** (and other non-manually-writable) groups so a
  read-only-OFF mapping **refuses** to write rather than erroring per-user. Manual membership writes
  to dynamic groups are not permitted by Graph.

### Graph permissions (application, admin-consented)

Scope is tiered to the lowest-privilege set a deployment actually needs:

- **`GroupMember.Read.All`** + **`User.ReadBasic.All`** — sufficient for **read-only mappings**
  (the default): read group membership / `transitiveMembers` and resolve member UPN + mail
  (basic profile only; deliberately not `User.Read.All`).
- **`GroupMember.ReadWrite.All`** — required **only once a mapping is set Read only OFF**, i.e. when
  the app must add/remove directory group membership. **Writing directory membership is materially
  higher-stakes** — the stored app credential can then alter who is in a security/M365 group (and
  therefore who is licensed/provisioned). Because directory writes are opt-in, this scope can be
  withheld until a write mapping is genuinely needed.

The `GroupMember.ReadWrite.All` scope **must not be granted until the security prerequisite (§6) is
in place** — see sequencing in [§8](#8-phased-rollout--validation).

**Admin setup (no new app registration).** This feature reuses the **existing Intune app
registration and client-secret credential** (same tenant/client id + secret). The admin adds the
scopes above as **Application permissions** on that app registration and clicks **Grant admin
consent**. Until granted + consented, the feature's Graph calls fail at runtime and the affected
mapping shows an **error / halted** state (the rest of the app is unaffected). See also the README
"Graph API permissions" section.

## 2. Snipe-IT additions (`ISnipeItService` / `SnipeItService`)

Both directions touch Snipe-IT: read-only mappings **write** seats (assign/checkin) to mirror Entra;
read-only-OFF mappings **read** seat assignments to drive the Entra group. So we need read and write
of license seats.

Add to [src/AssetSync.Core/Interfaces/ISnipeItService.cs](../src/AssetSync.Core/Interfaces/ISnipeItService.cs)
and `SnipeItService`, reusing `SendWithRetryAsync` / `CreateClient`:

```csharp
Task<IReadOnlyList<SnipeItLookup>> GetLicensesAsync(CancellationToken ct = default);
Task<IReadOnlyList<LicenseSeat>>  GetLicenseSeatsAsync(int licenseId, CancellationToken ct = default);
Task<bool> CheckoutSeatAsync(int licenseId, int seatId, int snipeItUserId, CancellationToken ct = default);
Task<bool> CheckinSeatAsync(int licenseId, int seatId, CancellationToken ct = default);
```

- `GetLicensesAsync` — `GET /api/v1/licenses`, paged (template: `FetchLookupsAsync`). Returns
  `{Id, Name}` for name → license id.
- `GetLicenseSeatsAsync` — `GET /api/v1/licenses/{id}/seats`, paged. **New model**
  `src/AssetSync.Core/Models/LicenseSeat.cs` — `{ int Id; int? AssignedToUserId; }`. Free seat =
  `AssignedToUserId == null`. When a mapping is read-only OFF this list is the authoritative source,
  so its enumeration is subject to the same failed-page = FAILED rule.
- `CheckoutSeatAsync` / `CheckinSeatAsync` — `PATCH .../seats/{seatId}` with `{assigned_to: userId}`
  / `{assigned_to: null}`. Reuse the "200-with-error-body" parsing already in `CreateAssetAsync`
  ([SnipeItService.cs:120-135](../src/AssetSync.Infrastructure/Api/SnipeItService.cs)).

> ⚠️ **Validate the seat PATCH against the deployed Snipe-IT version before building on it.** The
> seat-update endpoint had a "method not allowed" bug in older Snipe-IT, and the exact path has
> varied — confirm whether the deployment uses singular `/api/v1/licenses/{id}/seat/{seatId}` or
> plural `/api/v1/licenses/{id}/seats/{seatId}`. Manually `curl`/Postman a PATCH against the target
> instance first; treat path/verb as version-specific config. Refs: snipe-it #11576, PR #8058.

### Extend `GetUsersAsync` / user model for matching (both directions)

Today `GetUsersAsync` returns only `{Id, Name}` — `username` is parsed but collapsed into `Name`,
and `mail` is never read
([SnipeItService.cs:189-194](../src/AssetSync.Infrastructure/Api/SnipeItService.cs);
[SnipeItLookup.cs](../src/AssetSync.Core/Models/SnipeItLookup.cs)). Matching needs username + email
exposed, in both directions:
- Read only ON: match an Entra member's UPN/mail to a Snipe-IT user id.
- Read only OFF: resolve a Snipe-assigned user's UPN/email to feed `ResolveUserObjectIdAsync`.

Add a dedicated `SnipeItUser` model (`{ int Id; string Username; string? Email; string DisplayName; }`)
rather than bloating the lightweight `SnipeItLookup` dropdown type, and parse `username` + `email`
in the `GetUsersAsync` row loop.

## 3. Data

### `group_license_mappings`

**DDL — add to `DatabaseInitializer.Initialize()`** alongside the other tables
([DatabaseInitializer.cs:46-85](../src/AssetSync.Infrastructure/Data/DatabaseInitializer.cs)):

```sql
CREATE TABLE IF NOT EXISTS group_license_mappings (
    id                 INTEGER PRIMARY KEY AUTOINCREMENT,
    entra_group_id     TEXT NOT NULL UNIQUE,
    entra_group_name   TEXT NOT NULL,              -- cached display name for the UI
    snipeit_license_id INTEGER NOT NULL,
    read_only          INTEGER NOT NULL DEFAULT 1, -- 1 = Entra authoritative (default, no directory writes);
                                                   -- 0 = Snipe authoritative (writes Entra membership)
    last_run_status    TEXT,                       -- e.g. ok | error | halted (for the per-line Rerun affordance)
    last_error         TEXT                        -- last failure/halt reason surfaced on the mapping's line
);
```

There is **no separate removal-enable toggle** — the single `read_only` flag governs whether a
mapping writes to Entra at all, and removal is intrinsic to reconciliation, bounded by the §7
guardrails (grace period + circuit breaker), not by a per-mapping on/off switch.

### `license_group_pending_removals` (grace-period / soft-delete state)

Tracks subjects absent from the authoritative side so removal only happens after **2 consecutive
successful syncs** (§7). A subject that reappears is cleared; misses are only counted on a
**successful (complete-read) sync**.

```sql
CREATE TABLE IF NOT EXISTS license_group_pending_removals (
    id                 INTEGER PRIMARY KEY AUTOINCREMENT,
    mapping_id         INTEGER NOT NULL,
    subject_key        TEXT NOT NULL,    -- Snipe user id (read-only ON) or Entra user object id (read-only OFF)
    consecutive_misses INTEGER NOT NULL DEFAULT 1,
    first_missed_utc   TEXT NOT NULL,
    UNIQUE(mapping_id, subject_key)
);
```

### Repository methods

**Bespoke methods (not the 2-column generic helper).** The generic `GetAllMappingsAsync`/
`SaveMappingAsync` assume exactly two data columns
([SqliteMappingRepository.cs:92-103, 175-196](../src/AssetSync.Infrastructure/Data/SqliteMappingRepository.cs)).
Add dedicated methods to `IMappingRepository` / `SqliteMappingRepository`:

```csharp
Task<IReadOnlyList<GroupLicenseMapping>> GetGroupLicenseMappingsAsync(CancellationToken ct = default);
Task SaveGroupLicenseMappingAsync(GroupLicenseMapping mapping, CancellationToken ct = default);
Task DeleteGroupLicenseMappingAsync(int id, CancellationToken ct = default);
Task UpdateMappingRunStatusAsync(int id, string status, string? error, CancellationToken ct = default);
// pending-removal state
Task<IReadOnlyList<PendingRemoval>> GetPendingRemovalsAsync(int mappingId, CancellationToken ct = default);
Task UpsertPendingRemovalAsync(int mappingId, string subjectKey, CancellationToken ct = default); // increments consecutive_misses
Task ClearPendingRemovalAsync(int mappingId, string subjectKey, CancellationToken ct = default);   // subject reappeared / removed
```

**New models:**
- `src/AssetSync.Core/Models/GroupLicenseMapping.cs` —
  `{ int Id; string EntraGroupId; string EntraGroupName; int SnipeItLicenseId; bool ReadOnly; string? LastRunStatus; string? LastError; }`.
- `src/AssetSync.Core/Models/PendingRemoval.cs` —
  `{ int MappingId; string SubjectKey; int ConsecutiveMisses; DateTime FirstMissedUtc; }`.

**Reuse existing `user_mappings` for fallback matching.** There is already a `user_mappings` table
+ `GetUserMappingAsync`/`SaveUserMappingAsync`
([SqliteMappingRepository.cs:35-52](../src/AssetSync.Infrastructure/Data/SqliteMappingRepository.cs))
mapping an arbitrary identifier → `snipeit_user_id`. Use it as the manual override/fallback when
automatic UPN/email matching fails (in either direction) rather than inventing a second mechanism.

## 4. UI: new "License Groups" module

A new screen/module (its own tab, or a sub-tab mirroring
`Views/MappingsView.xaml` + `ViewModels/MappingsViewModel.cs`):

- **Add an Entra group by ID**, then **select the correlated Snipe license from a dropdown**
  (populated via `GetLicensesAsync`). On add, resolve + cache the group display name via
  `GetGroupInfoAsync`, and surface a warning if the group is missing or **not membership-writable**
  (dynamic).
- **Per-mapping "Read only" checkbox** → `read_only`, **default ON** for new mappings. Tooltip:
  *ON = Entra is the source of truth; the app reflects membership into Snipe-IT seats and never
  writes to the directory. OFF = Snipe-IT is the source of truth; the app provisions/deprovisions
  Entra group membership.* Turning it **OFF is the explicit opt-in to directory writes** and should
  show a confirm noting it requires `GroupMember.ReadWrite.All`.
- **Per-line status + Rerun.** Each mapping row shows its `last_run_status`/`last_error`. A **"Rerun"
  button sits on the mapping's line and is ENABLED ONLY when that mapping is in the halted/error
  state** (e.g. the circuit breaker tripped). Clicking it re-runs **only that one mapping's process**,
  not the whole sync.
- Grid of mappings with add/edit/delete, following the existing pending-row/ComboBox binding
  conventions (`MappingsViewModel`, `PendingMappingRow`).

There is **no "enable removal" control** and **no log-only toggle** — removal is governed entirely by
the §7 grace period + circuit breaker.

DI registration mirrors the existing pattern in
[App.xaml.cs](../src/AssetSync.App/App.xaml.cs) and [Program.cs](../src/AssetSync.Service/Program.cs)
(register `IEntraDirectoryService` like `IIntuneService`, reading tenant/client id from config and
the secret from the credential store).

## 5. Sync flow

A new routine (a `LicenseGroupSyncEngine`, or an added pass in `SyncEngine`) reusing the existing
logging / webhook machinery. Runs **per mapping** (so one mapping halting never blocks others) and
branches on `read_only`. Can run on the existing cadence or as a separate scheduled job.

Every per-mapping run records a result (`ok` / `error` / `halted` + reason) via
`UpdateMappingRunStatusAsync`, which drives the per-line status and the Rerun affordance.

### 5a. Read only ON (default) — Entra authoritative (writes Snipe only)

1. `members = GetGroupMembersAsync(groupId)`. **Complete-read gate:** any failed page → record
   `error`, **skip the removal phase** this run (assignment may still proceed for members already
   read). **Never-act-on-empty:** if `GetGroupInfoAsync` reports missing/deleted/renamed, or the
   group returns zero members where it previously had members → record `error` (hold + alert), do
   **not** proceed to removals.
2. Match each member (UPN→username or email, per config; `user_mappings` fallback) to a Snipe user.
   No match → `no-matching-snipe-user` (skip + log).
3. **Assign phase:** if a matched user holds no seat, find a free seat (`AssignedToUserId == null`)
   and `CheckoutSeatAsync`. No free seat → `no-free-seat` (skip + log; optional webhook).
4. **Removal phase (grace + breaker):** any seat assigned to a user **not** in the Entra group is a
   *candidate*. On a successful complete read, increment its pending-removal miss count; a candidate
   reaching **2 consecutive misses** is checked in (`CheckinSeatAsync`) and cleared. Candidates that
   reappear are cleared without removal. **Circuit breaker:** if this run would check in **more than
   20** seats for this mapping, **halt the mapping** — perform no removals, record `halted` + reason
   on its line, leave pending state intact, and enable that line's Rerun.

The app writes **only Snipe-IT** in this direction; Snipe-side changes to the mapping's license are
overwritten from Entra on the next sync.

### 5b. Read only OFF — Snipe authoritative (writes Entra membership)

Requires `GroupMember.ReadWrite.All` and a membership-writable (non-dynamic) group.

1. `seats = GetLicenseSeatsAsync(licenseId)`; derive the desired set = Snipe user ids holding a seat.
   **Complete-read gate / never-act-on-empty:** failed page, or an empty seat list where the group
   previously had app-managed members → record `error`, skip removals.
2. For each assigned Snipe user, resolve UPN/email → `ResolveUserObjectIdAsync`. No Entra user found
   → `no-matching-entra-user` (skip + log).
3. **Add phase:** `AddGroupMemberAsync` for any desired user not already in the group (idempotent).
4. **Removal phase (grace + breaker):** any current Entra group member **not** in the desired Snipe
   set is a candidate; same **2-consecutive-successful-sync** grace before `RemoveGroupMemberAsync`,
   same **>20-per-mapping** circuit breaker that halts the mapping and enables its Rerun.

### User resolution rule (configurable)

Stored in `config` ([ConfigKeys.cs](../src/AssetSync.Core/ConfigKeys.cs)):
- **`LicenseUserMatchField`** — `upn-to-username` (default) or `email`.
- Fallback to `user_mappings` manual override; else record the appropriate `no-matching-*` outcome.
- Case-insensitive, trimmed. A match failure never triggers a counterpart removal.

**Logging / webhook reuse:** emit per-action rows into the `logs` table (reuse the `SyncRunSummary`
`Created/Updated/Skipped/Errors` shape) and fire `WebhookService` on error/halt conditions. The
app's existing manual dry-run remains available as an ad-hoc preview, but it is **not** a required
rollout gate — see §8.

## 6. Security prerequisite

**Confirmed current weakness (grounded):** `DpapiCredentialStore` encrypts with
`DataProtectionScope.LocalMachine` and **null entropy**
([DpapiCredentialStore.cs:20](../src/AssetSync.Infrastructure/Security/DpapiCredentialStore.cs)),
the DB lives at `C:\ProgramData\AssetSync\assetsync.db` (both
[App.xaml.cs:26-28](../src/AssetSync.App/App.xaml.cs) and
[Program.cs:24-26](../src/AssetSync.Service/Program.cs) use `CommonApplicationData`), and **no ACL
hardening** is applied — the file inherits ProgramData's default `Users: Read`. Net: any local user
can read the blob and DPAPI-decrypt it, recovering the Snipe-IT key, Intune secret, and Iru token.

This matters most for **read-only-OFF** mappings: once `GroupMember.ReadWrite.All` is granted, the
recoverable credential can rewrite Entra group membership tenant-wide.

**Constraint any fix must preserve:** LocalMachine scope is deliberate so the interactive WPF app
(writer) and the SYSTEM service (reader) can **both** decrypt the shared DB
([DpapiCredentialStore.cs:19](../src/AssetSync.Infrastructure/Security/DpapiCredentialStore.cs)); a
naive switch to `CurrentUser` breaks the service.

- **(6a) DB-file ACL hardening — MANDATORY before granting `GroupMember.ReadWrite.All`.** Restrict
  the ACL on `C:\ProgramData\AssetSync` (and the DB file) to SYSTEM + Administrators + the app's
  interactive user(s), removing inherited `Users: Read`. Apply a `DirectorySecurity` at DB-dir
  creation in both `App.OnStartup` and `Program.Main`. Cheap; gates enabling any write mapping.
  (Read-only operation already benefits, so do it up front regardless.)
- **(6b) Per-install entropy with safe key storage — IMPLEMENTED.** Per-install random entropy is
  mixed into `ProtectedData.Protect`/`Unprotect`, stored in a sibling, separately-ACL'd directory
  (`%ProgramData%\AssetSyncKeys`, not co-located with the DB), DPAPI scope kept LocalMachine so the
  app and SYSTEM service both decrypt; legacy no-entropy values are read via fallback and upgraded
  in place. See `FileDpapiEntropyProvider` / `DpapiCredentialStore`.

## 7. Guardrails — never act on the absence of data

These are **mandatory** and apply to whichever side a mapping writes (the authoritative side's read
drives that mapping's removals). The asymmetry: a bad read under the *add/assign* phase only
under-provisions and self-heals next run; under the *removal* phase it **over-removes** real access.
The decided guardrail set:

1. **Grace period / soft-delete (default = 2 successful syncs).** A user is removed/deprovisioned
   only after being absent from the authoritative side across **2 consecutive successful syncs**:
   the first miss records a *pending-removal* state, and actual removal happens on the second
   consecutive clean confirmation. A reappearance clears the pending state. Misses are counted
   **only on successful (complete-read) syncs** — a partial/failed read never advances the counter.
   Configurable via `LicenseRemovalGraceSyncs` (default `2`).
2. **Complete-read gate.** Run the removal phase only if the **full enumeration succeeded** on the
   relevant authoritative side (Entra members for read-only ON; Snipe seats for read-only OFF). Any
   partial/failed page → skip removals for that run; adds/assigns remain safe.
3. **Never act on empty / treat missing data as an error.** A missing / `404` / renamed / deleted
   group — or a zero-result where there were members before — is an **ERROR state: hold + alert**,
   never a mass-removal trigger.
4. **Circuit breaker — per mapping, absolute threshold = 20 users.** If a single mapping's run would
   remove more than **20** users, **HALT that mapping's process** (perform none of its removals) and
   surface the error on that mapping's line. The per-line **Rerun** button (enabled only in the
   halted/error state) re-runs **only that one mapping**. A genuinely large legitimate change is
   handled by raising that mapping's threshold for the rerun (config `LicenseRemovalCircuitBreaker`,
   default `20`) or by letting the grace period bleed it off over successive runs — never by a
   blanket override.
5. **Oscillation safety (by construction).** A mapping only ever writes its subordinate side; the
   authoritative side (selected by the Read only flag) is read-only to this app, so no mapping can
   remove a user on one side and have the other side re-add them.

> **No staged "would-remove" log-only rollout.** It is unnecessary here: the **read-only-first
> workflow *is* the staging** — a mapping runs Read only ON (Entra authoritative, no directory
> writes) to populate and verify license seats in Snipe-IT first, then Read only is turned OFF to
> enable directory provisioning. The grace period + circuit breaker provide the runtime safety
> margin in place of a log-only phase.

## 8. Phased rollout / validation

1. **Phase 0 — Security + external validation.** Land DB-file ACL hardening (6a). Validate the
   Snipe-IT seat PATCH path/verb against the deployed version (§2). Grant + admin-consent the
   **read** scopes (`GroupMember.Read.All` + `User.ReadBasic.All`). Defer `GroupMember.ReadWrite.All`.
2. **Phase 1 — Read + map.** Implement `IEntraDirectoryService` read methods, Snipe license/seat
   read+write, the `GetUsersAsync`/`SnipeItUser` extension, `group_license_mappings` +
   `license_group_pending_removals` + repo methods, and the UI module (new mappings default Read only
   ON). Validate enumeration/paging on a **large** group; verify counts match the Entra portal;
   verify missing/dynamic-group detection.
3. **Phase 2 — Read-only operation (this *is* the staging).** Map a pilot group **Read only ON** and
   confirm Snipe-IT seats populate correctly and idempotently. Exercise `no-matching-snipe-user` and
   `no-free-seat`. Confirm the grace period (a member removed from Entra goes pending, then checks in
   on the 2nd clean sync) and that a partial-read run does not advance removals.
4. **Phase 3 — Enable directory writes (opt-in).** After 6a, grant `GroupMember.ReadWrite.All`. Turn
   **Read only OFF** on one low-risk mapping; confirm adds to the Entra group and idempotency.
   Validate the circuit breaker (inject a >20 candidate set → mapping halts, Rerun enabled) and the
   never-act-on-empty error state. Expand mapping-by-mapping.
5. **Phase 4 — Durable security fix.** Per-install entropy with safe key storage (6b) before broad
   production rollout of write mappings.

## 9. Settings, defaults & remaining decisions

**Decided (locked):**

| Setting | Value |
|---------|-------|
| Per-mapping direction switch | **"Read only" checkbox, default ON** (Entra authoritative, no directory writes) |
| Directory writes | **Opt-in** — only when Read only is turned OFF (needs `GroupMember.ReadWrite.All`) |
| Authoritative side per mapping | **One side only** (Open Decision #1 = (A)); reconciliation flows source → subordinate |
| Removal grace period | **2 consecutive successful syncs** (`LicenseRemovalGraceSyncs`, default 2) |
| Circuit breaker | **Per mapping, absolute 20 users** (`LicenseRemovalCircuitBreaker`, default 20); halt + per-line Rerun |
| Removal enablement | **No separate toggle** — governed by Read only + grace + breaker |
| Staged log-only rollout | **None** — read-only-first is the staging |

**Remaining decisions (recommended defaults):**

| # | Decision | Recommended default | Notes |
|---|----------|---------------------|-------|
| 1 | User-match field | **`upn-to-username`** | Configurable; `email` alternative; `user_mappings` fallback. |
| 2 | No-matching-user (either direction) | **Skip + log** (optional webhook) | Never error the run; never remove the counterpart on a match failure. |
| 3 | Membership scope (Entra read) | **Transitive** | `/members` (direct-only) configurable. |
| 4 | Out-of-seats (read-only ON assign) | **Skip + log** | Optional webhook. |
| 5 | Cadence | **Separate scheduled job** | License/group churn differs from device sync. |
| 6 | Dynamic / non-writable Entra groups | **Refuse writes, warn in UI** | Detected via `GetGroupInfoAsync`; only affects read-only-OFF mappings. |
| 7 | Match casing | **Case-insensitive, trimmed** | — |

## 10. New/changed files (summary)

**New**
- `src/AssetSync.Core/Interfaces/IEntraDirectoryService.cs`
- `src/AssetSync.Core/Models/EntraUser.cs`, `EntraGroupInfo.cs`, `LicenseSeat.cs`, `GroupLicenseMapping.cs`, `PendingRemoval.cs`, `SnipeItUser.cs`
- `src/AssetSync.Infrastructure/Api/EntraDirectoryService.cs`
- `src/AssetSync.App/Views/` + `ViewModels/` for the License Groups module (per-line status + Rerun)
- `src/AssetSync.Core/Services/LicenseGroupSyncEngine.cs` (or a new pass in `SyncEngine`)
- `tests/` coverage: matching both directions, paging, free-seat selection, complete-read gate,
  grace-period (2-sync) state machine, circuit-breaker halt, never-act-on-empty, dynamic-group refusal

**Changed**
- `src/AssetSync.Core/Interfaces/ISnipeItService.cs` + `Api/SnipeItService.cs` — license/seat read+write; `GetUsersAsync` username/email
- `src/AssetSync.Core/Interfaces/IMappingRepository.cs` + `Data/SqliteMappingRepository.cs` — group-license + pending-removal + run-status methods
- `src/AssetSync.Infrastructure/Data/DatabaseInitializer.cs` — `group_license_mappings` + `license_group_pending_removals` DDL
- `src/AssetSync.Core/ConfigKeys.cs` — `LicenseUserMatchField`, `LicenseRemovalGraceSyncs`, `LicenseRemovalCircuitBreaker`, membership-scope, cadence keys
- `src/AssetSync.App/App.xaml.cs` + `src/AssetSync.Service/Program.cs` — DI for `IEntraDirectoryService`; **DB-dir ACL hardening (6a)**

## References

- Graph `transitiveMembers`: https://learn.microsoft.com/en-us/graph/api/group-list-transitivemembers
- Graph add member (`POST .../members/$ref`): https://learn.microsoft.com/en-us/graph/api/group-post-members
- Graph remove member (`DELETE .../members/{id}/$ref`): https://learn.microsoft.com/en-us/graph/api/group-delete-members
- Graph `GroupMember.Read.All` / `GroupMember.ReadWrite.All` / `User.ReadBasic.All` permissions reference: https://learn.microsoft.com/en-us/graph/permissions-reference
- Snipe-IT seat PATCH issue #11576: https://github.com/snipe/snipe-it/issues/11576
- Snipe-IT seats API PR #8058: https://github.com/snipe/snipe-it/pull/8058
