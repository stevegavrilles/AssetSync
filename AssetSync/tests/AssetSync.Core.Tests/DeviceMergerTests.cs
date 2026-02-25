using AssetSync.Core.Models;
using AssetSync.Core.Services;
using Xunit;

namespace AssetSync.Core.Tests;

public class DeviceMergerTests
{
    [Fact]
    public void Merge_EmptyLists_ReturnsEmpty()
    {
        var merger = new DeviceMerger();
        var result = merger.Merge(Array.Empty<Device>(), Array.Empty<Device>());
        Assert.Empty(result);
    }

    [Fact]
    public void Merge_SingleIntuneDevice_ReturnsOne()
    {
        var merger = new DeviceMerger();
        var intune = new List<Device>
        {
            new Device { SerialNumber = "SN1", NormalizedSerial = "SN1", DeviceName = "PC1", PlatformSource = "Intune" }
        };
        var result = merger.Merge(intune, Array.Empty<Device>());
        Assert.Single(result);
        Assert.Equal("SN1", result[0].NormalizedSerial);
        Assert.Equal("PC1", result[0].DeviceName);
    }

    [Fact]
    public void Merge_SameSerialFromBoth_MergesOne()
    {
        var merger = new DeviceMerger();
        var intune = new List<Device> { new Device { SerialNumber = "SN1", NormalizedSerial = "SN1", DeviceName = "PC1", PlatformSource = "Intune" } };
        var iru = new List<Device> { new Device { SerialNumber = "SN1", NormalizedSerial = "SN1", DeviceName = "Mac1", PlatformSource = "Iru", OperatingSystem = "macOS" } };
        var result = merger.Merge(intune, iru);
        Assert.Single(result);
    }
}
