using System.Text;
using System.Text.Json;
using AssetSync.Core.Interfaces;
using AssetSync.Core.Models;

namespace AssetSync.Infrastructure.Api;

public class WebhookService : IWebhookService
{
    private readonly string? _webhookUrl;
    private readonly string _webhookType;
    private readonly IHttpClientFactory _httpClientFactory;

    public WebhookService(string? webhookUrl, string webhookType, IHttpClientFactory httpClientFactory)
    {
        _webhookUrl = webhookUrl;
        _webhookType = webhookType;
        _httpClientFactory = httpClientFactory;
    }

    public async Task SendSyncNotificationAsync(SyncRunSummary summary, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_webhookUrl)) return;
        var (content, contentType) = FormatSyncPayload(summary);
        var client = _httpClientFactory.CreateClient();
        var body = new StringContent(content, Encoding.UTF8, contentType);
        await client.PostAsync(_webhookUrl, body, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendConnectivityFailureNotificationAsync(string serviceName, string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_webhookUrl)) return;
        var (content, contentType) = FormatAlertPayload("Connectivity failure", $"{serviceName}: {message}");
        var client = _httpClientFactory.CreateClient();
        var body = new StringContent(content, Encoding.UTF8, contentType);
        await client.PostAsync(_webhookUrl, body, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendCredentialErrorNotificationAsync(string serviceName, string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_webhookUrl)) return;
        var (content, contentType) = FormatAlertPayload("Credential error", $"{serviceName}: {message}");
        var client = _httpClientFactory.CreateClient();
        var body = new StringContent(content, Encoding.UTF8, contentType);
        await client.PostAsync(_webhookUrl, body, cancellationToken).ConfigureAwait(false);
    }

    public async Task TestWebhookAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_webhookUrl)) return;
        var (content, contentType) = FormatAlertPayload("Test", "Asset Sync webhook test.");
        var client = _httpClientFactory.CreateClient();
        var body = new StringContent(content, Encoding.UTF8, contentType);
        await client.PostAsync(_webhookUrl, body, cancellationToken).ConfigureAwait(false);
    }

    private (string Content, string ContentType) FormatSyncPayload(SyncRunSummary summary)
    {
        if (_webhookType == "Teams")
        {
            var card = new
            {
                type = "message",
                attachments = new[]
                {
                    new
                    {
                        contentType = "application/vnd.microsoft.card.adaptive",
                        content = new
                        {
                            type = "AdaptiveCard",
                            version = "1.0",
                            body = new object[]
                            {
                                new { type = "TextBlock", text = "Asset Sync completed", weight = "bolder", size = "large" },
                                new { type = "FactSet", facts = new[] { new { title = "Created", value = summary.Created.ToString() }, new { title = "Updated", value = summary.Updated.ToString() }, new { title = "Skipped", value = summary.Skipped.ToString() }, new { title = "Errors", value = summary.Errors.ToString() } } }
                            }
                        }
                    }
                }
            };
            return (JsonSerializer.Serialize(card), "application/json");
        }
        if (_webhookType == "Slack")
        {
            var block = new { text = new { type = "mrkdwn", text = $"*Asset Sync* completed. Created: {summary.Created}, Updated: {summary.Updated}, Skipped: {summary.Skipped}, Errors: {summary.Errors}" } };
            return (JsonSerializer.Serialize(new { blocks = new[] { block } }), "application/json");
        }
        return (JsonSerializer.Serialize(summary), "application/json");
    }

    private (string Content, string ContentType) FormatAlertPayload(string title, string message)
    {
        if (_webhookType == "Teams")
        {
            var card = new { type = "message", attachments = new[] { new { contentType = "application/vnd.microsoft.card.adaptive", content = new { type = "AdaptiveCard", version = "1.0", body = new object[] { new { type = "TextBlock", text = title, weight = "bolder" }, new { type = "TextBlock", text = message } } } } } };
            return (JsonSerializer.Serialize(card), "application/json");
        }
        if (_webhookType == "Slack")
            return (JsonSerializer.Serialize(new { text = $"{title}: {message}" }), "application/json");
        return (JsonSerializer.Serialize(new { title, message }), "application/json");
    }
}
