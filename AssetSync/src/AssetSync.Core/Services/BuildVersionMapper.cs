using AssetSync.Core.Interfaces;
using AssetSync.Core.Models;

namespace AssetSync.Core.Services;

/// <summary>
/// Maps Windows build number (e.g. from osVersion 10.0.22631.4890) to friendly name.
/// </summary>
public class BuildVersionMapper
{
    private readonly IMappingRepository _mappingRepository;

    public BuildVersionMapper(IMappingRepository mappingRepository)
    {
        _mappingRepository = mappingRepository;
    }

    /// <summary>
    /// Extract build number from osVersion (e.g. 10.0.22631.4890 -> 22631) and resolve friendly name.
    /// </summary>
    public async Task<string?> GetFriendlyNameAsync(string? osVersion, CancellationToken cancellationToken = default)
    {
        var build = ExtractBuildNumber(osVersion);
        if (string.IsNullOrEmpty(build)) return null;
        var mapping = await _mappingRepository.GetBuildMappingAsync(build, cancellationToken).ConfigureAwait(false);
        return mapping?.FriendlyName ?? osVersion;
    }

    public static string? ExtractBuildNumber(string? osVersion)
    {
        if (string.IsNullOrWhiteSpace(osVersion)) return null;
        var parts = osVersion.Split('.');
        if (parts.Length >= 3 && int.TryParse(parts[2], out _))
            return parts[2];
        return null;
    }
}
