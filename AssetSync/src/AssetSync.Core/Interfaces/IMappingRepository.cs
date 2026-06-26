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
    Task DeleteModelMappingAsync(int id, CancellationToken cancellationToken = default);
    Task DeleteUserMappingAsync(int id, CancellationToken cancellationToken = default);
    Task DeleteBuildMappingAsync(int id, CancellationToken cancellationToken = default);
    Task DeleteCategoryMappingAsync(int id, CancellationToken cancellationToken = default);

    // Model ignores — suppress noisy models (VMs, cloud instances, etc.)
    Task<IReadOnlyList<string>> GetIgnoredModelsAsync(CancellationToken cancellationToken = default);
    Task<bool> IsModelIgnoredAsync(string mdmModelString, CancellationToken cancellationToken = default);
    Task AddModelIgnoreAsync(string mdmModelString, CancellationToken cancellationToken = default);
    Task RemoveModelIgnoreAsync(string mdmModelString, CancellationToken cancellationToken = default);

    // Group <-> license mappings (Entra group correlated to a Snipe-IT license)
    Task<IReadOnlyList<GroupLicenseMapping>> GetGroupLicenseMappingsAsync(CancellationToken cancellationToken = default);
    Task SaveGroupLicenseMappingAsync(GroupLicenseMapping mapping, CancellationToken cancellationToken = default);
    Task DeleteGroupLicenseMappingAsync(int id, CancellationToken cancellationToken = default);
    Task UpdateGroupLicenseRunStatusAsync(int id, string status, string? error, CancellationToken cancellationToken = default);

    // Grace-period / soft-delete state for license-seat removals
    Task<IReadOnlyList<PendingRemoval>> GetPendingRemovalsAsync(int mappingId, CancellationToken cancellationToken = default);
    Task UpsertPendingRemovalAsync(int mappingId, string subjectKey, CancellationToken cancellationToken = default);
    Task ClearPendingRemovalAsync(int mappingId, string subjectKey, CancellationToken cancellationToken = default);
}
