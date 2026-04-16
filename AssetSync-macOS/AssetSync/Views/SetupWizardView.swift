import SwiftUI

/// First-run setup wizard. Presented as a sheet on initial launch.
struct SetupWizardView: View {
    @EnvironmentObject private var appState: AppState
    @StateObject private var viewModel = SetupWizardViewModel()
    @Environment(\.dismiss) private var dismiss
    @State private var step = 0

    var body: some View {
        VStack(spacing: 0) {
            // Step indicator
            HStack {
                ForEach(0..<4) { i in
                    Circle()
                        .fill(i <= step ? Color.accentColor : Color.secondary.opacity(0.3))
                        .frame(width: 8, height: 8)
                }
            }
            .padding(.top)

            TabView(selection: $step) {
                // Step 0: Welcome
                VStack(spacing: 16) {
                    Image(systemName: "arrow.triangle.2.circlepath.circle")
                        .font(.system(size: 64))
                        .foregroundStyle(.tint)
                    Text("Welcome to AssetSync")
                        .font(.title)
                    Text("Synchronize device assets between Intune, Iru, and Snipe-IT.")
                        .multilineTextAlignment(.center)
                        .foregroundStyle(.secondary)
                }
                .tag(0)

                // Step 1: Snipe-IT
                Form {
                    Section("Snipe-IT Connection") {
                        TextField("URL", text: $viewModel.snipeItUrl)
                        SecureField("API Key", text: $viewModel.snipeItApiKey)
                    }
                }
                .formStyle(.grouped)
                .tag(1)

                // Step 2: MDM Sources
                Form {
                    Section("Intune (optional)") {
                        TextField("Tenant ID", text: $viewModel.intuneTenantId)
                        TextField("Client ID", text: $viewModel.intuneClientId)
                        SecureField("Client Secret", text: $viewModel.intuneClientSecret)
                    }
                    Section("Iru (optional)") {
                        TextField("Base URL", text: $viewModel.iruBaseUrl)
                        SecureField("API Token", text: $viewModel.iruApiToken)
                    }
                }
                .formStyle(.grouped)
                .tag(2)

                // Step 3: Done
                VStack(spacing: 16) {
                    Image(systemName: "checkmark.circle")
                        .font(.system(size: 64))
                        .foregroundStyle(.green)
                    Text("You're all set!")
                        .font(.title)
                    Text("AssetSync will run in your menu bar. Click the icon to sync or open settings.")
                        .multilineTextAlignment(.center)
                        .foregroundStyle(.secondary)
                }
                .tag(3)
            }
            .tabViewStyle(.automatic)
            .padding()

            // Navigation buttons
            HStack {
                if step > 0 {
                    Button("Back") { step -= 1 }
                }
                Spacer()
                if step < 3 {
                    Button("Next") { step += 1 }
                        .buttonStyle(.borderedProminent)
                } else {
                    Button("Finish") {
                        Task {
                            await viewModel.save(appState: appState)
                            dismiss()
                        }
                    }
                    .buttonStyle(.borderedProminent)
                }
            }
            .padding()
        }
        .frame(width: 500, height: 400)
    }
}
