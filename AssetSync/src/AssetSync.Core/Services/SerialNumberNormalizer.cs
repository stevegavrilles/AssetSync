using System.Text.RegularExpressions;

namespace AssetSync.Core.Services;

public static class SerialNumberNormalizer
{
    public static string Normalize(string? serial)
    {
        if (string.IsNullOrWhiteSpace(serial)) return string.Empty;
        return Regex.Replace(serial.Trim().ToUpperInvariant(), @"[^A-Z0-9]", "");
    }
}
