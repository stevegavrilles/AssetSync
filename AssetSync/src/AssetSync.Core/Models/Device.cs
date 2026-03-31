namespace AssetSync.Core.Models;

/// <summary>
/// Unified device model merged from Intune, Iru, and Snipe-IT.
/// Serial number is the match key (normalized).
/// </summary>
public class Device
{
    public string NormalizedSerial { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string? DeviceName { get; set; }
    public string? Model { get; set; }
    public int? SnipeItModelId { get; set; }
    public string? AssignedUserUpn { get; set; }
    public int? SnipeItAssignedUserId { get; set; }
    public string? OsVersion { get; set; }
    public string? WindowsFeatureUpdate { get; set; }
    public string? DeviceType { get; set; }
    public int? SnipeItCategoryId { get; set; }
    public string? PlatformSource { get; set; }
    public int? SnipeItAssetId { get; set; }
    public string? SnipeItAssetTag { get; set; }
    public string? AzureAdDeviceId { get; set; }
    public string? IruDeviceId { get; set; }
    public string? OperatingSystem { get; set; }
    /// <summary>Asset tag as reported by the MDM platform (Intune Notes or Kandji asset_tag field).</summary>
    public string? MdmAssetTag { get; set; }
    /// <summary>Raw Notes field from Intune — preserved so write-back can update the tag without clobbering other note content.</summary>
    public string? IntuneNotes { get; set; }
    /// <summary>Intune managed device ID (m.Id) — required for PATCH DeviceManagement.ManagedDevices[id]. Distinct from AzureAdDeviceId.</summary>
    public string? IntuneDeviceId { get; set; }
}
