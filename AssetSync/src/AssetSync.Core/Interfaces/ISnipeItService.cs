using AssetSync.Core.Models;

namespace AssetSync.Core.Interfaces;

public interface ISnipeItService
{
    Task<Device?> GetAssetBySerialAsync(string normalizedSerial, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Device>> SearchAssetsBySerialAsync(string searchTerm, CancellationToken cancellationToken = default);
    Task<Device?> CreateAssetAsync(Device device, CancellationToken cancellationToken = default);
    Task<bool> UpdateAssetAsync(int assetId, IReadOnlyDictionary<string, object?> updates, CancellationToken cancellationToken = default);
}
