import Foundation

public enum SyncAction: String, Codable, Sendable {
    case create = "Create"
    case update = "Update"
    case skip = "Skip"
    case error = "Error"
}
