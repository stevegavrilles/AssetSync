using AssetSync.Core.Enums;

namespace AssetSync.Core.Models;

public class ConnectionStatus
{
    public SourceSystem Service { get; set; }
    public ConnectionState State { get; set; }
    public string? Message { get; set; }
    public long? ResponseTimeMs { get; set; }
}
