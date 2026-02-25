using CommunityToolkit.Mvvm.ComponentModel;

namespace AssetSync.App.ViewModels;

public partial class LogsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _infoText = "Log viewer with filtering and export.";
}
