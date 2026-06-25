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
    public void GetUpdatesToApply_NameDiffers_MdmWins()
    {
        // MDM (Intune/Iru) is source of truth for device names: any difference is pushed to Snipe-IT.
        var resolver = new ConflictResolver();
        var snipe = new Device { SerialNumber = "SN1", DeviceName = "SnipePC" };
        var mdm = new Device { SerialNumber = "SN1", DeviceName = "MDMPC" };
        var updates = resolver.GetUpdatesToApply(snipe, mdm);
        Assert.True(updates.ContainsKey("name"));
        Assert.Equal("MDMPC", updates["name"]);
    }

    [Fact]
    public void GetUpdatesToApply_NameMatches_NoUpdate()
    {
        // No redundant write-back when names already agree.
        var resolver = new ConflictResolver();
        var snipe = new Device { SerialNumber = "SN1", DeviceName = "SamePC" };
        var mdm = new Device { SerialNumber = "SN1", DeviceName = "SamePC" };
        var updates = resolver.GetUpdatesToApply(snipe, mdm);
        Assert.False(updates.ContainsKey("name"));
    }

    [Fact]
    public void GetUpdatesToApply_MdmNameEmpty_NoOverwrite()
    {
        // MDM only wins when it actually has a name — never blank out an existing Snipe-IT name.
        var resolver = new ConflictResolver();
        var snipe = new Device { SerialNumber = "SN1", DeviceName = "SnipePC" };
        var mdm = new Device { SerialNumber = "SN1", DeviceName = null };
        var updates = resolver.GetUpdatesToApply(snipe, mdm);
        Assert.False(updates.ContainsKey("name"));
    }
}
