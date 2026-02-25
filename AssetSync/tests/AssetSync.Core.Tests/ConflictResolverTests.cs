using AssetSync.Core.Models;
using AssetSync.Core.Services;
using Xunit;

namespace AssetSync.Core.Tests;

public class ConflictResolverTests
{
    [Fact]
    public void GetUpdatesToApply_EmptySnipeIt_FillsFromMdm()
    {
        var resolver = new ConflictResolver();
        var snipe = new Device { SerialNumber = "SN1", DeviceName = null, SnipeItModelId = null };
        var mdm = new Device { SerialNumber = "SN1", DeviceName = "PC1", SnipeItModelId = 5 };
        var updates = resolver.GetUpdatesToApply(snipe, mdm);
        Assert.True(updates.ContainsKey("name"));
        Assert.Equal("PC1", updates["name"]);
        Assert.True(updates.ContainsKey("model_id"));
        Assert.Equal(5, updates["model_id"]);
    }

    [Fact]
    public void GetUpdatesToApply_SnipeItHasValue_NoOverwrite()
    {
        var resolver = new ConflictResolver();
        var snipe = new Device { SerialNumber = "SN1", DeviceName = "SnipePC" };
        var mdm = new Device { SerialNumber = "SN1", DeviceName = "MDMPC" };
        var updates = resolver.GetUpdatesToApply(snipe, mdm);
        Assert.False(updates.ContainsKey("name"));
    }
}
