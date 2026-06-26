using AssetSync.Core.Models;

namespace AssetSync.Core.Interfaces;

/// <summary>
/// Reads (and, in Phase 2, writes) Entra (Azure AD) group membership via Microsoft Graph.
/// Separate from the device-centric <see cref="IIntuneService"/>.
/// Phase 1 implements only the read surface; the write/resolve members marked "Phase 2" are
/// deliberately not wired yet (they require the higher-stakes <c>GroupMember.ReadWrite.All</c> scope).
/// </summary>
public interface IEntraDirectoryService
{
    /// <summary>Transitive user members (nested groups expanded) of an Entra group, filtered to
    /// users. Throws on any failed page fetch — a partial enumeration must surface as a thrown
    /// FAILED read, never be returned as a short/empty list.</summary>
    Task<IReadOnlyList<EntraUser>> GetGroupMembersAsync(string groupId, CancellationToken cancellationToken = default);

    /// <summary>Group metadata for the UI / safety checks. <see cref="EntraGroupInfo.Exists"/> is
    /// false when the group is missing/deleted.</summary>
    Task<EntraGroupInfo> GetGroupInfoAsync(string groupId, CancellationToken cancellationToken = default);

    // --- Phase 2 (write direction, Snipe -> Entra) — NOT YET WIRED ---

    /// <summary>Phase 2: resolve a Snipe-assigned user (by UPN, then email) to an Entra user object id.</summary>
    Task<string?> ResolveUserObjectIdAsync(string? upn, string? email, CancellationToken cancellationToken = default);

    /// <summary>Phase 2: add a user to a group (POST /groups/{id}/members/$ref).</summary>
    Task<bool> AddGroupMemberAsync(string groupId, string userObjectId, CancellationToken cancellationToken = default);

    /// <summary>Phase 2: remove a user from a group (DELETE /groups/{id}/members/{userId}/$ref).</summary>
    Task<bool> RemoveGroupMemberAsync(string groupId, string userObjectId, CancellationToken cancellationToken = default);
}
