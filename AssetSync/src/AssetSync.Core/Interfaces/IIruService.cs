using AssetSync.Core.Models;

namespace AssetSync.Core.Interfaces;

public interface IIruService
{
    Task<IReadOnlyList<Device>> GetDevicesAsync(CancellationToken cancellationToken = default);
    Task<bool> WriteBackAssetTagAsync(string iruDeviceId, string assetTag, CancellationToken cancellationToken = default);
}
