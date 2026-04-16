import SwiftUI

struct MappingsView: View {
    @EnvironmentObject private var appState: AppState
    @StateObject private var viewModel = MappingsViewModel()
    @State private var selectedTab = "Models"

    var body: some View {
        VStack {
            Picker("Mapping Type", selection: $selectedTab) {
                Text("Models").tag("Models")
                Text("Users").tag("Users")
                Text("Builds").tag("Builds")
                Text("Categories").tag("Categories")
            }
            .pickerStyle(.segmented)
            .padding(.horizontal)

            switch selectedTab {
            case "Models":
                MappingTable(
                    items: viewModel.modelMappings,
                    columns: [("MDM Model", \.mdmModelString), ("Snipe-IT Model ID", { String($0.snipeItModelId) })],
                    onDelete: { viewModel.deleteModelMapping(id: $0) }
                )
            case "Users":
                MappingTable(
                    items: viewModel.userMappings,
                    columns: [("MDM User", \.mdmUserIdentifier), ("Snipe-IT User ID", { String($0.snipeItUserId) })],
                    onDelete: { viewModel.deleteUserMapping(id: $0) }
                )
            case "Builds":
                MappingTable(
                    items: viewModel.buildMappings,
                    columns: [("Build Number", \.buildNumber), ("Friendly Name", \.friendlyName)],
                    onDelete: { viewModel.deleteBuildMapping(id: $0) }
                )
            case "Categories":
                MappingTable(
                    items: viewModel.categoryMappings,
                    columns: [("Device Type", \.mdmDeviceType), ("Snipe-IT Category ID", { String($0.snipeItCategoryId) })],
                    onDelete: { viewModel.deleteCategoryMapping(id: $0) }
                )
            default:
                EmptyView()
            }
        }
        .navigationTitle("Mappings")
        .task { await viewModel.load(appState: appState) }
    }
}

/// Generic two-column table for any Identifiable mapping type.
private struct MappingTable<T: Identifiable>: View {
    let items: [T]
    let columns: [(String, (T) -> String)]
    let onDelete: (T.ID) -> Void

    var body: some View {
        Table(items) {
            TableColumn(columns[0].0) { item in Text(columns[0].1(item)) }
            TableColumn(columns[1].0) { item in Text(columns[1].1(item)) }
            TableColumn("") { item in
                Button(role: .destructive) { onDelete(item.id) } label: {
                    Image(systemName: "trash")
                }
                .buttonStyle(.borderless)
            }
            .width(40)
        }
    }
}
