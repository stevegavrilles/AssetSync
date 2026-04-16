import SwiftUI

/// Main window with sidebar navigation (NavigationSplitView).
struct ContentView: View {
    @EnvironmentObject private var appState: AppState
    @State private var selectedTab: SidebarItem = .dashboard

    enum SidebarItem: String, CaseIterable, Identifiable {
        case dashboard = "Dashboard"
        case mappings = "Mappings"
        case queues = "Queues"
        case logs = "Logs"
        case settings = "Settings"

        var id: String { rawValue }

        var icon: String {
            switch self {
            case .dashboard: return "gauge.with.dots.needle.33percent"
            case .mappings: return "arrow.left.arrow.right"
            case .queues: return "tray.full"
            case .logs: return "doc.text.magnifyingglass"
            case .settings: return "gearshape"
            }
        }
    }

    var body: some View {
        NavigationSplitView {
            List(SidebarItem.allCases, selection: $selectedTab) { item in
                Label(item.rawValue, systemImage: item.icon)
                    .tag(item)
            }
            .navigationSplitViewColumnWidth(min: 180, ideal: 200)
        } detail: {
            switch selectedTab {
            case .dashboard:
                DashboardView()
            case .mappings:
                MappingsView()
            case .queues:
                QueuesView()
            case .logs:
                LogsView()
            case .settings:
                SettingsView()
            }
        }
    }
}
