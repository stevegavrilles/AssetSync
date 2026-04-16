import Foundation

public enum LogLevel: String, Codable, Sendable, CaseIterable, Comparable {
    case debug = "Debug"
    case info = "Info"
    case warning = "Warning"
    case error = "Error"

    private var sortOrder: Int {
        switch self {
        case .debug: return 0
        case .info: return 1
        case .warning: return 2
        case .error: return 3
        }
    }

    public static func < (lhs: LogLevel, rhs: LogLevel) -> Bool {
        lhs.sortOrder < rhs.sortOrder
    }
}
