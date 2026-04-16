import Foundation
import AssetSyncCore

/// Snipe-IT REST API client with rate-limit retry.
public final class SnipeItService: SnipeItServiceProtocol, @unchecked Sendable {
    private static let maxRetries = 3
    private let baseURL: String
    private let getApiKey: @Sendable () -> String
    private let session: URLSession

    public init(baseURL: String, getApiKey: @escaping @Sendable () -> String, session: URLSession = .shared) {
        self.baseURL = baseURL.hasSuffix("/") ? String(baseURL.dropLast()) : baseURL
        self.getApiKey = getApiKey
        self.session = session
    }

    public func getAssetBySerial(_ normalizedSerial: String) async throws -> Device? {
        let list = try await searchAssetsBySerial(normalizedSerial)
        return list.count == 1 ? list[0] : nil
    }

    public func searchAssetsBySerial(_ searchTerm: String) async throws -> [Device] {
        let encoded = searchTerm.addingPercentEncoding(withAllowedCharacters: .urlPathAllowed) ?? searchTerm
        let (data, response) = try await sendWithRetry(.get, path: "/api/v1/hardware/byserial/\(encoded)")

        if (response as? HTTPURLResponse)?.statusCode == 404 { return [] }
        guard (response as? HTTPURLResponse)?.statusCode.isSuccessful == true else { return [] }

        let json = try JSONSerialization.jsonObject(with: data)

        if let arr = json as? [[String: Any]] {
            return arr.compactMap(parseDevice)
        }
        if let obj = json as? [String: Any] {
            if let rows = obj["rows"] as? [[String: Any]] {
                return rows.compactMap(parseDevice)
            }
            if obj["id"] != nil {
                return [parseDevice(obj)].compactMap { $0 }
            }
        }
        return []
    }

    public func createAsset(_ device: Device) async throws -> Device? {
        var payload: [String: Any] = [
            "name": device.deviceName ?? device.serialNumber ?? "",
            "serial": device.serialNumber ?? "",
            "model_id": device.snipeItModelId ?? 0,
            "status_id": 1,
        ]
        if let userId = device.snipeItAssignedUserId { payload["assigned_to"] = userId }
        if let catId = device.snipeItCategoryId { payload["category_id"] = catId }
        let tag = device.mdmAssetTag?.isEmpty == false
            ? device.mdmAssetTag!
            : "TEMP\(UUID().uuidString.prefix(8).uppercased())"
        payload["asset_tag"] = tag

        let body = try JSONSerialization.data(withJSONObject: payload)
        let (data, _) = try await sendWithRetry(.post, path: "/api/v1/hardware", body: body)

        guard let obj = try JSONSerialization.jsonObject(with: data) as? [String: Any] else { return nil }

        if let status = obj["status"] as? String, status == "error" {
            let messages = obj["messages"].map { "\($0)" } ?? "Unknown error"
            throw AssetSyncError.snipeItRejected(messages)
        }

        let created = (obj["payload"] as? [String: Any]) ?? obj
        var result = device
        result.snipeItAssetId = created["id"] as? Int
        return result
    }

    public func updateAsset(assetId: Int, updates: [String: Any]) async throws -> Bool {
        guard !updates.isEmpty else { return true }

        var payload: [String: Any] = [:]
        var customFields: [String: Any] = [:]
        for (key, value) in updates {
            if key == "os_version" || key == "windows_feature_update" {
                customFields[key] = value
            } else {
                payload[key] = value
            }
        }
        if !customFields.isEmpty { payload["custom_fields"] = customFields }

        let body = try JSONSerialization.data(withJSONObject: payload)
        let (_, response) = try await sendWithRetry(.patch, path: "/api/v1/hardware/\(assetId)", body: body)
        return (response as? HTTPURLResponse)?.statusCode.isSuccessful == true
    }

    public func getModels() async throws -> [SnipeItLookup] {
        try await fetchLookups(endpoint: "/api/v1/models")
    }

    public func getCategories() async throws -> [SnipeItLookup] {
        try await fetchLookups(endpoint: "/api/v1/categories")
    }

