using AssetSync.Core.Models;

namespace AssetSync.Core.Interfaces;

public interface IWebhookService
{
    Task SendSyncNotificationAsync(SyncRunSummary summary, CancellationToken cancellationToken = default);
    Task SendConnectivityFailureNotificationAsync(string serviceName, string message, CancellationToken cancellationToken = default);
    Task SendCredentialErrorNotificationAsync(string serviceName, string message, CancellationToken cancellationToken = default);
    Task TestWebhookAsync(CancellationToken cancellationToken = default);
}
