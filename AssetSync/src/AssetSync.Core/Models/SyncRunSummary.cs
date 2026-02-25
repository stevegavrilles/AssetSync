namespace AssetSync.Core.Models;

public class SyncRunSummary
{
    public string SyncRunId { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
    public bool DryRun { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
    public bool SnipeItReachable { get; set; }
    public bool IntuneReachable { get; set; }
    public bool IruReachable { get; set; }
}
