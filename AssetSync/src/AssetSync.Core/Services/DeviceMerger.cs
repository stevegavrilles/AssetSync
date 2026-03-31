using AssetSync.Core.Models;
using AssetSync.Core.Services;

namespace AssetSync.Core.Services;

/// <summary>
/// Merges devices from Intune and Iru keyed by normalized serial.
/// Preference: Iru for Apple, Intune for Windows/Android.
/// </summary>
public class DeviceMerger
{
    public IReadOnlyList<Device> Merge(IReadOnlyList<Device> intuneDevices, IReadOnlyList<Device> iruDevices)
    {
        var bySerial = new Dictionary<string, Device>(StringComparer.OrdinalIgnoreCase);

        foreach (var d in intuneDevices)
        {
            var key = SerialNumberNormalizer.Normalize(d.SerialNumber);
            if (string.IsNullOrEmpty(key)) continue;
            if (!bySerial.ContainsKey(key))
                bySerial[key] = Clone(d);
            else
                MergeInto(bySerial[key], d, preferIntune: IsWindowsOrAndroid(d));
        }

        foreach (var d in iruDevices)
        {
            var key = SerialNumberNormalizer.Normalize(d.SerialNumber);
            if (string.IsNullOrEmpty(key)) continue;
            if (!bySerial.ContainsKey(key))
                bySerial[key] = Clone(d);
            else
                MergeInto(bySerial[key], d, preferIntune: !IsApple(d));
        }

        return bySerial.Values.ToList();
    }

    private static bool IsApple(Device d)
    {
        var os = (d.OperatingSystem ?? "").ToLowerInvariant();
        var type = (d.DeviceType ?? "").ToLowerInvariant();
        return os.Contains("ios") || os.Contains("macos") || type.Contains("iphone") || type.Contains("ipad") || type.Contains("mac");
    }

    private static bool IsWindowsOrAndroid(Device d)
    {
        var os = (d.OperatingSystem ?? "").ToLowerInvariant();
        return os.Contains("windows") || os.Contains("android");
    }

    private static Device Clone(Device d)
    {
        return new Device
        {
            NormalizedSerial = d.NormalizedSerial,
            SerialNumber = d.SerialNumber,
            DeviceName = d.DeviceName,
            Model = d.Model,
            SnipeItModelId = d.SnipeItModelId,
            AssignedUserUpn = d.AssignedUserUpn,
            SnipeItAssignedUserId = d.SnipeItAssignedUserId,
            OsVersion = d.OsVersion,
            WindowsFeatureUpdate = d.WindowsFeatureUpdate,
            DeviceType = d.DeviceType,
            SnipeItCategoryId = d.SnipeItCategoryId,
            PlatformSource = d.PlatformSource,
            SnipeItAssetId = d.SnipeItAssetId,
            SnipeItAssetTag = d.SnipeItAssetTag,
            AzureAdDeviceId = d.AzureAdDeviceId,
            IruDeviceId = d.IruDeviceId,
            OperatingSystem = d.OperatingSystem,
            MdmAssetTag = d.MdmAssetTag
        };
    }

