using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AssetSync.Service;

public class SyncWorker : BackgroundService
{
    private readonly ILogger<SyncWorker> _logger;

    public SyncWorker(ILogger<SyncWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SyncWorker started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
            // TODO: Call ISyncEngine.RunSyncAsync when wired to DI
        }
    }
}
