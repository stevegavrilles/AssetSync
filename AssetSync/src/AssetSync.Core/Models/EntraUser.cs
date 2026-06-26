namespace AssetSync.Core.Models;

/// <summary>A user member of an Entra (Azure AD) group, as read via Microsoft Graph.</summary>
public class EntraUser
{
    public string Id { get; set; } = "";
    public string? UserPrincipalName { get; set; }
    public string? Mail { get; set; }
    public string? DisplayName { get; set; }
}
