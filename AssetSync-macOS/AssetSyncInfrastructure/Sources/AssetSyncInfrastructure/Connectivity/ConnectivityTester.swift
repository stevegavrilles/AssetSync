import Foundation
import AssetSyncCore

/// Tests connectivity to Snipe-IT, Intune, and Iru with response time tracking.
public final class ConnectivityTester: ConnectivityTesterProtocol, @unchecked Sendable {
    private let testSnipeItFn: @Sendable () async -> ConnectionStatus
    private let testIntuneFn: @Sendable () async -> ConnectionStatus
    private let testIruFn: @Sendable () async -> ConnectionStatus

    public init(
        testSnipeIt: @escaping @Sendable () async -> ConnectionStatus,
        testIntune: @escaping @Sendable () async -> ConnectionStatus,
        testIru: @escaping @Sendable () async -> ConnectionStatus
    ) {
        self.testSnipeItFn = testSnipeIt
        self.testIntuneFn = testIntune
        self.testIruFn = testIru
    }

    public func testSnipeIt() async -> ConnectionStatus { await testSnipeItFn() }
    public func testIntune() async -> ConnectionStatus { await testIntuneFn() }
    public func testIru() async -> ConnectionStatus { await testIruFn() }

    /// Utility: test an authenticated GET endpoint and return status with timing.
    public static func testGet(url urlString: String, bearerToken: String,
                                service: SourceSystem, session: URLSession = .shared) async -> ConnectionStatus {
        let start = ContinuousClock.now
        do {
            guard let url = URL(string: urlString) else {
                return ConnectionStatus(service: service, state: .error, message: "Invalid URL")
            }
            var request = URLRequest(url: url)
            request.setValue("Bearer \(bearerToken)", forHTTPHeaderField: "Authorization")
            request.timeoutInterval = 15
            let (_, response) = try await session.data(for: request)
            let elapsed = start.duration(to: .now)
            let ms = Int64(elapsed.components.seconds * 1000 + elapsed.components.attoseconds / 1_000_000_000_000_000)
            let status = (response as? HTTPURLResponse)?.statusCode ?? 0
            let ok = (200..<300).contains(status)
            return ConnectionStatus(
                service: service,
                state: ok ? .connected : .error,
                message: ok ? "OK" : "HTTP \(status)",
                responseTimeMs: ms)
        } catch {
            let elapsed = start.duration(to: .now)
            let ms = Int64(elapsed.components.seconds * 1000 + elapsed.components.attoseconds / 1_000_000_000_000_000)
            return ConnectionStatus(service: service, state: .error, message: error.localizedDescription, responseTimeMs: ms)
        }
    }
}
