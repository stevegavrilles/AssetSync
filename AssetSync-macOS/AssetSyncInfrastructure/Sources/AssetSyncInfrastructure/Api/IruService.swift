import Foundation
import AssetSyncCore

/// Fetches devices from the Iru (formerly Kandji) API with pagination.
public final class IruService: IruServiceProtocol, @unchecked Sendable {
    private static let pageSize = 300
    private let baseURL: String
    private let getToken: @Sendable () -> String
    private let session: URLSession

    public init(baseURL: String, getToken: @escaping @Sendable () -> String, session: URLSession = .shared) {
        self.baseURL = baseURL.trimmingSuffix("/")
        self.getToken = getToken
        self.session = session
    }

    public func getDevices() async throws -> [Device] {
        var devices: [Device] = []
        var offset = 0

        while true {
            var request = URLRequest(url: URL(string: "\(baseURL)/api/v1/devices?limit=\(Self.pageSize)&offset=\(offset)")!)
            request.setValue("Bearer \(getToken())", forHTTPHeaderField: "Authorization")

            let (data, _) = try await session.data(for: request)
            let json = try JSONSerialization.jsonObject(with: data)

            let array: [[String: Any]]
            if let arr = json as? [[String: Any]] {
                array = arr
            } else if let obj = json as? [String: Any], let results = obj["results"] as? [[String: Any]] {
                array = results
            } else {
                break
            }

            for r in array {
                let serial = r["serial_number"] as? String ?? ""
                let userEmail: String? = (r["user"] as? [String: Any])?["email"] as? String
                let deviceId = (r["device_id"] as? String) ?? (r["id"] as? String)
                let assetTag = (r["asset_tag"] as? String)?.trimmingCharacters(in: .whitespaces)

                var device = Device()
                device.normalizedSerial = SerialNumberNormalizer.normalize(serial)
                device.serialNumber = serial
                device.deviceName = r["device_name"] as? String
                device.model = r["model"] as? String
                device.assignedUserUpn = userEmail
                device.osVersion = r["os_version"] as? String
                device.platformSource = "Iru"
                device.iruDeviceId = deviceId
                device.deviceType = r["device_family"] as? String
                device.mdmAssetTag = (assetTag?.isEmpty ?? true) ? nil : assetTag
                devices.append(device)
            }

            if array.count < Self.pageSize { break }
            offset += array.count
        }

        return devices
    }

    public func writeBackAssetTag(iruDeviceId: String, assetTag: String) async throws -> Bool {
        var request = URLRequest(url: URL(string: "\(baseURL)/api/v1/devices/\(iruDeviceId)")!)
        request.httpMethod = "PATCH"
        request.setValue("Bearer \(getToken())", forHTTPHeaderField: "Authorization")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try JSONSerialization.data(withJSONObject: ["asset_tag": assetTag])

        let (_, response) = try await session.data(for: request)
        return (response as? HTTPURLResponse)?.statusCode.isSuccessful ?? false
    }
}

private extension Int {
    var isSuccessful: Bool { (200..<300).contains(self) }
}

private extension String {
    func trimmingSuffix(_ suffix: String) -> String {
        hasSuffix(suffix) ? String(dropLast(suffix.count)) : self
    }
}
