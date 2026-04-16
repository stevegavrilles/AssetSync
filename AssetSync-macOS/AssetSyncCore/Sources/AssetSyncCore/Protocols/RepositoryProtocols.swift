import Foundation

// MARK: - Config Repository

public protocol ConfigRepositoryProtocol: Sendable {
    func get(key: String) async throws -> String?
    func set(key: String, value: String) async throws

    func getSyncIntervalHours() async throws -> Int
    func setSyncIntervalHours(_ hours: Int) async throws

    func getDryRunDefault() async throws -> Bool
    func setDryRunDefault(_ value: Bool) async throws

    func getWriteBackIntuneEnabled() async throws -> Bool
    func setWriteBackIntuneEnabled(_ value: Bool) async throws

    func getWriteBackIruEnabled() async throws -> Bool
    func setWriteBackIruEnabled(_ value: Bool) async throws

    func getIntuneMdmWins() async throws -> Bool
    func setIntuneMdmWins(_ value: Bool) async throws

    func getIruMdmWins() async throws -> Bool
    func setIruMdmWins(_ value: Bool) async throws
}

// MARK: - Log Repository

public protocol LogRepositoryProtocol: Sendable {
    func append(_ entry: LogEntry) async throws
    func getEntries(filter: LogFilter) async throws -> [LogEntry]
    func purgeOlderThan(_ retention: TimeInterval) async throws
}

// MARK: - Mapping Repository

public protocol MappingRepositoryProtocol: Sendable {
    // Model mappings
    func getModelMappings() async throws -> [ModelMapping]
    func getModelMapping(mdmModelString: String) async throws -> ModelMapping?
    func saveModelMapping(_ mapping: ModelMapping) async throws
    func deleteModelMapping(id: Int) async throws

    // User mappings
    func getUserMappings() async throws -> [UserMapping]
    func getUserMapping(mdmUserIdentifier: String) async throws -> UserMapping?
    func saveUserMapping(_ mapping: UserMapping) async throws
    func deleteUserMapping(id: Int) async throws

    // Build mappings
    func getBuildMappings() async throws -> [BuildMapping]
    func getBuildMapping(buildNumber: String) async throws -> BuildMapping?
    func saveBuildMapping(_ mapping: BuildMapping) async throws
    func deleteBuildMapping(id: Int) async throws

    // Category mappings
    func getCategoryMappings() async throws -> [CategoryMapping]
    func getCategoryMapping(mdmDeviceType: String) async throws -> CategoryMapping?
    func saveCategoryMapping(_ mapping: CategoryMapping) async throws
    func deleteCategoryMapping(id: Int) async throws

    // Model ignores
    func getIgnoredModels() async throws -> [String]
    func isModelIgnored(_ mdmModelString: String) async throws -> Bool
    func addModelIgnore(_ mdmModelString: String) async throws
    func removeModelIgnore(_ mdmModelString: String) async throws
}

// MARK: - Well-known config and credential keys

public enum ConfigKeys {
    public static let snipeItUrl = "snipeit_url"
    public static let intuneTenantId = "intune_tenant_id"
    public static let intuneClientId = "intune_client_id"
    public static let iruBaseUrl = "iru_base_url"
    public static let webhookUrl = "webhook_url"
    public static let webhookType = "webhook_type"
    public static let setupComplete = "setup_complete"
}

public enum CredentialKeys {
    public static let snipeItApiKey = "snipeit_api_key"
    public static let intuneClientSecret = "intune_client_secret"
    public static let iruApiToken = "iru_api_token"
}
