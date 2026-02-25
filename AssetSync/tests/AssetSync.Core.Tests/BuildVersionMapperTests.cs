using AssetSync.Core.Models;
using AssetSync.Core.Interfaces;
using AssetSync.Core.Services;
using Moq;
using Xunit;

namespace AssetSync.Core.Tests;

public class BuildVersionMapperTests
{
    [Fact]
    public void ExtractBuildNumber_ValidVersion_ReturnsBuild()
    {
        var build = BuildVersionMapper.ExtractBuildNumber("10.0.22631.4890");
        Assert.Equal("22631", build);
    }

    [Fact]
    public void ExtractBuildNumber_Null_ReturnsNull()
    {
        var build = BuildVersionMapper.ExtractBuildNumber(null);
        Assert.Null(build);
    }

    [Fact]
    public async Task GetFriendlyNameAsync_KnownBuild_ReturnsMappedName()
    {
        var repo = new Mock<IMappingRepository>();
        repo.Setup(r => r.GetBuildMappingAsync("22631", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BuildMapping { BuildNumber = "22631", FriendlyName = "Windows 11 23H2" });
        var mapper = new BuildVersionMapper(repo.Object);
        var name = await mapper.GetFriendlyNameAsync("10.0.22631.4890", default);
        Assert.Equal("Windows 11 23H2", name);
    }
}
