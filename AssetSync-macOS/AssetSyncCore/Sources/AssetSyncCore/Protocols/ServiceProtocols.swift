import Foundation

// MARK: - MDM Services

public protocol IntuneServiceProtocol: Sendable {
    func getManagedDevices() async throws -> [Device]
    func writeBackAssetTag(intuneDeviceId: String, assetTag: String, existingNotes: String?) async throws -> Bool
}

public protocol IruServiceProtocol: Sendable {
    func getDevices() async throws -> [Device]
    func writeBackAssetTag(iruDeviceId: String, assetTag: String) async throws -> Bool
}

// MARK: - Snipe-IT

public protocol SnipeItServiceProtocol: Sendable {
    func getAssetBySerial(_ normalizedSerial: String) async throws -> Device?
    func searchAssetsBySerial(_ searchTerm: String) async throws -> [Device]
    func createAsset(_ device: Device) async throws -> Device?
    func updateAsset(assetId: Int, updates: [String: Any]) async throws -> Bool
    func getModels() async throws -> [SnipeItLookup]
    func getCategories() async throws -> [SnipeItLookup]
    func getUsers() async throws -> [SnipeItLookup]
}

// MARK: - Sync Engine

public protocol SyncEngineProtocol: Sendable {
    func runSync(dryRun: Bool) async throws -> SyncRunSummary
}

// MARK: - Connectivity

public protocol ConnectivityTesterProtocol: Sendable {
    func testSnipeIt() async -> ConnectionStatus
    func testIntune() async -> ConnectionStatus
    func testIru() async -> ConnectionStatus
}

// MARK: - Webhooks

public protocol WebhookServiceProtocol: Sendable {
    func sendSyncNotification(_ summary: SyncRunSummary) async
    func sendConnectivityFailureNotification(serviceName: String, message: String) async
    func sendCredentialErrorNotification(serviceName: String, message: String) async
    func testWebhook() async
}

// MARK: - Credential Store

public protocol CredentialStoreProtocol: Sendable {
    func set(key: String, value: String) throws
    func get(key: String) throws -> String?
    func remove(key: String) throws
    func exists(key: String) throws -> Bool
}
