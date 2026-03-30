using System.Collections.ObjectModel;
using AssetSync.Core.Interfaces;
using AssetSync.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AssetSync.App.ViewModels;

public partial class MappingsViewModel : ObservableObject
{
    private readonly IMappingRepository _repo;

    // Model Mappings
    public ObservableCollection<ModelMapping> ModelMappings { get; } = new();
    [ObservableProperty] private ModelMapping? _selectedModelMapping;
    [ObservableProperty] private string _editMdmModelString = "";
    [ObservableProperty] private int _editSnipeItModelId;

    // User Mappings
    public ObservableCollection<UserMapping> UserMappings { get; } = new();
    [ObservableProperty] private UserMapping? _selectedUserMapping;
    [ObservableProperty] private string _editMdmUserIdentifier = "";
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
    [ObservableProperty] private int _editSnipeItCategoryId;

    [ObservableProperty] private string _statusMessage = "";

    public MappingsViewModel(IMappingRepository repo)
    {
        _repo = repo;
        _ = LoadAllAsync();
    }

    [RelayCommand]
    private async Task LoadAllAsync()
    {
        await LoadModelMappingsAsync();
        await LoadUserMappingsAsync();
        await LoadBuildMappingsAsync();
        await LoadCategoryMappingsAsync();
    }

    // --- Model Mappings ---

    partial void OnSelectedModelMappingChanged(ModelMapping? value)
    {
        if (value == null) return;
        EditMdmModelString = value.MdmModelString;
        EditSnipeItModelId = value.SnipeItModelId;
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
    }

    // --- User Mappings ---

    partial void OnSelectedUserMappingChanged(UserMapping? value)
    {
        if (value == null) return;
        EditMdmUserIdentifier = value.MdmUserIdentifier;
        EditSnipeItUserId = value.SnipeItUserId;
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
    }
}
