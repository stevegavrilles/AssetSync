using AssetSync.Core.Models;

namespace AssetSync.Core.Services;

/// <summary>
/// MDM (Intune/Iru) is source of truth for device names.
/// Snipe-IT is source of truth for asset tags.
/// </summary>
public class ConflictResolver
{
    /// <summary>
    /// Returns updates to apply.
    /// Device name: MDM always wins — any name change in MDM is pushed to Snipe-IT.
    /// Asset tag: Snipe-IT is source of truth (handled separately in SyncEngine).
    /// Other fields: mdmWins=false fills empty Snipe-IT fields; mdmWins=true overwrites.
    /// Serial is never overwritten regardless of mode.
    /// </summary>
    public IReadOnlyDictionary<string, object?> GetUpdatesToApply(Device snipeItAsset, Device mdmDevice, bool mdmWins = false)
    {
        var updates = new Dictionary<string, object?>();

        // Name: MDM is always source of truth — push whenever MDM has a value and it differs
        if (!IsEmpty(mdmDevice.DeviceName) &&
            !string.Equals(snipeItAsset.DeviceName, mdmDevice.DeviceName, StringComparison.Ordinal))
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
    /// Returns discrepancies where Snipe-IT has a value and MDM differs (for logging only).
    /// Name is excluded — MDM is authoritative for names and differences are auto-resolved.
    /// </summary>
    public IReadOnlyList<(string Field, string? SnipeItValue, string? MdmValue)> GetDiscrepancies(Device snipeItAsset, Device mdmDevice)
    {
        var list = new List<(string, string?, string?)>();
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
