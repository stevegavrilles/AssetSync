namespace AssetSync.Core.Models;

public class UserMapping
{
    public int Id { get; set; }
    public string MdmUserIdentifier { get; set; } = string.Empty;
    public int SnipeItUserId { get; set; }
}
