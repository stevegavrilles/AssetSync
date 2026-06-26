namespace AssetSync.Core.Models;

public enum LicenseGroupRunStatus
{
    Ok,
    Error,
    Halted,
    Skipped
}

/// <summary>Outcome of reconciling a single group↔license mapping.</summary>
public class LicenseGroupMappingResult
{
    public int MappingId { get; set; }
    public string GroupName { get; set; } = "";
    public LicenseGroupRunStatus Status { get; set; }
    public int Assigned { get; set; }
    public int CheckedIn { get; set; }
    public int PendingNew { get; set; }
    public int NoMatch { get; set; }
    public int NoFreeSeat { get; set; }
    public string? Message { get; set; }

    /// <summary>Lowercase status string persisted to last_run_status / shown in the UI.</summary>
    public string StatusText => Status.ToString().ToLowerInvariant();
}

/// <summary>Aggregate outcome of a license-group sync run across all mappings.</summary>
public class LicenseGroupSyncSummary
{
    public string RunId { get; set; } = Guid.NewGuid().ToString();
    public bool DryRun { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
    public List<LicenseGroupMappingResult> Mappings { get; } = new();
}
