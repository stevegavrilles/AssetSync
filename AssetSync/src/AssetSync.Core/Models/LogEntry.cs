using AssetSync.Core.Enums;

namespace AssetSync.Core.Models;

public class LogEntry
{
    public long Id { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }
    public LogLevel Level { get; set; }
    public SourceSystem SourceSystem { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string? DeviceName { get; set; }
    public string? FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public bool Success { get; set; }
    public string? ErrorDetail { get; set; }
    public string SyncRunId { get; set; } = string.Empty;
}
