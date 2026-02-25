using AssetSync.Core.Models;

namespace AssetSync.Core.Interfaces;

public interface IIntuneService
{
    Task<IReadOnlyList<Device>> GetManagedDevicesAsync(CancellationToken cancellationToken = default);
}
