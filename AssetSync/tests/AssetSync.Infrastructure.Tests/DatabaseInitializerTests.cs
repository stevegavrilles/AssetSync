using AssetSync.Infrastructure.Data;
using Microsoft.Data.Sqlite;

namespace AssetSync.Infrastructure.Tests;

public class DatabaseInitializerTests : IDisposable
{
    private readonly TestDbHelper _db = new();

    [Fact]
    public void Initialize_CreatesAllTables()
    {
        using var conn = new SqliteConnection(_db.ConnectionString);
        conn.Open();
        var tables = new List<string>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) tables.Add(reader.GetString(0));

        Assert.Contains("config", tables);
        Assert.Contains("logs", tables);
        Assert.Contains("model_mappings", tables);
        Assert.Contains("user_mappings", tables);
        Assert.Contains("build_mappings", tables);
        Assert.Contains("category_mappings", tables);
        Assert.Contains("credentials", tables);
    }

    [Fact]
    public void Initialize_SeedsBuildMappings()
    {
        using var conn = new SqliteConnection(_db.ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM build_mappings";
        var count = (long)cmd.ExecuteScalar()!;
        Assert.True(count >= 5, $"Expected at least 5 seeded build mappings, got {count}");
    }

    [Fact]
    public void Initialize_IsIdempotent()
    {
        var init = new DatabaseInitializer(_db.ConnectionString);
        init.Initialize(); // second call should not throw
    }

    public void Dispose() => _db.Dispose();
}
