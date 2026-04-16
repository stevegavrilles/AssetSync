import SwiftUI

struct DashboardView: View {
    @EnvironmentObject private var appState: AppState
    @StateObject private var viewModel = DashboardViewModel()

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 20) {
                // Sync status
                GroupBox("Sync Status") {
                    VStack(alignment: .leading, spacing: 12) {
                        HStack {
                            Image(systemName: appState.isSyncing ? "arrow.triangle.2.circlepath" : "checkmark.circle")
                                .foregroundStyle(appState.isSyncing ? .orange : .green)
                            Text(appState.isSyncing ? "Sync in progress..." : "Idle")
                                .font(.headline)
                        }

                        if let lastSync = appState.lastSyncTime {
                            LabeledContent("Last Sync") {
                                Text(lastSync, style: .relative) + Text(" ago")
                            }
                        }

                        if let summary = appState.lastSyncSummary {
                            Text(summary)
                                .font(.callout)
                                .foregroundStyle(.secondary)
                        }
                    }
                    .padding(8)
                }

                // Quick actions
                GroupBox("Actions") {
                    HStack(spacing: 12) {
                        Button("Sync Now") {
                            Task { await appState.runSync(dryRun: false) }
                        }
                        .buttonStyle(.borderedProminent)
                        .disabled(appState.isSyncing)

                        Button("Dry Run") {
                            Task { await appState.runSync(dryRun: true) }
                        }
                        .buttonStyle(.bordered)
                        .disabled(appState.isSyncing)
                    }
                    .padding(8)
                }

                // Connectivity status
                GroupBox("Service Connectivity") {
                    VStack(alignment: .leading, spacing: 8) {
                        ConnectivityRow(name: "Snipe-IT", status: viewModel.snipeItStatus)
                        ConnectivityRow(name: "Intune", status: viewModel.intuneStatus)
                        ConnectivityRow(name: "Iru", status: viewModel.iruStatus)
                    }
                    .padding(8)
                }

                Button("Test Connections") {
                    Task { await viewModel.testConnections(appState: appState) }
                }
                .buttonStyle(.bordered)
            }
            .padding()
        }
        .navigationTitle("Dashboard")
    }
}

private struct ConnectivityRow: View {
    let name: String
    let status: String

    var statusColor: Color {
        switch status {
        case "Connected": return .green
        case "Not configured": return .secondary
        case "": return .secondary
        default: return .red
        }
    }

    var body: some View {
        HStack {
            Circle()
                .fill(statusColor)
                .frame(width: 8, height: 8)
            Text(name)
                .frame(width: 80, alignment: .leading)
            Text(status.isEmpty ? "Not tested" : status)
                .foregroundStyle(.secondary)
        }
    }
}
