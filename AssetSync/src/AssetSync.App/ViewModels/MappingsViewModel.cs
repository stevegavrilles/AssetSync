using System.Collections.ObjectModel;
using AssetSync.Core.Interfaces;
using AssetSync.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AssetSync.App.ViewModels;

public partial class MappingsViewModel : ObservableObject
{
    private readonly IMappingRepository _repo;
    private readonly ISnipeItService _snipeIt;
    private readonly ILogRepository _logRepository;

    // Snipe-IT lookup lists for dropdowns
    public ObservableCollection<SnipeItLookup> SnipeItModels { get; } = new();
    public ObservableCollection<SnipeItLookup> SnipeItCategories { get; } = new();
    public ObservableCollection<SnipeItLookup> SnipeItUsers { get; } = new();

    // --- Pending Mappings Grid ---
    public ObservableCollection<PendingMappingRow> PendingMappings { get; } = new();

    // Model Mappings
    public ObservableCollection<ModelMapping> ModelMappings { get; } = new();
    [ObservableProperty] private ModelMapping? _selectedModelMapping;
    [ObservableProperty] private string _editMdmModelString = "";
    [ObservableProperty] private SnipeItLookup? _selectedSnipeItModel;
    [ObservableProperty] private int _editSnipeItModelId;

    // User Mappings
    public ObservableCollection<UserMapping> UserMappings { get; } = new();
    [ObservableProperty] private UserMapping? _selectedUserMapping;
    [ObservableProperty] private string _editMdmUserIdentifier = "";
    [ObservableProperty] private SnipeItLookup? _selectedSnipeItUser;
    [ObservableProperty] private int _editSnipeItUserId;

    // Build Mappings
    public ObservableCollection<BuildMapping> BuildMappings { get; } = new();
    [ObservableProperty] private BuildMapping? _selectedBuildMapping;
    [ObservableProperty] private string _editBuildNumber = "";
    [ObservableProperty] private string _editFriendlyName = "";

    // Category Mappings
    public ObservableCollection<CategoryMapping> CategoryMappings { get; } = new();
    [ObservableProperty] private CategoryMapping? _selectedCategoryMapping;
    [ObservableProperty] private string _editMdmDeviceType = "";
    [ObservableProperty] private SnipeItLookup? _selectedSnipeItCategory;
    [ObservableProperty] private int _editSnipeItCategoryId;

    [ObservableProperty] private string _statusMessage = "Click 'Fetch from Snipe-IT' to load models, then 'Discover Unmapped' to find devices needing mapping.";
    [ObservableProperty] private bool _isLoading;

    public MappingsViewModel(IMappingRepository repo, ISnipeItService snipeIt, ILogRepository logRepository)
    {
        _repo = repo;
        _snipeIt = snipeIt;
        _logRepository = logRepository;
        _ = LoadAllAsync();
    }

    // When a Snipe-IT dropdown selection changes, update the ID field
    partial void OnSelectedSnipeItModelChanged(SnipeItLookup? value)
    {
        if (value != null) EditSnipeItModelId = value.Id;
    }

    partial void OnSelectedSnipeItUserChanged(SnipeItLookup? value)
    {
        if (value != null) EditSnipeItUserId = value.Id;
    }

    partial void OnSelectedSnipeItCategoryChanged(SnipeItLookup? value)
    {
        if (value != null) EditSnipeItCategoryId = value.Id;
    }

    [RelayCommand]
    private async Task LoadAllAsync()
    {
        await LoadModelMappingsAsync();
        await LoadUserMappingsAsync();
        await LoadBuildMappingsAsync();
        await LoadCategoryMappingsAsync();
    }