    private static void MergeInto(Device target, Device source, bool preferIntune)
    {
        if (preferIntune)
        {
            if (string.IsNullOrEmpty(target.DeviceName) && !string.IsNullOrEmpty(source.DeviceName)) target.DeviceName = source.DeviceName;
            if (string.IsNullOrEmpty(target.Model) && !string.IsNullOrEmpty(source.Model)) target.Model = source.Model;
            if (target.SnipeItModelId == null && source.SnipeItModelId != null) target.SnipeItModelId = source.SnipeItModelId;
            if (string.IsNullOrEmpty(target.AssignedUserUpn) && !string.IsNullOrEmpty(source.AssignedUserUpn)) target.AssignedUserUpn = source.AssignedUserUpn;
            if (target.SnipeItAssignedUserId == null && source.SnipeItAssignedUserId != null) target.SnipeItAssignedUserId = source.SnipeItAssignedUserId;
            if (string.IsNullOrEmpty(target.OsVersion) && !string.IsNullOrEmpty(source.OsVersion)) target.OsVersion = source.OsVersion;
            if (string.IsNullOrEmpty(target.WindowsFeatureUpdate) && !string.IsNullOrEmpty(source.WindowsFeatureUpdate)) target.WindowsFeatureUpdate = source.WindowsFeatureUpdate;
            if (string.IsNullOrEmpty(target.DeviceType) && !string.IsNullOrEmpty(source.DeviceType)) target.DeviceType = source.DeviceType;
            if (target.SnipeItCategoryId == null && source.SnipeItCategoryId != null) target.SnipeItCategoryId = source.SnipeItCategoryId;
            if (string.IsNullOrEmpty(target.PlatformSource)) target.PlatformSource = source.PlatformSource;
            if (target.SnipeItAssetId == null && source.SnipeItAssetId != null) target.SnipeItAssetId = source.SnipeItAssetId;
            if (string.IsNullOrEmpty(target.SnipeItAssetTag) && !string.IsNullOrEmpty(source.SnipeItAssetTag)) target.SnipeItAssetTag = source.SnipeItAssetTag;
            if (string.IsNullOrEmpty(target.AzureAdDeviceId) && !string.IsNullOrEmpty(source.AzureAdDeviceId)) target.AzureAdDeviceId = source.AzureAdDeviceId;
            if (string.IsNullOrEmpty(target.IruDeviceId) && !string.IsNullOrEmpty(source.IruDeviceId)) target.IruDeviceId = source.IruDeviceId;
            // Prefer whichever side has a valid PM-format asset tag
            if (string.IsNullOrEmpty(target.MdmAssetTag) && !string.IsNullOrEmpty(source.MdmAssetTag)) target.MdmAssetTag = source.MdmAssetTag;
        }
        else
        {
            if (string.IsNullOrEmpty(target.DeviceName) && !string.IsNullOrEmpty(source.DeviceName)) target.DeviceName = source.DeviceName;
            if (string.IsNullOrEmpty(target.Model) && !string.IsNullOrEmpty(source.Model)) target.Model = source.Model;
            if (target.SnipeItModelId == null && source.SnipeItModelId != null) target.SnipeItModelId = source.SnipeItModelId;
            if (string.IsNullOrEmpty(target.AssignedUserUpn) && !string.IsNullOrEmpty(source.AssignedUserUpn)) target.AssignedUserUpn = source.AssignedUserUpn;
            if (target.SnipeItAssignedUserId == null && source.SnipeItAssignedUserId != null) target.SnipeItAssignedUserId = source.SnipeItAssignedUserId;
            if (string.IsNullOrEmpty(target.OsVersion) && !string.IsNullOrEmpty(source.OsVersion)) target.OsVersion = source.OsVersion;
            if (string.IsNullOrEmpty(target.WindowsFeatureUpdate) && !string.IsNullOrEmpty(source.WindowsFeatureUpdate)) target.WindowsFeatureUpdate = source.WindowsFeatureUpdate;
            if (string.IsNullOrEmpty(target.DeviceType) && !string.IsNullOrEmpty(source.DeviceType)) target.DeviceType = source.DeviceType;
            if (target.SnipeItCategoryId == null && source.SnipeItCategoryId != null) target.SnipeItCategoryId = source.SnipeItCategoryId;
            if (string.IsNullOrEmpty(target.PlatformSource)) target.PlatformSource = source.PlatformSource;
            if (target.SnipeItAssetId == null && source.SnipeItAssetId != null) target.SnipeItAssetId = source.SnipeItAssetId;
            if (string.IsNullOrEmpty(target.SnipeItAssetTag) && !string.IsNullOrEmpty(source.SnipeItAssetTag)) target.SnipeItAssetTag = source.SnipeItAssetTag;
            if (string.IsNullOrEmpty(target.AzureAdDeviceId) && !string.IsNullOrEmpty(source.AzureAdDeviceId)) target.AzureAdDeviceId = source.AzureAdDeviceId;
            if (string.IsNullOrEmpty(target.IruDeviceId) && !string.IsNullOrEmpty(source.IruDeviceId)) target.IruDeviceId = source.IruDeviceId;
            // Prefer whichever side has a valid PM-format asset tag
            if (string.IsNullOrEmpty(target.MdmAssetTag) && !string.IsNullOrEmpty(source.MdmAssetTag)) target.MdmAssetTag = source.MdmAssetTag;
        }
    }
}
