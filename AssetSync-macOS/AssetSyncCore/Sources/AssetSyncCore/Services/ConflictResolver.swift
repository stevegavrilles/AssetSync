import Foundation

/// MDM (Intune/Iru) is source of truth for device names.
/// Snipe-IT is source of truth for asset tags.
public struct ConflictResolver: Sendable {
    public init() {}

    /// Returns updates to apply.
    /// - Device name: MDM always wins — any name change in MDM is pushed to Snipe-IT.
    /// - Asset tag: Snipe-IT is source of truth (handled separately in SyncEngine).
    /// - Other fields: mdmWins=false fills empty Snipe-IT fields; mdmWins=true overwrites.
    /// - Serial is never overwritten regardless of mode.
    public func getUpdatesToApply(snipeItAsset: Device, mdmDevice: Device, mdmWins: Bool = false) -> [String: Any] {
        var updates: [String: Any] = [:]

        // Name: MDM is always source of truth
        if !isEmpty(mdmDevice.deviceName) && snipeItAsset.deviceName != mdmDevice.deviceName {
            updates["name"] = mdmDevice.deviceName!
        }

        // Serial: never overwrite an existing value
        if isEmpty(snipeItAsset.serialNumber) && !isEmpty(mdmDevice.serialNumber) {
            updates["serial"] = mdmDevice.serialNumber!
        }

        if (snipeItAsset.snipeItModelId == nil || mdmWins), let v = mdmDevice.snipeItModelId {
            updates["model_id"] = v
        }
        if (snipeItAsset.snipeItAssignedUserId == nil || mdmWins), let v = mdmDevice.snipeItAssignedUserId {
            updates["assigned_to"] = v
        }
        if (isEmpty(snipeItAsset.osVersion) || mdmWins) && !isEmpty(mdmDevice.osVersion) {
            updates["os_version"] = mdmDevice.osVersion!
        }
        if (isEmpty(snipeItAsset.windowsFeatureUpdate) || mdmWins) && !isEmpty(mdmDevice.windowsFeatureUpdate) {
            updates["windows_feature_update"] = mdmDevice.windowsFeatureUpdate!
        }
        if (snipeItAsset.snipeItCategoryId == nil || mdmWins), let v = mdmDevice.snipeItCategoryId {
            updates["category_id"] = v
        }

        return updates
    }

    /// Returns discrepancies where Snipe-IT has a value and MDM differs (for logging only).
    /// Name is excluded — MDM is authoritative for names and differences are auto-resolved.
    public func getDiscrepancies(snipeItAsset: Device, mdmDevice: Device) -> [(field: String, snipeItValue: String?, mdmValue: String?)] {
        var list: [(String, String?, String?)] = []

        if !isEmpty(snipeItAsset.serialNumber) && !isEmpty(mdmDevice.serialNumber) && snipeItAsset.serialNumber != mdmDevice.serialNumber {
            list.append(("serial", snipeItAsset.serialNumber, mdmDevice.serialNumber))
        }
        if let a = snipeItAsset.snipeItModelId, let b = mdmDevice.snipeItModelId, a != b {
            list.append(("model_id", String(a), String(b)))
        }
        if let a = snipeItAsset.snipeItAssignedUserId, let b = mdmDevice.snipeItAssignedUserId, a != b {
            list.append(("assigned_to", String(a), String(b)))
        }

        return list
    }

    private func isEmpty(_ s: String?) -> Bool {
        s?.trimmingCharacters(in: .whitespaces).isEmpty ?? true
    }
}
