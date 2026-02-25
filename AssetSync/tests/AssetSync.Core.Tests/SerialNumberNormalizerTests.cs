using AssetSync.Core.Services;
using Xunit;

namespace AssetSync.Core.Tests;

public class SerialNumberNormalizerTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("  ", "")]
    [InlineData("ABC123", "ABC123")]
    [InlineData("abc123", "ABC123")]
    [InlineData("AB-C-1-2-3", "ABC123")]
    [InlineData("  AB C 123  ", "ABC123")]
    public void Normalize_ReturnsExpected(string? input, string expected)
    {
        var result = SerialNumberNormalizer.Normalize(input);
        Assert.Equal(expected, result);
    }
}
