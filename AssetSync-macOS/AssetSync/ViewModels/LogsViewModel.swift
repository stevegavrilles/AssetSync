import SwiftUI
import AssetSyncCore
import UniformTypeIdentifiers

@MainActor
final class LogsViewModel: ObservableObject {
    @Published var entries: [LogEntry] = []
    @Published var searchText = ""
    @Published var selectedLevel: LogLevel?
    @Published var selectedSource: SourceSystem?

    var filteredEntries: [LogEntry] {
        entries.filter { entry in
            if let level = selectedLevel, entry.level < level { return false }
            if let source = selectedSource, entry.sourceSystem != source { return false }
            if !searchText.isEmpty {
                let text = searchText.lowercased()
                let match = (entry.serialNumber?.lowercased().contains(text) ?? false)
                    || (entry.deviceName?.lowercased().contains(text) ?? false)
                    || (entry.errorDetail?.lowercased().contains(text) ?? false)
                    || entry.action.lowercased().contains(text)
                if !match { return false }
            }
            return true
        }
    }

    func load(appState: AppState) async {
        guard let c = appState.container else { return }
        var filter = LogFilter()
        filter.limit = 1000
        entries = (try? await c.logRepository.getEntries(filter: filter)) ?? []
    }

    func exportCSV() {
        let header = "Timestamp,Level,Source,Action,Serial,Device,Success,Detail"
        let rows = filteredEntries.map { e in
            let ts = ISO8601DateFormatter().string(from: e.timestampUtc)
            let detail = (e.errorDetail ?? "").replacingOccurrences(of: "\"", with: "\"\"")
            return "\(ts),\(e.level.rawValue),\(e.sourceSystem.rawValue),\(e.action),\(e.serialNumber ?? ""),\(e.deviceName ?? ""),\(e.success),\"\(detail)\""
        }
        let csv = ([header] + rows).joined(separator: "\n")

        let panel = NSSavePanel()
        panel.allowedContentTypes = [.commaSeparatedText]
        panel.nameFieldStringValue = "assetsync-logs.csv"
        if panel.runModal() == .OK, let url = panel.url {
            try? csv.write(to: url, atomically: true, encoding: .utf8)
        }
    }
}
