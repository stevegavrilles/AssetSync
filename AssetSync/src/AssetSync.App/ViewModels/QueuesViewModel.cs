using CommunityToolkit.Mvvm.ComponentModel;

namespace AssetSync.App.ViewModels;

public partial class QueuesViewModel : ObservableObject
{
    [ObservableProperty]
    private string _infoText = "Pending models, unmatched users, duplicate serials.";
}