    [RelayCommand]
    private async Task FetchSnipeItLookupsAsync()
    {
        IsLoading = true;
        StatusMessage = "Fetching from Snipe-IT...";
        try
        {
            var models = await _snipeIt.GetModelsAsync();
            SnipeItModels.Clear();
            foreach (var m in models) SnipeItModels.Add(m);

            var categories = await _snipeIt.GetCategoriesAsync();
            SnipeItCategories.Clear();
            foreach (var c in categories) SnipeItCategories.Add(c);

            var users = await _snipeIt.GetUsersAsync();
            SnipeItUsers.Clear();
            foreach (var u in users) SnipeItUsers.Add(u);

            StatusMessage = $"Loaded {models.Count} models, {categories.Count} categories, {users.Count} users from Snipe-IT.";

            // Auto-load pending mappings grid now that we have Snipe-IT data
            await LoadPendingMappingsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fetch failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // --- Pending Mappings Grid ---

    [RelayCommand]
    private async Task LoadPendingMappingsAsync()
    {
        IsLoading = true;
        StatusMessage = "Discovering devices needing model mappings...";
        try
        {
            var pending = await _logRepository.GetEntriesAsync(new LogFilter
            {
                FreeText = "Pending model mapping",
                Limit = 2000
            });

            var existingMappings = await _repo.GetModelMappingsAsync();
            var mappedModels = new HashSet<string>(
                existingMappings.Select(m => m.MdmModelString),
                StringComparer.OrdinalIgnoreCase);

            // Group by MDM model string only — never fall back to device name
            var grouped = new Dictionary<string, (int Count, List<string> Serials)>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in pending)
            {
                // Only process entries that contain an actual model string
                var detail = entry.ErrorDetail ?? "";
                if (!detail.StartsWith("Pending model mapping:")) continue;

                var model = detail["Pending model mapping:".Length..].Trim();
                if (string.IsNullOrEmpty(model) || mappedModels.Contains(model)) continue;

                if (!grouped.TryGetValue(model, out var grp))
                    grouped[model] = (1, new List<string> { entry.SerialNumber ?? "" });
                else if (!grp.Serials.Contains(entry.SerialNumber ?? ""))
                    grouped[model] = (grp.Count + 1, grp.Serials);
            }

            PendingMappings.Clear();
            foreach (var (model, (count, serials)) in grouped.OrderBy(kvp => kvp.Key))
            {
                PendingMappings.Add(new PendingMappingRow
                {
                    MdmModel = model,
                    DeviceCount = count,
                    ExampleSerials = string.Join(", ", serials.Take(3)),
                    StatusText = "Pending"
                });
            }

            if (PendingMappings.Count == 0)
                StatusMessage = "No unmapped models found. Run a sync first to discover devices, or all models are already mapped.";
            else
                StatusMessage = $"Found {PendingMappings.Count} model(s) needing mapping. Select a Snipe-IT model for each row, then click 'Save All Mappings'.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Discovery failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveAllPendingAsync()
    {
        var toSave = PendingMappings.Where(r => r.SelectedSnipeItModel != null && !r.IsSaved).ToList();
        if (toSave.Count == 0)
        {
            StatusMessage = "No rows have a Snipe-IT model selected. Select a model in the dropdown for each row first.";
            return;
        }

        IsLoading = true;
        StatusMessage = $"Saving {toSave.Count} mapping(s)...";
        try
        {
            foreach (var row in toSave)
            {
                var mapping = new ModelMapping
                {
                    MdmModelString = row.MdmModel,
                    SnipeItModelId = row.SelectedSnipeItModel!.Id
                };
                await _repo.SaveModelMappingAsync(mapping);
                row.IsSaved = true;
                row.StatusText = $"✓ {row.SelectedSnipeItModel!.Name}";
            }

            await LoadModelMappingsAsync();

            // Remove saved rows from pending list
            var saved = PendingMappings.Where(r => r.IsSaved).ToList();
            foreach (var r in saved) PendingMappings.Remove(r);

            StatusMessage = $"Saved {toSave.Count} model mapping(s). {PendingMappings.Count} remaining.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // --- Model Mappings (CRUD) ---

    partial void OnSelectedModelMappingChanged(ModelMapping? value)
    {
        if (value == null) return;
        EditMdmModelString = value.MdmModelString;
        EditSnipeItModelId = value.SnipeItModelId;
        SelectedSnipeItModel = SnipeItModels.FirstOrDefault(m => m.Id == value.SnipeItModelId);
    }

    private async Task LoadModelMappingsAsync()
    {
        ModelMappings.Clear();
        foreach (var m in await _repo.GetModelMappingsAsync()) ModelMappings.Add(m);
    }

    [RelayCommand]
    private async Task SaveModelMappingAsync()
    {
        var mapping = SelectedModelMapping != null
            ? new ModelMapping { Id = SelectedModelMapping.Id, MdmModelString = EditMdmModelString, SnipeItModelId = EditSnipeItModelId }
            : new ModelMapping { MdmModelString = EditMdmModelString, SnipeItModelId = EditSnipeItModelId };
        await _repo.SaveModelMappingAsync(mapping);
        await LoadModelMappingsAsync();
        StatusMessage = "Model mapping saved.";
    }

    [RelayCommand]
    private async Task DeleteModelMappingAsync()
    {
        if (SelectedModelMapping == null) return;
        await _repo.DeleteModelMappingAsync(SelectedModelMapping.Id);
        await LoadModelMappingsAsync();
        ClearModelEdit();
        StatusMessage = "Model mapping deleted.";
    }

    [RelayCommand]
    private void NewModelMapping() => ClearModelEdit();

    private void ClearModelEdit()
    {
        SelectedModelMapping = null;
        EditMdmModelString = "";
        EditSnipeItModelId = 0;
        SelectedSnipeItModel = null;
    }

    // --- User Mappings ---

    partial void OnSelectedUserMappingChanged(UserMapping? value)
    {
        if (value == null) return;
        EditMdmUserIdentifier = value.MdmUserIdentifier;
        EditSnipeItUserId = value.SnipeItUserId;
        SelectedSnipeItUser = SnipeItUsers.FirstOrDefault(u => u.Id == value.SnipeItUserId);
    }

    private async Task LoadUserMappingsAsync()
    {
        UserMappings.Clear();
        foreach (var m in await _repo.GetUserMappingsAsync()) UserMappings.Add(m);
    }

    [RelayCommand]
    private async Task SaveUserMappingAsync()
    {
        var mapping = SelectedUserMapping != null
            ? new UserMapping { Id = SelectedUserMapping.Id, MdmUserIdentifier = EditMdmUserIdentifier, SnipeItUserId = EditSnipeItUserId }
            : new UserMapping { MdmUserIdentifier = EditMdmUserIdentifier, SnipeItUserId = EditSnipeItUserId };
        await _repo.SaveUserMappingAsync(mapping);
        await LoadUserMappingsAsync();
        StatusMessage = "User mapping saved.";
    }

    [RelayCommand]
    private async Task DeleteUserMappingAsync()
    {
        if (SelectedUserMapping == null) return;
        await _repo.DeleteUserMappingAsync(SelectedUserMapping.Id);
        await LoadUserMappingsAsync();
        ClearUserEdit();
        StatusMessage = "User mapping deleted.";
    }

    [RelayCommand]
    private void NewUserMapping() => ClearUserEdit();

    private void ClearUserEdit()
    {
        SelectedUserMapping = null;
        EditMdmUserIdentifier = "";
        EditSnipeItUserId = 0;
        SelectedSnipeItUser = null;
    }

    // --- Build Mappings ---

    partial void OnSelectedBuildMappingChanged(BuildMapping? value)
    {
        if (value == null) return;
        EditBuildNumber = value.BuildNumber;
        EditFriendlyName = value.FriendlyName;
    }

    private async Task LoadBuildMappingsAsync()
    {
        BuildMappings.Clear();
        foreach (var m in await _repo.GetBuildMappingsAsync()) BuildMappings.Add(m);
    }

    [RelayCommand]
    private async Task SaveBuildMappingAsync()
    {
        var mapping = SelectedBuildMapping != null
            ? new BuildMapping { Id = SelectedBuildMapping.Id, BuildNumber = EditBuildNumber, FriendlyName = EditFriendlyName }
            : new BuildMapping { BuildNumber = EditBuildNumber, FriendlyName = EditFriendlyName };
        await _repo.SaveBuildMappingAsync(mapping);
        await LoadBuildMappingsAsync();
        StatusMessage = "Build mapping saved.";
    }

    [RelayCommand]
    private async Task DeleteBuildMappingAsync()
    {
        if (SelectedBuildMapping == null) return;
        await _repo.DeleteBuildMappingAsync(SelectedBuildMapping.Id);
        await LoadBuildMappingsAsync();
        ClearBuildEdit();
        StatusMessage = "Build mapping deleted.";
    }

    [RelayCommand]
    private void NewBuildMapping() => ClearBuildEdit();

    private void ClearBuildEdit()
    {
        SelectedBuildMapping = null;
        EditBuildNumber = "";
        EditFriendlyName = "";
    }

    // --- Category Mappings ---

    partial void OnSelectedCategoryMappingChanged(CategoryMapping? value)
    {
        if (value == null) return;
        EditMdmDeviceType = value.MdmDeviceType;
        EditSnipeItCategoryId = value.SnipeItCategoryId;
        SelectedSnipeItCategory = SnipeItCategories.FirstOrDefault(c => c.Id == value.SnipeItCategoryId);
    }

    private async Task LoadCategoryMappingsAsync()
    {
        CategoryMappings.Clear();
        foreach (var m in await _repo.GetCategoryMappingsAsync()) CategoryMappings.Add(m);
    }

    [RelayCommand]
    private async Task SaveCategoryMappingAsync()
    {
        var mapping = SelectedCategoryMapping != null
            ? new CategoryMapping { Id = SelectedCategoryMapping.Id, MdmDeviceType = EditMdmDeviceType, SnipeItCategoryId = EditSnipeItCategoryId }
            : new CategoryMapping { MdmDeviceType = EditMdmDeviceType, SnipeItCategoryId = EditSnipeItCategoryId };
        await _repo.SaveCategoryMappingAsync(mapping);
        await LoadCategoryMappingsAsync();
        StatusMessage = "Category mapping saved.";
    }

    [RelayCommand]
    private async Task DeleteCategoryMappingAsync()
    {
        if (SelectedCategoryMapping == null) return;
        await _repo.DeleteCategoryMappingAsync(SelectedCategoryMapping.Id);
        await LoadCategoryMappingsAsync();
        ClearCategoryEdit();
        StatusMessage = "Category mapping deleted.";
    }

    [RelayCommand]
    private void NewCategoryMapping() => ClearCategoryEdit();

    private void ClearCategoryEdit()
    {
        SelectedCategoryMapping = null;
        EditMdmDeviceType = "";
        EditSnipeItCategoryId = 0;
        SelectedSnipeItCategory = null;
    }
}
