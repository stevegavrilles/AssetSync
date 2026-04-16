import SwiftUI
import ServiceManagement

/// Menu bar + windowed SwiftUI app.
/// Registers as a Login Item via SMAppService so it auto-launches at login (Option C).
@main
struct AssetSyncApp: App {
    @StateObject private var appState = AppState()

    var body: some Scene {
        // Menu bar extra — always-visible status icon
        MenuBarExtra {
            MenuBarView(appState: appState)
        } label: {
            Image(systemName: appState.isSyncing ? "arrow.triangle.2.circlepath" : "arrow.triangle.2.circlepath.circle")
                .symbolEffect(.rotate, isActive: appState.isSyncing)
        }

        // Main settings/dashboard window (opened from menu bar)
        Window("AssetSync", id: "main") {
            ContentView()
                .environmentObject(appState)
                .frame(minWidth: 900, minHeight: 600)
        }
        .defaultSize(width: 1000, height: 700)

        // macOS Settings scene (Cmd+,)
        Settings {
            SettingsView()
                .environmentObject(appState)
        }
    }
}

/// Shared app state observable across the menu bar and main window.
@MainActor
final class AppState: ObservableObject {
    @Published var isSyncing = false
    @Published var lastSyncTime: Date?
    @Published var lastSyncSummary: String?
    @Published var loginItemEnabled = false

    private var syncTimer: Timer?
    // These are initialized by DependencyContainer on launch
    var container: DependencyContainer?

    init() {
        // Check current login item status
        if #available(macOS 13.0, *) {
            loginItemEnabled = SMAppService.mainApp.status == .enabled
        }
    }

    func toggleLoginItem() {
        if #available(macOS 13.0, *) {
            do {
                if loginItemEnabled {
                    try SMAppService.mainApp.unregister()
                } else {
                    try SMAppService.mainApp.register()
                }
                loginItemEnabled = SMAppService.mainApp.status == .enabled
            } catch {
                print("Login item toggle failed: \(error)")
            }
        }
    }

    func startScheduledSync(intervalHours: Int) {
        stopScheduledSync()
        let interval = TimeInterval(intervalHours * 3600)
        syncTimer = Timer.scheduledTimer(withTimeInterval: interval, repeats: true) { [weak self] _ in
            Task { @MainActor in
                await self?.runSync(dryRun: false)
            }
        }
    }

    func stopScheduledSync() {
        syncTimer?.invalidate()
        syncTimer = nil
    }

    func runSync(dryRun: Bool) async {
        guard !isSyncing, let engine = container?.syncEngine else { return }
        isSyncing = true
        defer { isSyncing = false }
        do {
            let summary = try await engine.runSync(dryRun: dryRun)
            lastSyncTime = summary.completedAtUtc
            lastSyncSummary = "Created: \(summary.created), Updated: \(summary.updated), Skipped: \(summary.skipped), Errors: \(summary.errors)"
        } catch {
            lastSyncSummary = "Sync failed: \(error.localizedDescription)"
        }
    }
}
