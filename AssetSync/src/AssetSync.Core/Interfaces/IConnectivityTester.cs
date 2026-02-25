using AssetSync.Core.Models;

namespace AssetSync.Core.Interfaces;

public interface IConnectivityTester
{
    Task<ConnectionStatus> TestSnipeItAsync(CancellationToken cancellationToken = default);
    Task<ConnectionStatus> TestIntuneAsync(CancellationToken cancellationToken = default);
    Task<ConnectionStatus> TestIruAsync(CancellationToken cancellationToken = default);
}
