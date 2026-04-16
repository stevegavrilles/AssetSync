import Foundation

/// Maps Windows build number (e.g. from osVersion 10.0.22631.4890) to friendly name.
public struct BuildVersionMapper: Sendable {
    private let mappingRepository: MappingRepositoryProtocol

    public init(mappingRepository: MappingRepositoryProtocol) {
        self.mappingRepository = mappingRepository
    }

    /// Extract build number from osVersion (e.g. 10.0.22631.4890 → 22631) and resolve friendly name.
    public func getFriendlyName(for osVersion: String?) async -> String? {
        guard let build = Self.extractBuildNumber(from: osVersion) else { return nil }
        if let mapping = try? await mappingRepository.getBuildMapping(buildNumber: build) {
            return mapping.friendlyName
        }
        return osVersion
    }

    /// Extracts the third segment of a dotted version string: "10.0.22631.4890" → "22631"
    public static func extractBuildNumber(from osVersion: String?) -> String? {
        guard let osVersion, !osVersion.trimmingCharacters(in: .whitespaces).isEmpty else { return nil }
        let parts = osVersion.split(separator: ".")
        guard parts.count >= 3, Int(parts[2]) != nil else { return nil }
        return String(parts[2])
    }
}
