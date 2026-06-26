using AssetSync.Core.Models;
using AssetSync.Infrastructure.Data;

namespace AssetSync.Infrastructure.Tests;

public class SqliteMappingRepositoryTests : IDisposable
{
    private readonly TestDbHelper _db = new();
    private readonly SqliteMappingRepository _repo;

    public SqliteMappingRepositoryTests()
    {
        _repo = new SqliteMappingRepository(_db.ConnectionString);
    }

    // Model Mappings

    [Fact]
    public async Task ModelMapping_SaveAndGetAll()
    {
        await _repo.SaveModelMappingAsync(new ModelMapping { MdmModelString = "Surface Pro 9", SnipeItModelId = 42 });
        var all = await _repo.GetModelMappingsAsync();
        Assert.Contains(all, m => m.MdmModelString == "Surface Pro 9" && m.SnipeItModelId == 42);
    }

    [Fact]
    public async Task ModelMapping_GetByKey()
    {
        await _repo.SaveModelMappingAsync(new ModelMapping { MdmModelString = "MacBook Air", SnipeItModelId = 10 });
        var result = await _repo.GetModelMappingAsync("MacBook Air");
        Assert.NotNull(result);
        Assert.Equal(10, result.SnipeItModelId);
    }

    [Fact]
    public async Task ModelMapping_Delete()
    {
        await _repo.SaveModelMappingAsync(new ModelMapping { MdmModelString = "ToDelete", SnipeItModelId = 1 });
        var all = await _repo.GetModelMappingsAsync();
        var item = all.First(m => m.MdmModelString == "ToDelete");
        await _repo.DeleteModelMappingAsync(item.Id);
        var afterDelete = await _repo.GetModelMappingsAsync();
        Assert.DoesNotContain(afterDelete, m => m.MdmModelString == "ToDelete");
    }

    // User Mappings

    [Fact]
    public async Task UserMapping_SaveAndGetAll()
    {
        await _repo.SaveUserMappingAsync(new UserMapping { MdmUserIdentifier = "user@example.com", SnipeItUserId = 5 });
        var all = await _repo.GetUserMappingsAsync();
        Assert.Contains(all, m => m.MdmUserIdentifier == "user@example.com");
    }

    [Fact]
    public async Task UserMapping_Delete()
    {
        await _repo.SaveUserMappingAsync(new UserMapping { MdmUserIdentifier = "del@test.com", SnipeItUserId = 2 });
        var all = await _repo.GetUserMappingsAsync();
        var item = all.First(m => m.MdmUserIdentifier == "del@test.com");
        await _repo.DeleteUserMappingAsync(item.Id);
        Assert.DoesNotContain(await _repo.GetUserMappingsAsync(), m => m.MdmUserIdentifier == "del@test.com");
    }

    // Build Mappings (seeded data exists)

    [Fact]
    public async Task BuildMapping_SeededDataExists()
    {
        var all = await _repo.GetBuildMappingsAsync();
        Assert.True(all.Count >= 5);
    }

    [Fact]
    public async Task BuildMapping_SaveAndGet()
    {
        await _repo.SaveBuildMappingAsync(new BuildMapping { BuildNumber = "99999", FriendlyName = "Test Build" });
        var result = await _repo.GetBuildMappingAsync("99999");
        Assert.NotNull(result);
        Assert.Equal("Test Build", result.FriendlyName);
    }

    [Fact]
    public async Task BuildMapping_Delete()
    {
        await _repo.SaveBuildMappingAsync(new BuildMapping { BuildNumber = "88888", FriendlyName = "Del" });
        var all = await _repo.GetBuildMappingsAsync();
        var item = all.First(m => m.BuildNumber == "88888");
        await _repo.DeleteBuildMappingAsync(item.Id);
        Assert.Null(await _repo.GetBuildMappingAsync("88888"));
    }

    // Category Mappings

    [Fact]
    public async Task CategoryMapping_SaveAndGetAll()
    {
        await _repo.SaveCategoryMappingAsync(new CategoryMapping { MdmDeviceType = "Laptop", SnipeItCategoryId = 3 });
        var all = await _repo.GetCategoryMappingsAsync();
        Assert.Contains(all, m => m.MdmDeviceType == "Laptop");
    }

    [Fact]
    public async Task CategoryMapping_Delete()
    {
        await _repo.SaveCategoryMappingAsync(new CategoryMapping { MdmDeviceType = "Tablet", SnipeItCategoryId = 7 });
        var all = await _repo.GetCategoryMappingsAsync();
        var item = all.First(m => m.MdmDeviceType == "Tablet");
        await _repo.DeleteCategoryMappingAsync(item.Id);
        Assert.DoesNotContain(await _repo.GetCategoryMappingsAsync(), m => m.MdmDeviceType == "Tablet");
    }

    // Group <-> License Mappings (read_only direction)

    [Fact]
    public async Task GroupLicenseMapping_SavedNewMapping_DefaultsToReadOnly()
    {
        await _repo.SaveGroupLicenseMappingAsync(new GroupLicenseMapping
        {
            EntraGroupId = "g-1", EntraGroupName = "Acrobat Users", SnipeItLicenseId = 10
        });

        var saved = (await _repo.GetGroupLicenseMappingsAsync()).Single(m => m.EntraGroupId == "g-1");
        Assert.True(saved.ReadOnly); // default ON
        Assert.Equal("Acrobat Users", saved.EntraGroupName);
        Assert.Equal(10, saved.SnipeItLicenseId);
    }

    [Fact]
    public async Task GroupLicenseMapping_ToggleReadOnly_PersistsBothWays()
    {
        await _repo.SaveGroupLicenseMappingAsync(new GroupLicenseMapping
        {
            EntraGroupId = "g-2", EntraGroupName = "Photoshop Users", SnipeItLicenseId = 20
        });
        var saved = (await _repo.GetGroupLicenseMappingsAsync()).Single(m => m.EntraGroupId == "g-2");
        Assert.True(saved.ReadOnly);

        // Toggle to write mode (read_only = false) and persist.
        saved.ReadOnly = false;
        await _repo.SaveGroupLicenseMappingAsync(saved);

        var afterWrite = (await _repo.GetGroupLicenseMappingsAsync()).Single(m => m.EntraGroupId == "g-2");
        Assert.Equal(saved.Id, afterWrite.Id);
        Assert.False(afterWrite.ReadOnly); // persisted to the read_only column

        // Toggle back to read-only and persist.
        afterWrite.ReadOnly = true;
        await _repo.SaveGroupLicenseMappingAsync(afterWrite);
        Assert.True((await _repo.GetGroupLicenseMappingsAsync()).Single(m => m.EntraGroupId == "g-2").ReadOnly);
    }

    [Fact]
    public async Task GroupLicenseMapping_Delete()
    {
        await _repo.SaveGroupLicenseMappingAsync(new GroupLicenseMapping
        {
            EntraGroupId = "g-3", EntraGroupName = "ToRemove", SnipeItLicenseId = 30
        });
        var item = (await _repo.GetGroupLicenseMappingsAsync()).Single(m => m.EntraGroupId == "g-3");
        await _repo.DeleteGroupLicenseMappingAsync(item.Id);
        Assert.DoesNotContain(await _repo.GetGroupLicenseMappingsAsync(), m => m.EntraGroupId == "g-3");
    }

    public void Dispose() => _db.Dispose();
}
