import SwiftUI
import AssetSyncCore

struct LogsView: View {
    @EnvironmentObject private var appState: AppState
    @StateObject private var viewModel = LogsViewModel()

    var body: some View {
        VStack(spacing: 0) {
            // Filter bar
            HStack {
                TextField("Search...", text: $viewModel.searchText)
                    .textFieldStyle(.roundedBorder)
                    .frame(maxWidth: 250)

                Picker("Level", selection: $viewModel.selectedLevel) {
                    Text("All").tag(nil as LogLevel?)
                    ForEach(LogLevel.allCases, id: \.self) { level in
                        Text(level.rawValue).tag(level as LogLevel?)
                    }
                }
                .frame(width: 120)

                Picker("Source", selection: $viewModel.selectedSource) {
                    Text("All").tag(nil as SourceSystem?)
                    ForEach(SourceSystem.allCases, id: \.self) { source in
                        Text(source.rawValue).tag(source as SourceSystem?)
                    }
                }
                .frame(width: 120)

                Spacer()

                Button("Refresh") {
                    Task { await viewModel.load(appState: appState) }
                }

                Button("Export CSV") {
                    viewModel.exportCSV()
                }
            }
            .padding()

            // Log table
            Table(viewModel.filteredEntries) {
                TableColumn("Time") { entry in
                    Text(entry.timestampUtc, style: .date)
                    + Text(" ")
                    + Text(entry.timestampUtc, style: .time)
                }
                .width(min: 140, ideal: 160)

                TableColumn("Level") { entry in
                    Text(entry.level.rawValue)
                        .foregroundStyle(colorForLevel(entry.level))
                }
                .width(60)

                TableColumn("Source") { entry in Text(entry.sourceSystem.rawValue) }
                    .width(80)

                TableColumn("Action") { entry in Text(entry.action) }
                    .width(80)

                TableColumn("Serial") { entry in Text(entry.serialNumber ?? "") }
                    .width(100)

                TableColumn("Device") { entry in Text(entry.deviceName ?? "") }
                    .width(min: 100, ideal: 150)

                TableColumn("Detail") { entry in Text(entry.errorDetail ?? "") }
            }
        }
        .navigationTitle("Logs")
        .task { await viewModel.load(appState: appState) }
    }

    private func colorForLevel(_ level: LogLevel) -> Color {
        switch level {
        case .debug: return .secondary
        case .info: return .primary
        case .warning: return .orange
        case .error: return .red
        }
    }
}
