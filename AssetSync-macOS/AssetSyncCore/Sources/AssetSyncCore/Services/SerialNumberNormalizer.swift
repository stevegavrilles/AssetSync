import Foundation

public enum SerialNumberNormalizer {
    /// Strips whitespace and non-alphanumeric characters, uppercases.
    /// e.g. " lpg2-v3q 402 " → "LPG2V3Q402"
    public static func normalize(_ serial: String?) -> String {
        guard let serial, !serial.trimmingCharacters(in: .whitespaces).isEmpty else {
            return ""
        }
        return serial
            .trimmingCharacters(in: .whitespaces)
            .uppercased()
            .unicodeScalars
            .filter { CharacterSet.alphanumerics.contains($0) }
            .map { String($0) }
            .joined()
    }
}
