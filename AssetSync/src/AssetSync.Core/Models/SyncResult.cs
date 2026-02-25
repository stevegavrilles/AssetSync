using AssetSync.Core.Enums;

namespace AssetSync.Core.Models;

public class SyncResult
{
    public string NormalizedSerial { get; set; } = string.Empty;
    public SyncAction Action { get; set; }
    public bool Success { get; set; }
    public string? ErrorDetail { get; set; }
    public string? DeviceName { get; set; }
}
