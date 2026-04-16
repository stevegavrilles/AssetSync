import SwiftUI

struct SettingsView: View {
    @EnvironmentObject private var appState: AppState
    @StateObject private var viewModel = SettingsViewModel()

    var body: some View {
        Form {
            Section("Snipe-IT") {
                TextField("URL", text: $viewModel.snipeItUrl, prompt: Text("https://snipeit.example.com"))
                SecureField("API Key", text: $viewModel.snipeItApiKey)
            }

            Section("Intune") {
                TextField("Tenant ID", text: $viewModel.intuneTenantId)
                TextField("Client ID", text: $viewModel.intuneClientId)
                SecureField("Client Secret", text: $viewModel.intuneClientSecret)
            }

            Section("Iru (Kandji)") {
                TextField("Base URL", text: $viewModel.iruBaseUrl, prompt: Text("https://your-subdomain.api.kandji.io"))
                SecureField("API Token", text: $viewModel.iruApiToken)
            }

            Section("Sync") {
                Picker("Interval", selection: $viewModel.syncIntervalHours) {
                    Text("1 hour").tag(1)
                    Text("2 hours").tag(2)
                    Text("4 hours").tag(4)
                    Text("8 hours").tag(8)
                    Text("12 hours").tag(12)
                    Text("24 hours").tag(24)
                }
                Toggle("Dry run by default", isOn: $viewModel.dryRunDefault)
            }

            Section("Write-Back") {
                Toggle("Write asset tag to Intune notes", isOn: $viewModel.writeBackIntune)
                Toggle("Write asset tag to Iru", isOn: $viewModel.writeBackIru)
            }

            Section("MDM Priority") {
                Toggle("Intune MDM wins (overwrite Snipe-IT)", isOn: $viewModel.intuneMdmWins)
                Toggle("Iru MDM wins (overwrite Snipe-IT)", isOn: $viewModel.iruMdmWins)
            }

            Section("Webhooks") {
                TextField("Webhook URL", text: $viewModel.webhookUrl)
                Picker("Type", selection: $viewModel.webhookType) {
                    Text("Generic").tag("Generic")
                    Text("Teams").tag("Teams")
                    Text("Slack").tag("Slack")
                }
                Button("Test Webhook") {
                    Task { await viewModel.testWebhook(appState: appState) }
                }
            }

            Section("Login Item") {
                Toggle("Launch at login", isOn: Binding(
                    get: { appState.loginItemEnabled },
                    set: { _ in appState.toggleLoginItem() }
                ))
            }

            HStack {
                Spacer()
                Button("Save") {
                    Task { await viewModel.save(appState: appState) }
                }
                .buttonStyle(.borderedProminent)
            }
        }
        .formStyle(.grouped)
        .navigationTitle("Settings")
        .task { await viewModel.load(appState: appState) }
    }
}
