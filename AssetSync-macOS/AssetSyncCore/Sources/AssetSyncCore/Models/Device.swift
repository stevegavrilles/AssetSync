import Foundation

/// Unified device model merged from Intune, Iru, and Snipe-IT.
/// Serial number is the match key (normalized).
public struct Device: Identifiable, Sendable {
    public var id: String { normalizedSerial }

    public var normalizedSerial: String = ""
    public var serialNumber: String?
    public var deviceName: String?
    public var model: String?
    public var snipeItModelId: Int?
    public var assignedUserUpn: String?
    public var snipeItAssignedUserId: Int?
    public var osVersion: String?
    public var windowsFeatureUpdate: String?
    public var deviceType: String?
    public var snipeItCategoryId: Int?
    public var platformSource: String?
    public var snipeItAssetId: Int?
    public var snipeItAssetTag: String?
    public var azureAdDeviceId: String?
    public var iruDeviceId: String?
    public var operatingSystem: String?
    /// Asset tag as reported by the MDM platform (Intune Notes or Iru asset_tag field).
    public var mdmAssetTag: String?
    /// Raw Notes field from Intune — preserved so write-back can update the tag without clobbering other note content.
    public var intuneNotes: String?
    /// Intune managed device ID — required for PATCH. Distinct from azureAdDeviceId.
    public var intuneDeviceId: String?

    public init() {}
}

/// Lightweight id+name pair returned by Snipe-IT lookup endpoints.
public struct SnipeItLookup: Identifiable, Sendable {
    public var id: Int
    public var name: String

    public init(id: Int, name: String) {
        self.id = id
        self.name = name
    }
}
