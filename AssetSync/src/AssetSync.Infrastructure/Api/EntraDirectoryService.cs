using AssetSync.Core.Interfaces;
using AssetSync.Core.Models;
using Azure.Identity;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Kiota.Authentication.Azure;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace AssetSync.Infrastructure.Api;

/// <summary>
/// Reads Entra (Azure AD) group membership via Microsoft Graph, using the same app registration as
/// <see cref="IntuneService"/> (Phase 1 needs only the read scopes GroupMember.Read.All +
/// User.ReadBasic.All). The write members (Phase 2) are stubbed and throw until wired.
/// </summary>
public class EntraDirectoryService : IEntraDirectoryService
{
    private const int PageSize = 999;
    private readonly string _tenantId;
    private readonly string _clientId;
    private readonly Func<string> _getClientSecret;

    public EntraDirectoryService(string tenantId, string clientId, Func<string> getClientSecret)
    {
        _tenantId = tenantId;
        _clientId = clientId;
        _getClientSecret = getClientSecret;
    }

    private Microsoft.Graph.GraphServiceClient CreateClient()
    {
        var credential = new ClientSecretCredential(_tenantId, _clientId, _getClientSecret());
        var authProvider = new AzureIdentityAuthenticationProvider(credential, scopes: new[] { "https://graph.microsoft.com/.default" });
        var adapter = new HttpClientRequestAdapter(authProvider);
        return new Microsoft.Graph.GraphServiceClient(adapter);
    }

    public async Task<IReadOnlyList<EntraUser>> GetGroupMembersAsync(string groupId, CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        var list = new List<EntraUser>();

        // Any failed page fetch throws (Graph SDK behavior) — a partial enumeration must surface as a
        // thrown FAILED read, never be returned as a short/empty list.
        var page = await client.Groups[groupId].TransitiveMembers.GetAsync(c => c.QueryParameters.Top = PageSize, cancellationToken).ConfigureAwait(false);
        CollectUsers(page, list);

        while (page?.OdataNextLink != null)
        {
            page = await client.Groups[groupId].TransitiveMembers.WithUrl(page.OdataNextLink!).GetAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            CollectUsers(page, list);
        }

        return list;
    }

    private static void CollectUsers(DirectoryObjectCollectionResponse? page, List<EntraUser> list)
    {
        if (page?.Value == null) return;
        // transitiveMembers returns mixed directory objects — keep only users.
        foreach (var obj in page.Value)
        {
            if (obj is User u)
                list.Add(new EntraUser
                {
                    Id = u.Id ?? "",
                    UserPrincipalName = u.UserPrincipalName,
                    Mail = u.Mail,
                    DisplayName = u.DisplayName
                });
        }
    }

    public async Task<EntraGroupInfo> GetGroupInfoAsync(string groupId, CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        try
        {
            var g = await client.Groups[groupId].GetAsync(c =>
                c.QueryParameters.Select = new[] { "id", "displayName", "groupTypes" }, cancellationToken).ConfigureAwait(false);
            if (g == null)
                return new EntraGroupInfo { Id = groupId, Exists = false };

            var isDynamic = g.GroupTypes?.Any(t => string.Equals(t, "DynamicMembership", StringComparison.OrdinalIgnoreCase)) == true;
            return new EntraGroupInfo
            {
                Id = g.Id ?? groupId,
                DisplayName = g.DisplayName ?? "",
                Exists = true,
                IsMembershipWritable = !isDynamic,
                MembershipType = isDynamic ? "Dynamic" : "Assigned"
            };
        }
        catch (ODataError ex) when (ex.ResponseStatusCode == 404)
        {
            return new EntraGroupInfo { Id = groupId, Exists = false };
        }
    }

    // --- Phase 2 (write direction) — NOT YET WIRED. Requires GroupMember.ReadWrite.All. ---

    public Task<string?> ResolveUserObjectIdAsync(string? upn, string? email, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Entra user resolution is part of the Phase 2 write path and is not wired yet.");

    public Task<bool> AddGroupMemberAsync(string groupId, string userObjectId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Writing Entra group membership is the Phase 2 write path and is not wired yet.");

    public Task<bool> RemoveGroupMemberAsync(string groupId, string userObjectId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Removing Entra group membership is the Phase 2 write path and is not wired yet.");
}
