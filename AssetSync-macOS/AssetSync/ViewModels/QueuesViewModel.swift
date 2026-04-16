import SwiftUI
import AssetSyncCore

@MainActor
final class QueuesViewModel: ObservableObject {
    @Published var pendingModels: [String] = []
    @Published var unmatchedUsers: [String] = []
    @Published var duplicateSerials: [String] = []

    func load(appState: AppState) async {
        guard let c = appState.container else { return }

        // Pending models: distinct models from recent "skip" log entries with "Pending model mapping"
        var filter = LogFilter()
        filter.action = "skip"
        filter.limit = 5000
        let skipLogs = (try? await c.logRepository.getEntries(filter: filter)) ?? []
        let modelSet = Set(skipLogs.compactMap { entry -> String? in
            guard let detail = entry.errorDetail, detail.hasPrefix("Pending model mapping:") else { return nil }
            return detail.replacingOccurrences(of: "Pending model mapping: ", with: "").trimmingCharacters(in: .whitespaces)
        })
        pendingModels = modelSet.sorted()

        // Duplicate serials: from "error" entries mentioning "Multiple Snipe-IT assets"
        var errorFilter = LogFilter()
        errorFilter.action = "error"
        errorFilter.limit = 5000
        let errorLogs = (try? await c.logRepository.getEntries(filter: errorFilter)) ?? []
        let dupeSet = Set(errorLogs.compactMap { entry -> String? in
            guard let detail = entry.errorDetail, detail.contains("Multiple Snipe-IT assets") else { return nil }
            return entry.serialNumber
        })
        duplicateSerials = dupeSet.sorted()

        // Unmatched users would require additional tracking — placeholder
        unmatchedUsers = []
    }

    func ignoreModel(_ model: String, appState: AppState) async {
        guard let c = appState.container else { return }
        try? await c.mappingRepository.addModelIgnore(model)
        pendingModels.removeAll { $0 == model }
    }
}
