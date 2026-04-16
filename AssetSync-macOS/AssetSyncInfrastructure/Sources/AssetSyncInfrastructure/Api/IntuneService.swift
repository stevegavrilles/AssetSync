import Foundation
import MSAL
import AssetSyncCore

/// Fetches managed devices from Microsoft Graph via MSAL client credentials flow.
public final class IntuneService: IntuneServiceProtocol, @unchecked Sendable {
    private static let pageSize = 1000
    private static let graphBase = "https://graph.microsoft.com/v1.0"
    private static let pmTagRegex = /\bPM\d{7}\b/
    private let tenantId: String
    private let clientId: String
    private let getClientSecret: @Sendable () -> String
    private let session: URLSession

    public init(tenantId: String, clientId: String, getClientSecret: @escaping @Sendable () -> String,
                session: URLSession = .shared) {
        self.tenantId = tenantId
        self.clientId = clientId
        self.getClientSecret = getClientSecret
        self.session = session
    }

    public func getManagedDevices() async throws -> [Device] {
        let token = try await acquireToken()
        var devices: [Device] = []
        var url: String? = "\(Self.graphBase)/deviceManagement/managedDevices?$top=\(Self.pageSize)"

        while let currentURL = url {
            var request = URLRequest(url: URL(string: currentURL)!)
            request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
            let (data, _) = try await session.data(for: request)
            let json = try JSONSerialization.jsonObject(with: data) as? [String: Any]
            guard let values = json?["value"] as? [[String: Any]] else { break }

            for m in values {
                let serial = m["serialNumber"] as? String ?? ""
                let notes = m["notes"] as? String
                var mdmAssetTag: String?
                if let notes, let match = notes.firstMatch(of: Self.pmTagRegex) {
                    mdmAssetTag = String(match.output).uppercased()
                }

                var device = Device()
                device.normalizedSerial = SerialNumberNormalizer.normalize(serial)
                device.serialNumber = serial
                device.deviceName = m["deviceName"] as? String
                device.model = m["model"] as? String
                device.assignedUserUpn = m["userPrincipalName"] as? String
                device.osVersion = m["osVersion"] as? String
                device.deviceType = (m["deviceCategoryDisplayName"] as? String) ?? (m["managementAgent"] as? String)
                device.platformSource = "Intune"
                device.azureAdDeviceId = m["azureADDeviceId"] as? String
                device.operatingSystem = m["operatingSystem"] as? String
                device.mdmAssetTag = mdmAssetTag
                device.intuneNotes = notes
                device.intuneDeviceId = m["id"] as? String
                devices.append(device)
            }

            url = json?["@odata.nextLink"] as? String
        }

        return devices
    }

    public func writeBackAssetTag(intuneDeviceId: String, assetTag: String, existingNotes: String?) async throws -> Bool {
        let token = try await acquireToken()
        let tagLine = "Asset Tag: \(assetTag)"
        let updatedNotes: String
        if let existing = existingNotes, !existing.isEmpty {
            let replaced = existing.replacingOccurrences(
                of: #"Asset Tag:\s*PM\d{7}"#,
                with: tagLine,
                options: .regularExpression)
            updatedNotes = (replaced == existing)
                ? existing.trimmingCharacters(in: .whitespacesAndNewlines) + "\n" + tagLine
                : replaced
        } else {
            updatedNotes = tagLine
        }

        var request = URLRequest(url: URL(string: "\(Self.graphBase)/deviceManagement/managedDevices/\(intuneDeviceId)")!)
        request.httpMethod = "PATCH"
        request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try JSONSerialization.data(withJSONObject: ["notes": updatedNotes])

        let (_, response) = try await session.data(for: request)
        return (response as? HTTPURLResponse)?.statusCode ?? 0 < 300
    }

    // MARK: - MSAL Token Acquisition

    private func acquireToken() async throws -> String {
        let authority = try MSALAADAuthority(url: URL(string: "https://login.microsoftonline.com/\(tenantId)")!)
        let config = MSALPublicClientApplicationConfig(clientId: clientId, redirectUri: nil, authority: authority)
        // For confidential client, use MSAL's token acquisition with client credential
        // Note: MSAL for macOS uses MSALPublicClientApplication; for daemon/service flows
        // you'll use a direct OAuth2 client_credentials POST instead:
        return try await acquireTokenViaClientCredentials()
    }

    private func acquireTokenViaClientCredentials() async throws -> String {
        let url = URL(string: "https://login.microsoftonline.com/\(tenantId)/oauth2/v2.0/token")!
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/x-www-form-urlencoded", forHTTPHeaderField: "Content-Type")
        let body = [
            "client_id": clientId,
            "client_secret": getClientSecret(),
            "scope": "https://graph.microsoft.com/.default",
            "grant_type": "client_credentials",
        ].map { "\($0.key)=\($0.value.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? $0.value)" }
            .joined(separator: "&")
        request.httpBody = Data(body.utf8)

        let (data, _) = try await session.data(for: request)
        guard let json = try JSONSerialization.jsonObject(with: data) as? [String: Any],
              let token = json["access_token"] as? String else {
            throw AssetSyncError.snipeItRejected("Failed to acquire Intune token")
        }
        return token
    }
}
