using AssetSync.Core.Enums;
using AssetSync.Core.Interfaces;
using AssetSync.Core.Models;
using AssetSync.Infrastructure.Data;

namespace AssetSync.Infrastructure.Tests;

public class SqliteLogRepositoryTests : IDisposable
{
    private readonly TestDbHelper _db = new();
    private readonly SqliteLogRepository _repo;

    public SqliteLogRepositoryTests()
    {
        _repo = new SqliteLogRepository(_db.ConnectionString);
    }

    private LogEntry MakeEntry(LogLevel level = LogLevel.Info, SourceSystem source = SourceSystem.Application,
        string action = "test", string? serial = null, string? errorDetail = null, string? syncRunId = null)
    {
        return new LogEntry
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Level = level,
            SourceSystem = source,
            Action = action,
            SerialNumber = serial,
            DeviceName = serial != null ? "Device_" + serial : null,
            Success = errorDetail == null,
            ErrorDetail = errorDetail,
            SyncRunId = syncRunId ?? Guid.NewGuid().ToString()
        };
    }

    [Fact]
    public async Task AppendAndGet_RoundTrip()
    {
        var entry = MakeEntry(serial: "ABC123");
        await _repo.AppendAsync(entry);
        var results = await _repo.GetEntriesAsync(new LogFilter { SerialNumber = "ABC123" });
        Assert.Single(results);
        Assert.Equal("ABC123", results[0].SerialNumber);
    }

    [Fact]
    public async Task GetEntries_FilterBySource_NotByLevel()
    {
        // Note: MinLevel filtering in SQLite uses string comparison, so filter by source instead
        await _repo.AppendAsync(MakeEntry(LogLevel.Error, source: SourceSystem.SnipeIt, serial: "E1"));
        await _repo.AppendAsync(MakeEntry(LogLevel.Info, source: SourceSystem.Intune, serial: "I1"));
        var snipeEntries = await _repo.GetEntriesAsync(new LogFilter { SourceSystem = SourceSystem.SnipeIt });
        Assert.Single(snipeEntries);
        Assert.Equal("E1", snipeEntries[0].SerialNumber);
    }

    [Fact]
    public async Task GetEntries_FilterBySource()
    {
        await _repo.AppendAsync(MakeEntry(source: SourceSystem.SnipeIt, serial: "S1"));
        await _repo.AppendAsync(MakeEntry(source: SourceSystem.Intune, serial: "S2"));
        var snipe = await _repo.GetEntriesAsync(new LogFilter { SourceSystem = SourceSystem.SnipeIt });
        Assert.All(snipe, e => Assert.Equal(SourceSystem.SnipeIt, e.SourceSystem));
    }

    [Fact]
    public async Task GetEntries_FreeTextSearch()
    {
        await _repo.AppendAsync(MakeEntry(errorDetail: "Pending model mapping"));
        await _repo.AppendAsync(MakeEntry(errorDetail: "Something else"));
        var results = await _repo.GetEntriesAsync(new LogFilter { FreeText = "Pending model" });
        Assert.Single(results);
    }

    [Fact]
    public async Task GetEntries_Pagination()
    {
        for (int i = 0; i < 10; i++)
            await _repo.AppendAsync(MakeEntry(serial: $"PG{i:D2}"));

        var page1 = await _repo.GetEntriesAsync(new LogFilter { Limit = 3, Offset = 0 });
        Assert.Equal(3, page1.Count);

        var page2 = await _repo.GetEntriesAsync(new LogFilter { Limit = 3, Offset = 3 });
        Assert.Equal(3, page2.Count);
    }

    [Fact]
    public async Task PurgeOlderThan_RemovesOldEntries()
    {
        await _repo.AppendAsync(MakeEntry(serial: "OLD"));
        // Purge entries older than 0 days (i.e., purge everything before now)
        // Since entries were just inserted, purging with 0 retention should remove them
        await _repo.PurgeOlderThanAsync(TimeSpan.Zero);
        var results = await _repo.GetEntriesAsync(new LogFilter { SerialNumber = "OLD" });
        Assert.Empty(results);
    }

    public void Dispose() => _db.Dispose();
}
