using AssetSync.Core.Models;

namespace AssetSync.Core.Interfaces;

public interface ISyncEngine
{
    Task<SyncRunSummary> RunSyncAsync(bool dryRun, CancellationToken cancellationToken = default);
}
