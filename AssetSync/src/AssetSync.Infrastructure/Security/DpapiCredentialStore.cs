using System.Security.Cryptography;
using AssetSync.Core.Interfaces;
using Microsoft.Data.Sqlite;

namespace AssetSync.Infrastructure.Security;

public class DpapiCredentialStore : ICredentialStore
{
    private readonly string _connectionString;

    public DpapiCredentialStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var plainBytes = System.Text.Encoding.UTF8.GetBytes(value);
        var encrypted = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);

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
        var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
        return System.Text.Encoding.UTF8.GetString(decrypted);
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