    public func getUsers() async throws -> [SnipeItLookup] {
        var list: [SnipeItLookup] = []
        var offset = 0
        let limit = 500
        while true {
            let (data, response) = try await sendWithRetry(.get, path: "/api/v1/users?limit=\(limit)&offset=\(offset)")
            guard (response as? HTTPURLResponse)?.statusCode.isSuccessful == true else { break }
            guard let obj = try JSONSerialization.jsonObject(with: data) as? [String: Any],
                  let rows = obj["rows"] as? [[String: Any]] else { break }
            for u in rows {
                let id = u["id"] as? Int ?? 0
                let first = u["first_name"] as? String ?? ""
                let last = u["last_name"] as? String ?? ""
                let uname = u["username"] as? String ?? ""
                let name = (!first.isEmpty || !last.isEmpty) ? "\(first) \(last)".trimmingCharacters(in: .whitespaces) : uname
                list.append(SnipeItLookup(id: id, name: name))
            }
            if rows.count < limit { break }
            offset += rows.count
        }
        return list
    }

    // MARK: - Private

    private enum HTTPMethodString: String { case get = "GET", post = "POST", patch = "PATCH" }

    private func sendWithRetry(_ method: HTTPMethodString, path: String, body: Data? = nil) async throws -> (Data, URLResponse) {
        for attempt in 0...Self.maxRetries {
            var request = URLRequest(url: URL(string: "\(baseURL)\(path)")!)
            request.httpMethod = method.rawValue
            request.setValue("Bearer \(getApiKey())", forHTTPHeaderField: "Authorization")
            request.setValue("application/json", forHTTPHeaderField: "Accept")
            if let body {
                request.httpBody = body
                request.setValue("application/json", forHTTPHeaderField: "Content-Type")
            }
            let (data, response) = try await session.data(for: request)
            let status = (response as? HTTPURLResponse)?.statusCode ?? 0
            if status != 429 || attempt == Self.maxRetries {
                return (data, response)
            }
            let retryAfter = (response as? HTTPURLResponse)?
                .value(forHTTPHeaderField: "Retry-After")
                .flatMap(Double.init) ?? pow(2, Double(attempt + 1))
            try await Task.sleep(for: .seconds(retryAfter))
        }
        fatalError("Unreachable")
    }

    private func fetchLookups(endpoint: String) async throws -> [SnipeItLookup] {
        var list: [SnipeItLookup] = []
        var offset = 0
        let limit = 500
        while true {
            let (data, response) = try await sendWithRetry(.get, path: "\(endpoint)?limit=\(limit)&offset=\(offset)")
            guard (response as? HTTPURLResponse)?.statusCode.isSuccessful == true else { break }
            guard let obj = try JSONSerialization.jsonObject(with: data) as? [String: Any],
                  let rows = obj["rows"] as? [[String: Any]] else { break }
            for item in rows {
                let id = item["id"] as? Int ?? 0
                let name = item["name"] as? String ?? ""
                list.append(SnipeItLookup(id: id, name: name))
            }
            if rows.count < limit { break }
            offset += rows.count
        }
        return list
    }

    private func parseDevice(_ r: [String: Any]) -> Device? {
        guard r["id"] != nil else { return nil }
        var d = Device()
        d.snipeItAssetId = r["id"] as? Int
        d.serialNumber = r["serial"] as? String
        d.normalizedSerial = SerialNumberNormalizer.normalize(d.serialNumber)
        d.deviceName = (r["name"] as? String)?.removingHTMLEntities
        d.snipeItModelId = (r["model"] as? [String: Any])?["id"] as? Int
        d.snipeItAssignedUserId = (r["assigned_to"] as? [String: Any])?["id"] as? Int
        d.snipeItAssetTag = (r["asset_tag"] as? String)?.removingHTMLEntities
        return d
    }
}

public enum AssetSyncError: LocalizedError {
    case snipeItRejected(String)

    public var errorDescription: String? {
        switch self {
        case .snipeItRejected(let msg): return "Snipe-IT create rejected: \(msg)"
        }
    }
}

private extension Int {
    var isSuccessful: Bool { (200..<300).contains(self) }
}

private extension String {
    /// Basic HTML entity decoding for Snipe-IT responses.
    var removingHTMLEntities: String {
        replacingOccurrences(of: "&amp;", with: "&")
            .replacingOccurrences(of: "&lt;", with: "<")
            .replacingOccurrences(of: "&gt;", with: ">")
            .replacingOccurrences(of: "&quot;", with: "\"")
            .replacingOccurrences(of: "&#39;", with: "'")
    }
}
