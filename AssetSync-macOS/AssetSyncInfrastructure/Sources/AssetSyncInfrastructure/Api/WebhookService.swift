import Foundation
import AssetSyncCore

/// Sends notifications to Teams, Slack, or generic webhook endpoints.
public final class WebhookService: WebhookServiceProtocol, @unchecked Sendable {
    private let webhookURL: String?
    private let webhookType: String
    private let session: URLSession

    public init(webhookURL: String?, webhookType: String = "Generic", session: URLSession = .shared) {
        self.webhookURL = webhookURL
        self.webhookType = webhookType
        self.session = session
    }

    public func sendSyncNotification(_ summary: SyncRunSummary) async {
        guard let url = webhookURL, !url.isEmpty else { return }
        let payload = formatSyncPayload(summary)
        await post(to: url, json: payload)
    }

    public func sendConnectivityFailureNotification(serviceName: String, message: String) async {
        guard let url = webhookURL, !url.isEmpty else { return }
        let payload = formatAlertPayload(title: "Connectivity failure", message: "\(serviceName): \(message)")
        await post(to: url, json: payload)
    }

    public func sendCredentialErrorNotification(serviceName: String, message: String) async {
        guard let url = webhookURL, !url.isEmpty else { return }
        let payload = formatAlertPayload(title: "Credential error", message: "\(serviceName): \(message)")
        await post(to: url, json: payload)
    }

    public func testWebhook() async {
        guard let url = webhookURL, !url.isEmpty else { return }
        let payload = formatAlertPayload(title: "Test", message: "Asset Sync webhook test.")
        await post(to: url, json: payload)
    }

    // MARK: - Formatting

    private func formatSyncPayload(_ summary: SyncRunSummary) -> [String: Any] {
        switch webhookType {
        case "Teams":
            return [
                "type": "message",
                "attachments": [[
                    "contentType": "application/vnd.microsoft.card.adaptive",
                    "content": [
                        "type": "AdaptiveCard", "version": "1.0",
                        "body": [
                            ["type": "TextBlock", "text": "Asset Sync completed", "weight": "bolder", "size": "large"],
                            ["type": "FactSet", "facts": [
                                ["title": "Created", "value": "\(summary.created)"],
                                ["title": "Updated", "value": "\(summary.updated)"],
                                ["title": "Skipped", "value": "\(summary.skipped)"],
                                ["title": "Errors", "value": "\(summary.errors)"],
                            ]],
                        ],
                    ] as [String : Any],
                ]],
            ]
        case "Slack":
            return [
                "blocks": [[
                    "text": [
                        "type": "mrkdwn",
                        "text": "*Asset Sync* completed. Created: \(summary.created), Updated: \(summary.updated), Skipped: \(summary.skipped), Errors: \(summary.errors)",
                    ],
                ]],
            ]
        default:
            return [
                "created": summary.created, "updated": summary.updated,
                "skipped": summary.skipped, "errors": summary.errors,
                "dryRun": summary.dryRun, "syncRunId": summary.syncRunId,
            ]
        }
    }

    private func formatAlertPayload(title: String, message: String) -> [String: Any] {
        switch webhookType {
        case "Teams":
            return [
                "type": "message",
                "attachments": [[
                    "contentType": "application/vnd.microsoft.card.adaptive",
                    "content": ["type": "AdaptiveCard", "version": "1.0", "body": [
                        ["type": "TextBlock", "text": title, "weight": "bolder"],
                        ["type": "TextBlock", "text": message],
                    ]] as [String : Any],
                ]],
            ]
        case "Slack":
            return ["text": "\(title): \(message)"]
        default:
            return ["title": title, "message": message]
        }
    }

    private func post(to urlString: String, json: [String: Any]) async {
        guard let url = URL(string: urlString) else { return }
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try? JSONSerialization.data(withJSONObject: json)
        _ = try? await session.data(for: request)
    }
}
