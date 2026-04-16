import Foundation

public struct ModelMapping: Identifiable, Sendable {
    public var id: Int
    public var mdmModelString: String
    public var snipeItModelId: Int

    public init(id: Int = 0, mdmModelString: String = "", snipeItModelId: Int = 0) {
        self.id = id
        self.mdmModelString = mdmModelString
        self.snipeItModelId = snipeItModelId
    }
}

public struct UserMapping: Identifiable, Sendable {
    public var id: Int
    public var mdmUserIdentifier: String
    public var snipeItUserId: Int

    public init(id: Int = 0, mdmUserIdentifier: String = "", snipeItUserId: Int = 0) {
        self.id = id
        self.mdmUserIdentifier = mdmUserIdentifier
        self.snipeItUserId = snipeItUserId
    }
}

public struct BuildMapping: Identifiable, Sendable {
    public var id: Int
    public var buildNumber: String
    public var friendlyName: String

    public init(id: Int = 0, buildNumber: String = "", friendlyName: String = "") {
        self.id = id
        self.buildNumber = buildNumber
        self.friendlyName = friendlyName
    }
}

public struct CategoryMapping: Identifiable, Sendable {
    public var id: Int
    public var mdmDeviceType: String
    public var snipeItCategoryId: Int

    public init(id: Int = 0, mdmDeviceType: String = "", snipeItCategoryId: Int = 0) {
        self.id = id
        self.mdmDeviceType = mdmDeviceType
        self.snipeItCategoryId = snipeItCategoryId
    }
}
