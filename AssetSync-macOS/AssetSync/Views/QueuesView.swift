import SwiftUI

struct QueuesView: View {
    @EnvironmentObject private var appState: AppState
    @StateObject private var viewModel = QueuesViewModel()
    @State private var selectedTab = "PendingModels"

    var body: some View {
        VStack {
            Picker("Queue", selection: $selectedTab) {
                Text("Pending Models").tag("PendingModels")
                Text("Unmatched Users").tag("UnmatchedUsers")
                Text("Duplicate Serials").tag("DuplicateSerials")
            }
            .pickerStyle(.segmented)
            .padding(.horizontal)

            switch selectedTab {
            case "PendingModels":
                List(viewModel.pendingModels, id: \.self) { model in
                    HStack {
                        Text(model)
                        Spacer()
                        Button("Ignore") {
                            Task { await viewModel.ignoreModel(model, appState: appState) }
                        }
                        .buttonStyle(.bordered)
                        .controlSize(.small)
                    }
                }
                .overlay {
                    if viewModel.pendingModels.isEmpty {
                        ContentUnavailableView("No Pending Models",
                            systemImage: "checkmark.circle",
                            description: Text("All device models have been mapped."))
                    }
                }
            case "UnmatchedUsers":
                List(viewModel.unmatchedUsers, id: \.self) { user in
                    Text(user)
                }
                .overlay {
                    if viewModel.unmatchedUsers.isEmpty {
                        ContentUnavailableView("No Unmatched Users",
                            systemImage: "checkmark.circle",
                            description: Text("All users have been mapped."))
                    }
                }
            case "DuplicateSerials":
                List(viewModel.duplicateSerials, id: \.self) { serial in
                    Text(serial)
                }
                .overlay {
                    if viewModel.duplicateSerials.isEmpty {
                        ContentUnavailableView("No Duplicate Serials",
                            systemImage: "checkmark.circle",
                            description: Text("No duplicate serial numbers found."))
                    }
                }
            default:
                EmptyView()
            }
        }
        .navigationTitle("Queues")
        .task { await viewModel.load(appState: appState) }
    }
}
