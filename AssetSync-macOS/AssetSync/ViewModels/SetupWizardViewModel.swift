import SwiftUI
import AssetSyncCore

@MainActor
final class SetupWizardViewModel: ObservableObject {
    @Published var snipeItUrl = ""
    @Published var snipeItApiKey = ""
    @Published var intuneTenantId = ""
    @Published var intuneClientId = ""
    @Published var intuneClientSecret = ""
    @Published var iruBaseUrl = ""
    @Published var iruApiToken = ""

    func save(appState: AppState) async {
        guard let c = appState.container else { return }

        if !snipeItUrl.isEmpty {
            try? await c.configRepository.set(key: ConfigKeys.snipeItUrl, value: snipeItUrl)
        }
        if !snipeItApiKey.isEmpty {
            try? c.credentialStore.set(key: CredentialKeys.snipeItApiKey, value: snipeItApiKey)
        }
        if !intuneTenantId.isEmpty {
            try? await c.configRepository.set(key: ConfigKeys.intuneTenantId, value: intuneTenantId)
        }
        if !intuneClientId.isEmpty {
            try? await c.configRepository.set(key: ConfigKeys.intuneClientId, value: intuneClientId)
        }
        if !intuneClientSecret.isEmpty {
            try? c.credentialStore.set(key: CredentialKeys.intuneClientSecret, value: intuneClientSecret)
        }
        if !iruBaseUrl.isEmpty {
            try? await c.configRepository.set(key: ConfigKeys.iruBaseUrl, value: iruBaseUrl)
        }
        if !iruApiToken.isEmpty {
            try? c.credentialStore.set(key: CredentialKeys.iruApiToken, value: iruApiToken)
        }

        try? await c.configRepository.set(key: ConfigKeys.setupComplete, value: "true")
    }
}
