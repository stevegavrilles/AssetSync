using AssetSync.Core.Models;

namespace AssetSync.Core.Interfaces;

public interface IIntuneService
{
    Task<IReadOnlyList<Device>> GetManagedDevicesAsync(CancellationToken cancellationToken = default);
    Task<bool> WriteBackAssetTagAsync(string azureAdDeviceId, string assetTag, string? existingNotes, CancellationToken cancellationToken = default);
}
