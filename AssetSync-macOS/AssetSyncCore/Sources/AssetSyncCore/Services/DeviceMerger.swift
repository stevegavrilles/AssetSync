import Foundation

/// Merges devices from Intune and Iru keyed by normalized serial.
/// Preference: Iru for Apple, Intune for Windows/Android.
/// Intune devices are added first; Iru fills any remaining empty fields.
public struct DeviceMerger: Sendable {
    public init() {}

    public func merge(intuneDevices: [Device], iruDevices: [Device]) -> [Device] {
        var bySerial: [String: Device] = [:]

        for d in intuneDevices {
            let key = SerialNumberNormalizer.normalize(d.serialNumber)
            guard !key.isEmpty else { continue }
            if bySerial[key] == nil {
                bySerial[key] = d
            } else {
                mergeInto(&bySerial[key]!, from: d)
            }
        }

        for d in iruDevices {
            let key = SerialNumberNormalizer.normalize(d.serialNumber)
            guard !key.isEmpty else { continue }
            if bySerial[key] == nil {
                bySerial[key] = d
            } else {
                mergeInto(&bySerial[key]!, from: d)
            }
        }

        return Array(bySerial.values)
    }

    /// Fills empty fields on target from source.
    /// Priority is determined by iteration order (Intune first, then Iru).
    private func mergeInto(_ target: inout Device, from source: Device) {
        if isEmpty(target.deviceName) && !isEmpty(source.deviceName) { target.deviceName = source.deviceName }
        if isEmpty(target.model) && !isEmpty(source.model) { target.model = source.model }
        if target.snipeItModelId == nil && source.snipeItModelId != nil { target.snipeItModelId = source.snipeItModelId }
        if isEmpty(target.assignedUserUpn) && !isEmpty(source.assignedUserUpn) { target.assignedUserUpn = source.assignedUserUpn }
        if target.snipeItAssignedUserId == nil && source.snipeItAssignedUserId != nil { target.snipeItAssignedUserId = source.snipeItAssignedUserId }
        if isEmpty(target.osVersion) && !isEmpty(source.osVersion) { target.osVersion = source.osVersion }
        if isEmpty(target.windowsFeatureUpdate) && !isEmpty(source.windowsFeatureUpdate) { target.windowsFeatureUpdate = source.windowsFeatureUpdate }
        if isEmpty(target.deviceType) && !isEmpty(source.deviceType) { target.deviceType = source.deviceType }
        if target.snipeItCategoryId == nil && source.snipeItCategoryId != nil { target.snipeItCategoryId = source.snipeItCategoryId }
        if isEmpty(target.platformSource) { target.platformSource = source.platformSource }
        if target.snipeItAssetId == nil && source.snipeItAssetId != nil { target.snipeItAssetId = source.snipeItAssetId }
        if isEmpty(target.snipeItAssetTag) && !isEmpty(source.snipeItAssetTag) { target.snipeItAssetTag = source.snipeItAssetTag }
        if isEmpty(target.azureAdDeviceId) && !isEmpty(source.azureAdDeviceId) { target.azureAdDeviceId = source.azureAdDeviceId }
        if isEmpty(target.iruDeviceId) && !isEmpty(source.iruDeviceId) { target.iruDeviceId = source.iruDeviceId }
        if isEmpty(target.mdmAssetTag) && !isEmpty(source.mdmAssetTag) { target.mdmAssetTag = source.mdmAssetTag }
        if isEmpty(target.intuneNotes) && !isEmpty(source.intuneNotes) { target.intuneNotes = source.intuneNotes }
        if isEmpty(target.intuneDeviceId) && !isEmpty(source.intuneDeviceId) { target.intuneDeviceId = source.intuneDeviceId }
    }

    private func isEmpty(_ s: String?) -> Bool {
        s?.trimmingCharacters(in: .whitespaces).isEmpty ?? true
    }
}
