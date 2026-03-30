using AssetSync.Infrastructure.Data;

namespace AssetSync.Infrastructure.Tests;

public sealed class TestDbHelper : IDisposable
{
    public string DbPath { get; }
    public string ConnectionString { get; }

    public TestDbHelper()
    {
        DbPath = Path.Combine(Path.GetTempPath(), $"assetsync_test_{Guid.NewGuid():N}.db");
        ConnectionString = $"Data Source={DbPath}";
        var initializer = new DatabaseInitializer(ConnectionString);
        initializer.Initialize();
    }

    public void Dispose()
    {
        try { File.Delete(DbPath); } catch { }
    }
}
