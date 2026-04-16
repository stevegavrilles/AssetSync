import Foundation

public enum ConnectionState: String, Codable, Sendable {
    case connected = "Connected"
    case warning = "Warning"
    case error = "Error"
    case notConfigured = "NotConfigured"
}
