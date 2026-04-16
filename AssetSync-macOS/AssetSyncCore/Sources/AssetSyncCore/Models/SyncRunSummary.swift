import Foundation

public struct SyncRunSummary: Sendable {
    public var syncRunId: String = UUID().uuidString
    public var startedAtUtc: Date = Date()
    public var completedAtUtc: Date = Date()
    public var dryRun: Bool = false
    public var created: Int = 0
    public var updated: Int = 0
    public var skipped: Int = 0
    public var errors: Int = 0
    public var snipeItReachable: Bool = false
    public var intuneReachable: Bool = false
    public var iruReachable: Bool = false

    public init() {}
}
