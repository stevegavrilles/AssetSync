using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using AssetSync.Infrastructure.Data;
using AssetSync.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AssetSync.Infrastructure.Tests;

public class DpapiEntropyTests
{
    private sealed class TempEnv : IDisposable
    {
        public string Root { get; }
        public string KeysDir { get; }
        public string DbPath { get; }
        public string ConnectionString { get; }

        public TempEnv()
        {
            Root = Path.Combine(Path.GetTempPath(), "AssetSyncEntropyTest_" + Guid.NewGuid().ToString("N"));
            KeysDir = Path.Combine(Root, "keys");
            DbPath = Path.Combine(Root, "assetsync.db");
            Directory.CreateDirectory(Root);
            ConnectionString = $"Data Source={DbPath}";
            new DatabaseInitializer(ConnectionString).Initialize();
        }

        public void InsertRawCredential(string key, byte[] blob)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO credentials (key, encrypted_value) VALUES ($k, $v)";
            cmd.Parameters.AddWithValue("$k", key);
            cmd.Parameters.AddWithValue("$v", blob);
            cmd.ExecuteNonQuery();
        }

        public byte[] ReadRawCredential(string key)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT encrypted_value FROM credentials WHERE key = $k";
            cmd.Parameters.AddWithValue("$k", key);
            return (byte[])cmd.ExecuteScalar()!;
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(Root, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task ProtectUnprotect_RoundTrips_AndAppliesEntropy()
    {
        if (!OperatingSystem.IsWindows()) return;
        await RoundTripOnWindows();
    }

    [SupportedOSPlatform("windows")]
    private static async Task RoundTripOnWindows()
    {
        using var env = new TempEnv();
        var entropy = new FileDpapiEntropyProvider(env.KeysDir);
        var store = new DpapiCredentialStore(env.ConnectionString, entropy);

        await store.SetAsync("snipeit_api_key", "s3cr3t-value");
        Assert.Equal("s3cr3t-value", await store.GetAsync("snipeit_api_key"));

        // The stored blob must actually depend on the entropy: it should NOT decrypt without it,
        // and SHOULD decrypt with it.
        var blob = env.ReadRawCredential("snipeit_api_key");
        Assert.ThrowsAny<CryptographicException>(() => ProtectedData.Unprotect(blob, null, DataProtectionScope.LocalMachine));
        Assert.Equal("s3cr3t-value", Encoding.UTF8.GetString(ProtectedData.Unprotect(blob, entropy.GetEntropy(), DataProtectionScope.LocalMachine)));
    }

    [Fact]
    public async Task LegacyNoEntropyValue_DecryptsViaFallback_ThenUpgradesInPlace()
    {
        if (!OperatingSystem.IsWindows()) return;
        await MigrationOnWindows();
    }

    [SupportedOSPlatform("windows")]
    private static async Task MigrationOnWindows()
    {
        using var env = new TempEnv();
        var entropy = new FileDpapiEntropyProvider(env.KeysDir);
        var store = new DpapiCredentialStore(env.ConnectionString, entropy);

        // Simulate a credential saved by an older build: LocalMachine scope, NO entropy.
        var legacyBlob = ProtectedData.Protect(Encoding.UTF8.GetBytes("legacy-secret"), null, DataProtectionScope.LocalMachine);
        env.InsertRawCredential("intune_client_secret", legacyBlob);

        // Reading it must succeed (backward-compatible fallback) and return the original value.
        Assert.Equal("legacy-secret", await store.GetAsync("intune_client_secret"));

        // ...and it must have been transparently re-protected WITH entropy (upgraded in place).
        var upgraded = env.ReadRawCredential("intune_client_secret");
        Assert.NotEqual(legacyBlob, upgraded);
        Assert.ThrowsAny<CryptographicException>(() => ProtectedData.Unprotect(upgraded, null, DataProtectionScope.LocalMachine));
        Assert.Equal("legacy-secret", Encoding.UTF8.GetString(ProtectedData.Unprotect(upgraded, entropy.GetEntropy(), DataProtectionScope.LocalMachine)));

        // A subsequent read still works (now via the current path).
        Assert.Equal("legacy-secret", await store.GetAsync("intune_client_secret"));
    }

    [Fact]
    public void Entropy_IsPersisted_AndReusedAcrossInstances()
    {
        if (!OperatingSystem.IsWindows()) return;
        PersistenceOnWindows();
    }

    [SupportedOSPlatform("windows")]
    private static void PersistenceOnWindows()
    {
        using var env = new TempEnv();

        var provider1 = new FileDpapiEntropyProvider(env.KeysDir);
        var e1 = provider1.GetEntropy();
        Assert.Equal(32, e1.Length);
        Assert.Equal(e1, provider1.GetEntropy()); // cached / stable within an instance

        // A fresh provider over the same directory reuses the persisted value (does not regenerate).
        var provider2 = new FileDpapiEntropyProvider(env.KeysDir);
        Assert.Equal(e1, provider2.GetEntropy());

        // The file exists and stores the entropy wrapped (DPAPI), not in the clear.
        var file = Path.Combine(env.KeysDir, "entropy.bin");
        Assert.True(File.Exists(file));
        Assert.NotEqual(e1, File.ReadAllBytes(file));
    }
}
