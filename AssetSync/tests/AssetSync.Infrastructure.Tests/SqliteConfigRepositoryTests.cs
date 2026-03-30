using AssetSync.Infrastructure.Data;

namespace AssetSync.Infrastructure.Tests;

public class SqliteConfigRepositoryTests : IDisposable
{
    private readonly TestDbHelper _db = new();
    private readonly SqliteConfigRepository _repo;

    public SqliteConfigRepositoryTests()
    {
        _repo = new SqliteConfigRepository(_db.ConnectionString);
    }

    [Fact]
    public async Task GetSet_RoundTrip()
    {
        await _repo.SetAsync("test_key", "test_value");
        var result = await _repo.GetAsync("test_key");
        Assert.Equal("test_value", result);
    }

    [Fact]
    public async Task Get_ReturnNullForMissing()
    {
        var result = await _repo.GetAsync("nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public async Task SyncIntervalHours_DefaultsToOne()
    {
        var result = await _repo.GetSyncIntervalHoursAsync();
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task SyncIntervalHours_SetAndGet()
    {
        await _repo.SetSyncIntervalHoursAsync(4);
        var result = await _repo.GetSyncIntervalHoursAsync();
        Assert.Equal(4, result);
    }

    [Fact]
    public async Task DryRunDefault_RoundTrip()
    {
        await _repo.SetDryRunDefaultAsync(true);
        Assert.True(await _repo.GetDryRunDefaultAsync());
        await _repo.SetDryRunDefaultAsync(false);
        Assert.False(await _repo.GetDryRunDefaultAsync());
    }

    [Fact]
    public async Task WriteBackIntune_RoundTrip()
    {
        await _repo.SetWriteBackIntuneEnabledAsync(true);
        Assert.True(await _repo.GetWriteBackIntuneEnabledAsync());
    }

    [Fact]
    public async Task WriteBackIru_RoundTrip()
    {
        await _repo.SetWriteBackIruEnabledAsync(true);
        Assert.True(await _repo.GetWriteBackIruEnabledAsync());
    }

    public void Dispose() => _db.Dispose();
}
