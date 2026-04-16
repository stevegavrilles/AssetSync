import SwiftUI

/// The dropdown that appears when the user clicks the menu bar icon.
struct MenuBarView: View {
    @ObservedObject var appState: AppState

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            if let summary = appState.lastSyncSummary {
                Text(summary)
                    .font(.caption)
            }
            if let lastSync = appState.lastSyncTime {
                Text("Last sync: \(lastSync, style: .relative) ago")
                    .font(.caption2)
                    .foregroundStyle(.secondary)
            }
        }
        .padding(.horizontal, 8)

        Divider()

        Button(appState.isSyncing ? "Syncing..." : "Sync Now") {
            Task { await appState.runSync(dryRun: false) }
        }
        .disabled(appState.isSyncing)
        .keyboardShortcut("s")

        Button("Dry Run") {
            Task { await appState.runSync(dryRun: true) }
        }
        .disabled(appState.isSyncing)

        Divider()

        Button("Open Dashboard") {
            NSApp.activate(ignoringOtherApps: true)
            if let window = NSApp.windows.first(where: { $0.identifier?.rawValue == "main" }) {
                window.makeKeyAndOrderFront(nil)
            } else {
                NSWorkspace.shared.open(URL(string: "assetsync://main")!)
            }
        }
        .keyboardShortcut("d")

        Divider()

        Toggle("Launch at Login", isOn: Binding(
            get: { appState.loginItemEnabled },
            set: { _ in appState.toggleLoginItem() }
        ))

        Divider()

        Button("Quit AssetSync") {
            NSApplication.shared.terminate(nil)
        }
        .keyboardShortcut("q")
    }
}
