using AssetSync.Core.Models;

namespace AssetSync.Core.Interfaces;

public interface IMappingRepository
{
    Task<IReadOnlyList<ModelMapping>> GetModelMappingsAsync(CancellationToken cancellationToken = default);
    Task<ModelMapping?> GetModelMappingAsync(string mdmModelString, CancellationToken cancellationToken = default);
    Task SaveModelMappingAsync(ModelMapping mapping, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserMapping>> GetUserMappingsAsync(CancellationToken cancellationToken = default);
    Task<UserMapping?> GetUserMappingAsync(string mdmUserIdentifier, CancellationToken cancellationToken = default);
    Task SaveUserMappingAsync(UserMapping mapping, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BuildMapping>> GetBuildMappingsAsync(CancellationToken cancellationToken = default);
    Task<BuildMapping?> GetBuildMappingAsync(string buildNumber, CancellationToken cancellationToken = default);
    Task SaveBuildMappingAsync(BuildMapping mapping, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategoryMapping>> GetCategoryMappingsAsync(CancellationToken cancellationToken = default);
    Task<CategoryMapping?> GetCategoryMappingAsync(string mdmDeviceType, CancellationToken cancellationToken = default);
    Task SaveCategoryMappingAsync(CategoryMapping mapping, CancellationToken cancellationToken = default);
}
