using AssetSync.Core.Models;

namespace AssetSync.Core.Interfaces;

public interface ILogRepository
{
    Task AppendAsync(LogEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LogEntry>> GetEntriesAsync(LogFilter filter, CancellationToken cancellationToken = default);
    Task PurgeOlderThanAsync(TimeSpan retention, CancellationToken cancellationToken = default);
}

public class LogFilter
{
    public DateTimeOffset? FromUtc { get; set; }
    public DateTimeOffset? ToUtc { get; set; }
    public Core.Enums.LogLevel? MinLevel { get; set; }
    public Core.Enums.SourceSystem? SourceSystem { get; set; }
    public string? Action { get; set; }
    public string? SerialNumber { get; set; }
    public string? FreeText { get; set; }
    public string? SyncRunId { get; set; }
    public int? Limit { get; set; }
    public int? Offset { get; set; }
}
