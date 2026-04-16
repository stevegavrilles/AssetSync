import SwiftUI
import AssetSyncCore

@MainActor
final class MappingsViewModel: ObservableObject {
    @Published var modelMappings: [ModelMapping] = []
    @Published var userMappings: [UserMapping] = []
    @Published var buildMappings: [BuildMapping] = []
    @Published var categoryMappings: [CategoryMapping] = []

    private weak var appState: AppState?

    func load(appState: AppState) async {
        self.appState = appState
        guard let c = appState.container else { return }
        modelMappings = (try? await c.mappingRepository.getModelMappings()) ?? []
        userMappings = (try? await c.mappingRepository.getUserMappings()) ?? []
        buildMappings = (try? await c.mappingRepository.getBuildMappings()) ?? []
        categoryMappings = (try? await c.mappingRepository.getCategoryMappings()) ?? []
    }

    func deleteModelMapping(id: Int) {
        Task {
            guard let c = appState?.container else { return }
            try? await c.mappingRepository.deleteModelMapping(id: id)
            modelMappings.removeAll { $0.id == id }
        }
    }

    func deleteUserMapping(id: Int) {
        Task {
            guard let c = appState?.container else { return }
            try? await c.mappingRepository.deleteUserMapping(id: id)
            userMappings.removeAll { $0.id == id }
        }
    }

    func deleteBuildMapping(id: Int) {
        Task {
            guard let c = appState?.container else { return }
            try? await c.mappingRepository.deleteBuildMapping(id: id)
            buildMappings.removeAll { $0.id == id }
        }
    }

    func deleteCategoryMapping(id: Int) {
        Task {
            guard let c = appState?.container else { return }
            try? await c.mappingRepository.deleteCategoryMapping(id: id)
            categoryMappings.removeAll { $0.id == id }
        }
    }
}
