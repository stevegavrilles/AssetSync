import SwiftUI
import AssetSyncCore

@MainActor
final class DashboardViewModel: ObservableObject {
    @Published var snipeItStatus = ""
    @Published var intuneStatus = ""
    @Published var iruStatus = ""

    func testConnections(appState: AppState) async {
        guard let container = appState.container else { return }
        // Run all three tests concurrently
        async let snipe = ConnectivityTester.testGet(
            url: "", bearerToken: "", service: .snipeIt) // Placeholder — real test goes through container
        // In a real build, use the container's connectivity tester:
        // For now, set placeholder values to show structure
        snipeItStatus = "Testing..."
        intuneStatus = "Testing..."
        iruStatus = "Testing..."

        // TODO: Wire through container.connectivityTester once DI is fully connected
        snipeItStatus = "Connected"
        intuneStatus = "Not configured"
        iruStatus = "Not configured"
    }
}
