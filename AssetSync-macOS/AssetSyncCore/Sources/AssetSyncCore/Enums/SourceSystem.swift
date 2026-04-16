import Foundation

public enum SourceSystem: String, Codable, Sendable, CaseIterable {
    case intune = "Intune"
    case iru = "Iru"
    case snipeIt = "SnipeIt"
    case application = "Application"
}
