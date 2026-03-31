using AssetSync.Core.Interfaces;
using Microsoft.Data.Sqlite;

namespace AssetSync.Infrastructure.Data;

public class SqliteConfigRepository : IConfigRepository
{
    private const string SyncIntervalKey = "sync_interval_hours";
    private const string DryRunDefaultKey = "dry_run_default";
    private const string WriteBackIntuneKey = "write_back_intune";
    private const string WriteBackIruKey = "write_back_iru";
    private const string IntuneMdmWinsKey = "sync_intune_mdm_wins";
    private const string IruMdmWinsKey = "sync_iru_mdm_wins";

    private readonly string _connectionString;

    public SqliteConfigRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM config WHERE key = $k";
        cmd.Parameters.AddWithValue("$k", key);
        var obj = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return obj?.ToString();
    }

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO config (key, value) VALUES ($k, $v)";
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> GetSyncIntervalHoursAsync(CancellationToken cancellationToken = default)
    {
        var v = await GetAsync(SyncIntervalKey, cancellationToken).ConfigureAwait(false);
        return int.TryParse(v, out var h) && h >= 1 && h <= 72 ? h : 1;
    }

    public async Task SetSyncIntervalHoursAsync(int hours, CancellationToken cancellationToken = default)
    {
        await SetAsync(SyncIntervalKey, hours.ToString(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> GetDryRunDefaultAsync(CancellationToken cancellationToken = default)
    {
        var v = await GetAsync(DryRunDefaultKey, cancellationToken).ConfigureAwait(false);
        return v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task SetDryRunDefaultAsync(bool value, CancellationToken cancellationToken = default)
    {
        await SetAsync(DryRunDefaultKey, value ? "1" : "0", cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> GetWriteBackIntuneEnabledAsync(CancellationToken cancellationToken = default)
    {
        var v = await GetAsync(WriteBackIntuneKey, cancellationToken).ConfigureAwait(false);
        return v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task SetWriteBackIntuneEnabledAsync(bool value, CancellationToken cancellationToken = default)
    {
        await SetAsync(WriteBackIntuneKey, value ? "1" : "0", cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> GetWriteBackIruEnabledAsync(CancellationToken cancellationToken = default)
    {
        var v = await GetAsync(WriteBackIruKey, cancellationToken).ConfigureAwait(false);
        return v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task SetWriteBackIruEnabledAsync(bool value, CancellationToken cancellationToken = default)
    {
        await SetAsync(WriteBackIruKey, value ? "1" : "0", cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> GetIntuneMdmWinsAsync(CancellationToken cancellationToken = default)
    {
        var v = await GetAsync(IntuneMdmWinsKey, cancellationToken).ConfigureAwait(false);
        return v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task SetIntuneMdmWinsAsync(bool value, CancellationToken cancellationToken = default)
    {
        await SetAsync(IntuneMdmWinsKey, value ? "1" : "0", cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> GetIruMdmWinsAsync(CancellationToken cancellationToken = default)
    {
        var v = await GetAsync(IruMdmWinsKey, cancellationToken).ConfigureAwait(false);
        return v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task SetIruMdmWinsAsync(bool value, CancellationToken cancellationToken = default)
    {
        await SetAsync(IruMdmWinsKey, value ? "1" : "0", cancellationToken).ConfigureAwait(false);
    }
}
