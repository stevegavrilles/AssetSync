import Foundation

public struct ConnectionStatus: Sendable {
    public var service: SourceSystem
    public var state: ConnectionState
    public var message: String?
    public var responseTimeMs: Int64?

    public init(service: SourceSystem, state: ConnectionState, message: String? = nil, responseTimeMs: Int64? = nil) {
        self.service = service
        self.state = state
        self.message = message
        self.responseTimeMs = responseTimeMs
    }
}
