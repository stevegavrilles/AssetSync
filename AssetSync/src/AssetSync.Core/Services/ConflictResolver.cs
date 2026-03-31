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
    /// Returns updates to apply.
    /// mdmWins=false (default): only fills empty Snipe-IT fields (Snipe-IT wins).
    /// mdmWins=true: MDM overwrites existing Snipe-IT fields as well (MDM wins).
    /// Serial is never overwritten regardless of mode.
    /// </summary>
    public IReadOnlyDictionary<string, object?> GetUpdatesToApply(Device snipeItAsset, Device mdmDevice, bool mdmWins = false)
    {
        var updates = new Dictionary<string, object?>();

        // Name: overwrite if empty, placeholder, or MDM wins
        if (!IsEmpty(mdmDevice.DeviceName) &&
            (IsEmpty(snipeItAsset.DeviceName) || PlaceholderNames.Contains(snipeItAsset.DeviceName!) || mdmWins))
            updates["name"] = mdmDevice.DeviceName;

        // Serial: never overwrite an existing value regardless of mode
        if (IsEmpty(snipeItAsset.SerialNumber) && !IsEmpty(mdmDevice.SerialNumber))
            updates["serial"] = mdmDevice.SerialNumber;

        if ((snipeItAsset.SnipeItModelId == null || mdmWins) && mdmDevice.SnipeItModelId != null)
            updates["model_id"] = mdmDevice.SnipeItModelId;
        if ((snipeItAsset.SnipeItAssignedUserId == null || mdmWins) && mdmDevice.SnipeItAssignedUserId != null)
            updates["assigned_to"] = mdmDevice.SnipeItAssignedUserId;
        if ((IsEmpty(snipeItAsset.OsVersion) || mdmWins) && !IsEmpty(mdmDevice.OsVersion))
            updates["os_version"] = mdmDevice.OsVersion;
        if ((IsEmpty(snipeItAsset.WindowsFeatureUpdate) || mdmWins) && !IsEmpty(mdmDevice.WindowsFeatureUpdate))
            updates["windows_feature_update"] = mdmDevice.WindowsFeatureUpdate;
        if ((snipeItAsset.SnipeItCategoryId == null || mdmWins) && mdmDevice.SnipeItCategoryId != null)
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
