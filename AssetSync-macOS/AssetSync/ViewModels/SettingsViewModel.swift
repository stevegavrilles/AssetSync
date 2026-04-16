import SwiftUI
import AssetSyncCore

@MainActor
final class SettingsViewModel: ObservableObject {
    @Published var snipeItUrl = ""
    @Published var snipeItApiKey = ""
    @Published var intuneTenantId = ""
    @Published var intuneClientId = ""
    @Published var intuneClientSecret = ""
    @Published var iruBaseUrl = ""
    @Published var iruApiToken = ""
    @Published var syncIntervalHours = 1
    @Published var dryRunDefault = false
    @Published var writeBackIntune = false
    @Published var writeBackIru = false
    @Published var intuneMdmWins = false
    @Published var iruMdmWins = false
    @Published var webhookUrl = ""
    @Published var webhookType = "Generic"

    func load(appState: AppState) async {
        guard let c = appState.container else { return }
        snipeItUrl = (try? await c.configRepository.get(key: ConfigKeys.snipeItUrl)) ?? ""
        snipeItApiKey = (try? c.credentialStore.get(key: CredentialKeys.snipeItApiKey)) ?? ""
        intuneTenantId = (try? await c.configRepository.get(key: ConfigKeys.intuneTenantId)) ?? ""
        intuneClientId = (try? await c.configRepository.get(key: ConfigKeys.intuneClientId)) ?? ""
        intuneClientSecret = (try? c.credentialStore.get(key: CredentialKeys.intuneClientSecret)) ?? ""
        iruBaseUrl = (try? await c.configRepository.get(key: ConfigKeys.iruBaseUrl)) ?? ""
        iruApiToken = (try? c.credentialStore.get(key: CredentialKeys.iruApiToken)) ?? ""
        syncIntervalHours = (try? await c.configRepository.getSyncIntervalHours()) ?? 1
        dryRunDefault = (try? await c.configRepository.getDryRunDefault()) ?? false
        writeBackIntune = (try? await c.configRepository.getWriteBackIntuneEnabled()) ?? false
        writeBackIru = (try? await c.configRepository.getWriteBackIruEnabled()) ?? false
        intuneMdmWins = (try? await c.configRepository.getIntuneMdmWins()) ?? false
        iruMdmWins = (try? await c.configRepository.getIruMdmWins()) ?? false
        webhookUrl = (try? await c.configRepository.get(key: ConfigKeys.webhookUrl)) ?? ""
        webhookType = (try? await c.configRepository.get(key: ConfigKeys.webhookType)) ?? "Generic"
    }

    func save(appState: AppState) async {
        guard let c = appState.container else { return }
        try? await c.configRepository.set(key: ConfigKeys.snipeItUrl, value: snipeItUrl)
        try? c.credentialStore.set(key: CredentialKeys.snipeItApiKey, value: snipeItApiKey)
        try? await c.configRepository.set(key: ConfigKeys.intuneTenantId, value: intuneTenantId)
        try? await c.configRepository.set(key: ConfigKeys.intuneClientId, value: intuneClientId)
        try? c.credentialStore.set(key: CredentialKeys.intuneClientSecret, value: intuneClientSecret)
        try? await c.configRepository.set(key: ConfigKeys.iruBaseUrl, value: iruBaseUrl)
        try? c.credentialStore.set(key: CredentialKeys.iruApiToken, value: iruApiToken)
        try? await c.configRepository.setSyncIntervalHours(syncIntervalHours)
        try? await c.configRepository.setDryRunDefault(dryRunDefault)
        try? await c.configRepository.setWriteBackIntuneEnabled(writeBackIntune)
        try? await c.configRepository.setWriteBackIruEnabled(writeBackIru)
        try? await c.configRepository.setIntuneMdmWins(intuneMdmWins)
        try? await c.configRepository.setIruMdmWins(iruMdmWins)
        try? await c.configRepository.set(key: ConfigKeys.webhookUrl, value: webhookUrl)
        try? await c.configRepository.set(key: ConfigKeys.webhookType, value: webhookType)

        appState.startScheduledSync(intervalHours: syncIntervalHours)
    }

    func testWebhook(appState: AppState) async {
        // TODO: Wire through container's webhook service
    }
}
