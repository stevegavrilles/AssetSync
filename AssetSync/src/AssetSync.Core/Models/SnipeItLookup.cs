namespace AssetSync.Core.Models;

/// <summary>Simple id + name pair used for Snipe-IT dropdowns.</summary>
public class SnipeItLookup
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    // Populated for users (GetUsersAsync) to support Entra UPN/email matching; null for models/categories.
    public string? Username { get; set; }
    public string? Email { get; set; }
    public override string ToString() => $"{Name} (#{Id})";
}
