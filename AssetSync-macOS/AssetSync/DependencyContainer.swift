import Foundation
import AssetSyncCore
import AssetSyncInfrastructure

/// Wires up all dependencies — the Swift equivalent of App.xaml.cs DI setup.
@MainActor
final class DependencyContainer: ObservableObject {
    let credentialStore: KeychainCredentialStore
    let dbInitializer: DatabaseInitializer
    let configRepository: SQLiteConfigRepository
    let logRepository: SQLiteLogRepository
    let mappingRepository: SQLiteMappingRepository
    let syncEngine: SyncEngine

    init() throws {
        // Database
        let dbInit = try DatabaseInitializer()
        try dbInit.initialize()
        self.dbInitializer = dbInit
        let pool = dbInit.pool

        // Repositories
        self.credentialStore = KeychainCredentialStore()
        self.configRepository = SQLiteConfigRepository(dbPool: pool)
        self.logRepository = SQLiteLogRepository(dbPool: pool)
        self.mappingRepository = SQLiteMappingRepository(dbPool: pool)

        let creds = self.credentialStore
        let config = self.configRepository

        // API services (read fresh config each time via closures)
        let snipeItService = SnipeItService(
            baseURL: blockingGet(config, key: ConfigKeys.snipeItUrl) ?? "",
            getApiKey: { (try? creds.get(key: CredentialKeys.snipeItApiKey)) ?? "" }
        )

        let intuneService = IntuneService(
            tenantId: blockingGet(config, key: ConfigKeys.intuneTenantId) ?? "",
            clientId: blockingGet(config, key: ConfigKeys.intuneClientId) ?? "",
            getClientSecret: { (try? creds.get(key: CredentialKeys.intuneClientSecret)) ?? "" }
        )

        let iruService = IruService(
            baseURL: blockingGet(config, key: ConfigKeys.iruBaseUrl) ?? "",
            getToken: { (try? creds.get(key: CredentialKeys.iruApiToken)) ?? "" }
        )

        // Connectivity tester
        let connectivityTester = ConnectivityTester(
            testSnipeIt: {
                let url = try? await config.get(key: ConfigKeys.snipeItUrl)
                let key = (try? creds.get(key: CredentialKeys.snipeItApiKey)) ?? ""
                guard let url, !url.isEmpty else {
                    return ConnectionStatus(service: .snipeIt, state: .notConfigured, message: "Not configured")
                }
                return await ConnectivityTester.testGet(
                    url: "\(url.hasSuffix("/") ? String(url.dropLast()) : url)/api/v1/hardware?limit=1",
                    bearerToken: key, service: .snipeIt)
            },
            testIntune: {
                let tenantId = try? await config.get(key: ConfigKeys.intuneTenantId)
                guard let tenantId, !tenantId.isEmpty else {
                    return ConnectionStatus(service: .intune, state: .notConfigured, message: "Not configured")
                }
                do {
                    _ = try await intuneService.getManagedDevices()
                    return ConnectionStatus(service: .intune, state: .connected, message: "OK")
                } catch {
                    return ConnectionStatus(service: .intune, state: .error, message: error.localizedDescription)
                }
            },
            testIru: {
                let url = try? await config.get(key: ConfigKeys.iruBaseUrl)
                let token = (try? creds.get(key: CredentialKeys.iruApiToken)) ?? ""
                guard let url, !url.isEmpty else {
                    return ConnectionStatus(service: .iru, state: .notConfigured, message: "Not configured")
                }
                return await ConnectivityTester.testGet(
                    url: "\(url.hasSuffix("/") ? String(url.dropLast()) : url)/api/v1/devices?limit=1",
                    bearerToken: token, service: .iru)
            }
        )

        // Webhook
        let webhookURL = blockingGet(config, key: ConfigKeys.webhookUrl)
        let webhookType = blockingGet(config, key: ConfigKeys.webhookType) ?? "Generic"
        let webhookService = WebhookService(webhookURL: webhookURL, webhookType: webhookType)

        // Core services
        let merger = DeviceMerger()
        let resolver = ConflictResolver()
        let buildMapper = BuildVersionMapper(mappingRepository: self.mappingRepository)

        // Sync engine
        self.syncEngine = SyncEngine(
            intuneService: intuneService,
            iruService: iruService,
            snipeItService: snipeItService,
            logRepository: self.logRepository,
            mappingRepository: self.mappingRepository,
            configRepository: self.configRepository,
            webhookService: webhookService,
            connectivityTester: connectivityTester,
            merger: merger,
            resolver: resolver,
            buildMapper: buildMapper
        )
    }
}

/// Helper to synchronously get a config value during init.
private func blockingGet(_ config: SQLiteConfigRepository, key: String) -> String? {
    let semaphore = DispatchSemaphore(value: 0)
    var result: String?
    Task {
        result = try? await config.get(key: key)
        semaphore.signal()
    }
    semaphore.wait()
    return result
}
