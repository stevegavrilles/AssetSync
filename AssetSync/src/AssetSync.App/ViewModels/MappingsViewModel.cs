using CommunityToolkit.Mvvm.ComponentModel;

namespace AssetSync.App.ViewModels;

public partial class MappingsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _infoText = "Model, user, build, and category mappings.";
}
