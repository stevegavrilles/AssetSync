using AssetSync.Core.Enums;
using AssetSync.Core.Interfaces;
using AssetSync.Core.Models;
using Microsoft.Extensions.Logging;
using LogLevel = AssetSync.Core.Enums.LogLevel;

namespace AssetSync.Core.Services;

public class SyncEngine : ISyncEngine
{
    private readonly IIntuneService _intuneService;
    private readonly IIruService _iruService;
    private readonly ISnipeItService _snipeItService;
    private readonly ILogRepository _logRepository;
    private readonly IMappingRepository _mappingRepository;
    private readonly IConfigRepository _configRepository;
    private readonly IWebhookService _webhookService;
    private readonly IConnectivityTester _connectivityTester;
    private readonly DeviceMerger _merger;
    private readonly ConflictResolver _resolver;
    private readonly BuildVersionMapper _buildMapper;
    private readonly ILogger<SyncEngine> _logger;

    public SyncEngine(
        IIntuneService intuneService,
        IIruService iruService,
        ISnipeItService snipeItService,
        ILogRepository logRepository,
        IMappingRepository mappingRepository,
        IConfigRepository configRepository,
        IWebhookService webhookService,
        IConnectivityTester connectivityTester,
        DeviceMerger merger,
        ConflictResolver resolver,
        BuildVersionMapper buildMapper,
        ILogger<SyncEngine> logger)
    {
        _intuneService = intuneService;
        _iruService = iruService;
        _snipeItService = snipeItService;
        _logRepository = logRepository;
        _mappingRepository = mappingRepository;
        _configRepository = configRepository;
        _webhookService = webhookService;
        _connectivityTester = connectivityTester;
        _merger = merger;
        _resolver = resolver;
        _buildMapper = buildMapper;
        _logger = logger;
    }

