namespace AssetSync.Core.Models;

/// <summary>A single seat on a Snipe-IT software license. A free seat has a null
/// <see cref="AssignedToUserId"/>.</summary>
public class LicenseSeat
{
    public int Id { get; set; }
    public int? AssignedToUserId { get; set; }
}
