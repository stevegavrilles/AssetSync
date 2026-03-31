namespace AssetSync.Core.Interfaces;

public interface IConfigRepository
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task SetAsync(string key, string value, CancellationToken cancellationToken = default);
    Task<int> GetSyncIntervalHoursAsync(CancellationToken cancellationToken = default);
    Task SetSyncIntervalHoursAsync(int hours, CancellationToken cancellationToken = default);
    Task<bool> GetDryRunDefaultAsync(CancellationToken cancellationToken = default);
    Task SetDryRunDefaultAsync(bool value, CancellationToken cancellationToken = default);
    Task<bool> GetWriteBackIntuneEnabledAsync(CancellationToken cancellationToken = default);
    Task SetWriteBackIntuneEnabledAsync(bool value, CancellationToken cancellationToken = default);
    Task<bool> GetWriteBackIruEnabledAsync(CancellationToken cancellationToken = default);
    Task SetWriteBackIruEnabledAsync(bool value, CancellationToken cancellationToken = default);
    Task<bool> GetIntuneMdmWinsAsync(CancellationToken cancellationToken = default);
    Task SetIntuneMdmWinsAsync(bool value, CancellationToken cancellationToken = default);
    Task<bool> GetIruMdmWinsAsync(CancellationToken cancellationToken = default);
    Task SetIruMdmWinsAsync(bool value, CancellationToken cancellationToken = default);
}
