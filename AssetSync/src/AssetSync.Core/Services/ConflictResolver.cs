using AssetSync.Core.Models;

namespace AssetSync.Core.Services;

/// <summary>
/// Snipe-IT wins for user-managed fields. MDM overwrites placeholder/default names.
/// </summary>
public class ConflictResolver
{
    // Snipe-IT default/placeholder names that should be overwritten by MDM
    private static readonly HashSet<string> PlaceholderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Awaiting Enrollment",
        "Pending Enrollment",
        "Unknown",
        "Unknown Device",
        "No Name",
        "New Asset"
    };

    /// <summary>
    /// Returns updates to apply: fills empty Snipe-IT fields and replaces placeholder names from MDM.
    /// </summary>
    public IReadOnlyDictionary<string, object?> GetUpdatesToApply(Device snipeItAsset, Device mdmDevice)
    {
        var updates = new Dictionary<string, object?>();

        // Overwrite if Snipe-IT name is empty OR a known placeholder
        if ((IsEmpty(snipeItAsset.DeviceName) || PlaceholderNames.Contains(snipeItAsset.DeviceName!)) && !IsEmpty(mdmDevice.DeviceName))
            updates["name"] = mdmDevice.DeviceName;
        if (IsEmpty(snipeItAsset.SerialNumber) && !IsEmpty(mdmDevice.SerialNumber))
            updates["serial"] = mdmDevice.SerialNumber;
        if (snipeItAsset.SnipeItModelId == null && mdmDevice.SnipeItModelId != null)
            updates["model_id"] = mdmDevice.SnipeItModelId;
        if (snipeItAsset.SnipeItAssignedUserId == null && mdmDevice.SnipeItAssignedUserId != null)
            updates["assigned_to"] = mdmDevice.SnipeItAssignedUserId;
        if (IsEmpty(snipeItAsset.OsVersion) && !IsEmpty(mdmDevice.OsVersion))
            updates["os_version"] = mdmDevice.OsVersion;
        if (IsEmpty(snipeItAsset.WindowsFeatureUpdate) && !IsEmpty(mdmDevice.WindowsFeatureUpdate))
            updates["windows_feature_update"] = mdmDevice.WindowsFeatureUpdate;
        if (snipeItAsset.SnipeItCategoryId == null && mdmDevice.SnipeItCategoryId != null)
            updates["category_id"] = mdmDevice.SnipeItCategoryId;

        return updates;
    }

    /// <summary>
    /// Returns discrepancies where Snipe-IT has a value and MDM differs (for logging only; we do not overwrite).
    /// </summary>
    public IReadOnlyList<(string Field, string? SnipeItValue, string? MdmValue)> GetDiscrepancies(Device snipeItAsset, Device mdmDevice)
    {
        var list = new List<(string, string?, string?)>();
        if (!IsEmpty(snipeItAsset.DeviceName) && !IsEmpty(mdmDevice.DeviceName) && snipeItAsset.DeviceName != mdmDevice.DeviceName)
            list.Add(("name", snipeItAsset.DeviceName, mdmDevice.DeviceName));
        if (!IsEmpty(snipeItAsset.SerialNumber) && !IsEmpty(mdmDevice.SerialNumber) && snipeItAsset.SerialNumber != mdmDevice.SerialNumber)
            list.Add(("serial", snipeItAsset.SerialNumber, mdmDevice.SerialNumber));
        if (snipeItAsset.SnipeItModelId != null && mdmDevice.SnipeItModelId != null && snipeItAsset.SnipeItModelId != mdmDevice.SnipeItModelId)
            list.Add(("model_id", snipeItAsset.SnipeItModelId.ToString(), mdmDevice.SnipeItModelId.ToString()));
        if (snipeItAsset.SnipeItAssignedUserId != null && mdmDevice.SnipeItAssignedUserId != null && snipeItAsset.SnipeItAssignedUserId != mdmDevice.SnipeItAssignedUserId)
            list.Add(("assigned_to", snipeItAsset.SnipeItAssignedUserId.ToString(), mdmDevice.SnipeItAssignedUserId.ToString()));
        return list;
    }

    private static bool IsEmpty(string? s) => string.IsNullOrWhiteSpace(s);
}