    public async Task<SyncRunSummary> RunSyncAsync(bool dryRun, CancellationToken cancellationToken = default)
    {
        var runId = Guid.NewGuid().ToString();
        var summary = new SyncRunSummary
        {
            SyncRunId = runId,
            StartedAtUtc = DateTimeOffset.UtcNow,
            DryRun = dryRun
        };

        await LogAsync(runId, LogLevel.Info, SourceSystem.Application, dryRun ? "sync_start_dry" : "sync_start", null, null, true, null, cancellationToken).ConfigureAwait(false);

        try
        {
            var snipeStatus = await _connectivityTester.TestSnipeItAsync(cancellationToken).ConfigureAwait(false);
            summary.SnipeItReachable = snipeStatus.State == ConnectionState.Connected;
            if (!summary.SnipeItReachable)
            {
                await _webhookService.SendConnectivityFailureNotificationAsync("Snipe-IT", snipeStatus.Message ?? "Unreachable", cancellationToken).ConfigureAwait(false);
                summary.CompletedAtUtc = DateTimeOffset.UtcNow;
                await LogAsync(runId, LogLevel.Error, SourceSystem.Application, "sync_complete", null, null, false, "Snipe-IT unreachable", cancellationToken).ConfigureAwait(false);
                await _webhookService.SendSyncNotificationAsync(summary, cancellationToken).ConfigureAwait(false);
                return summary;
            }

            var intuneStatus = await _connectivityTester.TestIntuneAsync(cancellationToken).ConfigureAwait(false);
            summary.IntuneReachable = intuneStatus.State == ConnectionState.Connected;
            var iruStatus = await _connectivityTester.TestIruAsync(cancellationToken).ConfigureAwait(false);
            summary.IruReachable = iruStatus.State == ConnectionState.Connected;

            if (!summary.IntuneReachable && !summary.IruReachable)
            {
                await _webhookService.SendConnectivityFailureNotificationAsync("Intune and Iru", "Both MDM sources unreachable", cancellationToken).ConfigureAwait(false);
                summary.CompletedAtUtc = DateTimeOffset.UtcNow;
                await LogAsync(runId, LogLevel.Error, SourceSystem.Application, "sync_complete", null, null, false, "No MDM source available", cancellationToken).ConfigureAwait(false);
                await _webhookService.SendSyncNotificationAsync(summary, cancellationToken).ConfigureAwait(false);
                return summary;
            }

            var intuneList = summary.IntuneReachable
                ? await _intuneService.GetManagedDevicesAsync(cancellationToken).ConfigureAwait(false)
                : Array.Empty<Device>();
            var iruList = summary.IruReachable
                ? await _iruService.GetDevicesAsync(cancellationToken).ConfigureAwait(false)
                : Array.Empty<Device>();

            var merged = _merger.Merge(intuneList, iruList);

            foreach (var device in merged)
            {
                if (string.IsNullOrEmpty(device.NormalizedSerial))
                    device.NormalizedSerial = SerialNumberNormalizer.Normalize(device.SerialNumber);

                var existing = await _snipeItService.SearchAssetsBySerialAsync(device.NormalizedSerial, cancellationToken).ConfigureAwait(false);
                if (existing.Count > 1)
                {
                    summary.Errors++;
                    await LogAsync(runId, LogLevel.Error, SourceSystem.SnipeIt, "error", device.SerialNumber, device.DeviceName, false, "Multiple Snipe-IT assets with same serial", cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (existing.Count == 0)
                {
                    var modelMapping = await _mappingRepository.GetModelMappingAsync(device.Model ?? "", cancellationToken).ConfigureAwait(false);
                    if (modelMapping == null)
                    {
                        summary.Skipped++;
                        await LogAsync(runId, LogLevel.Warning, SourceSystem.Application, "skip", device.SerialNumber, device.DeviceName, true, "Pending model mapping", cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    device.SnipeItModelId = modelMapping.SnipeItModelId;
                    device.WindowsFeatureUpdate = await _buildMapper.GetFriendlyNameAsync(device.OsVersion, cancellationToken).ConfigureAwait(false);
                    var catMapping = await _mappingRepository.GetCategoryMappingAsync(device.DeviceType ?? "", cancellationToken).ConfigureAwait(false);
                    if (catMapping != null) device.SnipeItCategoryId = catMapping.SnipeItCategoryId;
                    if (!dryRun)
                    {
                        var created = await _snipeItService.CreateAssetAsync(device, cancellationToken).ConfigureAwait(false);
                        if (created != null)
                        {
                            summary.Created++;
                            await LogAsync(runId, LogLevel.Info, SourceSystem.SnipeIt, "create", device.SerialNumber, device.DeviceName, true, null, cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            summary.Errors++;
                            await LogAsync(runId, LogLevel.Error, SourceSystem.SnipeIt, "error", device.SerialNumber, device.DeviceName, false, "Create failed", cancellationToken).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        summary.Created++;
                        await LogAsync(runId, LogLevel.Info, SourceSystem.Application, "create", device.SerialNumber, device.DeviceName, true, "[DRY RUN]", cancellationToken).ConfigureAwait(false);
                    }
                    continue;
                }

                var snipeAsset = existing[0];
                var updates = _resolver.GetUpdatesToApply(snipeAsset, device);
                var discrepancies = _resolver.GetDiscrepancies(snipeAsset, device);
                foreach (var (field, snipeVal, mdmVal) in discrepancies)
                    await LogAsync(runId, LogLevel.Warning, SourceSystem.Application, "skip", device.SerialNumber, device.DeviceName, true, $"Discrepancy {field}: Snipe-IT={snipeVal} MDM={mdmVal}", cancellationToken).ConfigureAwait(false);

                if (updates.Count == 0)
                {
                    summary.Skipped++;
                    continue;
                }

                if (!dryRun && snipeAsset.SnipeItAssetId != null)
                {
                    var ok = await _snipeItService.UpdateAssetAsync(snipeAsset.SnipeItAssetId.Value, updates, cancellationToken).ConfigureAwait(false);
                    if (ok)
                    {
                        summary.Updated++;
                        await LogAsync(runId, LogLevel.Info, SourceSystem.SnipeIt, "update", device.SerialNumber, device.DeviceName, true, null, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        summary.Errors++;
                        await LogAsync(runId, LogLevel.Error, SourceSystem.SnipeIt, "error", device.SerialNumber, device.DeviceName, false, "Update failed", cancellationToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    summary.Updated++;
                    await LogAsync(runId, LogLevel.Info, SourceSystem.Application, "update", device.SerialNumber, device.DeviceName, true, dryRun ? "[DRY RUN]" : null, cancellationToken).ConfigureAwait(false);
                }
            }

            summary.CompletedAtUtc = DateTimeOffset.UtcNow;
            await LogAsync(runId, LogLevel.Info, SourceSystem.Application, "sync_complete", null, null, true, null, cancellationToken).ConfigureAwait(false);
            await _webhookService.SendSyncNotificationAsync(summary, cancellationToken).ConfigureAwait(false);

            var retention = TimeSpan.FromDays(30);
            await _logRepository.PurgeOlderThanAsync(retention, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync failed");
            summary.CompletedAtUtc = DateTimeOffset.UtcNow;
            summary.Errors++;
            await LogAsync(runId, LogLevel.Error, SourceSystem.Application, "sync_complete", null, null, false, ex.Message, cancellationToken).ConfigureAwait(false);
            await _webhookService.SendSyncNotificationAsync(summary, cancellationToken).ConfigureAwait(false);
        }

        return summary;
    }

    private async Task LogAsync(string runId, LogLevel level, SourceSystem source, string action, string? serial, string? deviceName, bool success, string? errorDetail, CancellationToken cancellationToken)
    {
        await _logRepository.AppendAsync(new LogEntry
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Level = level,
            SourceSystem = source,
            Action = action,
            SerialNumber = serial,
            DeviceName = deviceName,
            Success = success,
            ErrorDetail = errorDetail,
            SyncRunId = runId
        }, cancellationToken).ConfigureAwait(false);
    }
}
