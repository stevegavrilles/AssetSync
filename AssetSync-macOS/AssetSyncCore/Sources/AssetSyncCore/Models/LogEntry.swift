import Foundation

public struct LogEntry: Identifiable, Sendable {
    public var id: Int64 = 0
    public var timestampUtc: Date = Date()
    public var level: LogLevel = .info
    public var sourceSystem: SourceSystem = .application
    public var action: String = ""
    public var serialNumber: String?
    public var deviceName: String?
    public var fieldName: String?
    public var oldValue: String?
    public var newValue: String?
    public var success: Bool = true
    public var errorDetail: String?
    public var syncRunId: String = ""

    public init() {}
}

public struct LogFilter: Sendable {
    public var fromUtc: Date?
    public var toUtc: Date?
    public var minLevel: LogLevel?
    public var sourceSystem: SourceSystem?
    public var action: String?
    public var serialNumber: String?
    public var freeText: String?
    public var syncRunId: String?
    public var limit: Int?
    public var offset: Int?

    public init() {}
}
