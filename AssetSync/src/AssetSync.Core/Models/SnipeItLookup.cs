namespace AssetSync.Core.Models;

/// <summary>Simple id + name pair used for Snipe-IT dropdowns.</summary>
public class SnipeItLookup
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public override string ToString() => $"{Name} (#{Id})";
}
