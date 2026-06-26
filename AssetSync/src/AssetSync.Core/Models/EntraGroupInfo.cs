namespace AssetSync.Core.Models;

/// <summary>Metadata about an Entra group, used by the UI to validate/cache the name and to guard
/// against writing to non-manually-writable (dynamic) groups. <see cref="Exists"/> is false when the
/// group is missing/deleted.</summary>
public class EntraGroupInfo
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool Exists { get; set; }
    public bool IsMembershipWritable { get; set; }
    public string? MembershipType { get; set; }
}
