using System.Security.Cryptography;
using AssetSync.Core.Interfaces;
using Microsoft.Data.Sqlite;

namespace AssetSync.Infrastructure.Security;

public class DpapiCredentialStore : ICredentialStore
{
    private readonly string _connectionString;
    private readonly IDpapiEntropyProvider _entropyProvider;

    public DpapiCredentialStore(string connectionString, IDpapiEntropyProvider entropyProvider)
    {
        _connectionString = connectionString;
        _entropyProvider = entropyProvider;
    }

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var plainBytes = System.Text.Encoding.UTF8.GetBytes(value);
        // LocalMachine scope allows both the interactive user and SYSTEM (service) to decrypt.
        // Per-install entropy is mixed in so the DB blob alone is insufficient to decrypt.
        var encrypted = ProtectedData.Protect(plainBytes, _entropyProvider.GetEntropy(), DataProtectionScope.LocalMachine);

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO credentials (key, encrypted_value) VALUES ($k, $v)";
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", encrypted);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT encrypted_value FROM credentials WHERE key = $k";
        cmd.Parameters.AddWithValue("$k", key);
        var obj = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (obj is not byte[] encrypted) return null;

        // 1. Current format: LocalMachine scope + per-install entropy.
        try
        {
            var decrypted = ProtectedData.Unprotect(encrypted, _entropyProvider.GetEntropy(), DataProtectionScope.LocalMachine);
            return System.Text.Encoding.UTF8.GetString(decrypted);
        }
        catch (CryptographicException) { /* not the current format — try legacy below */ }

        // 2. Legacy: LocalMachine scope, NO entropy. Decrypt, then upgrade in place (re-protect with entropy).
        try
        {
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.LocalMachine);
            var value = System.Text.Encoding.UTF8.GetString(decrypted);
            await SetAsync(key, value, cancellationToken).ConfigureAwait(false);
            return value;
        }
        catch (CryptographicException) { /* try the oldest format below */ }

        // 3. Oldest legacy: CurrentUser scope, no entropy. Decrypt, then upgrade in place.
        try
        {
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            var value = System.Text.Encoding.UTF8.GetString(decrypted);
            await SetAsync(key, value, cancellationToken).ConfigureAwait(false);
            return value;
        }
        catch
        {
            return null;
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM credentials WHERE key = $k";
        cmd.Parameters.AddWithValue("$k", key);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM credentials WHERE key = $k LIMIT 1";
        cmd.Parameters.AddWithValue("$k", key);
        var obj = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return obj != null;
    }
}
