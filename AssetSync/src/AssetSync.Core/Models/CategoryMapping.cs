namespace AssetSync.Core.Models;

public class CategoryMapping
{
    public int Id { get; set; }
    public string MdmDeviceType { get; set; } = string.Empty;
    public int SnipeItCategoryId { get; set; }
}
