using AssetSync.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AssetSync.Service;

public class SyncWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<SyncWorker> _logger;

    public SyncWorker(IServiceProvider services, ILogger<SyncWorker> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SyncWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var syncEngine = scope.ServiceProvider.GetRequiredService<ISyncEngine>();
                var config = scope.ServiceProvider.GetRequiredService<IConfigRepository>();

                var dryRun = await config.GetDryRunDefaultAsync(stoppingToken);
                _logger.LogInformation("Starting scheduled sync (dryRun={DryRun}).", dryRun);

                var summary = await syncEngine.RunSyncAsync(dryRun, stoppingToken);
                _logger.LogInformation(
                    "Sync completed. Created={Created}, Updated={Updated}, Skipped={Skipped}, Errors={Errors}",
                    summary.Created, summary.Updated, summary.Skipped, summary.Errors);

                var intervalHours = await config.GetSyncIntervalHoursAsync(stoppingToken);
                var delay = TimeSpan.FromHours(Math.Max(intervalHours, 1));
                _logger.LogInformation("Next sync in {Hours} hour(s).", delay.TotalHours);
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sync failed. Retrying in 5 minutes.");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("SyncWorker stopped.");
    }
}
