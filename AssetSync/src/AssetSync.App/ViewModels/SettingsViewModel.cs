using CommunityToolkit.Mvvm.ComponentModel;

namespace AssetSync.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _snipeItUrl = "";

    [ObservableProperty]
    private string _intuneTenantId = "";
}
