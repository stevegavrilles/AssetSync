using AssetSync.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AssetSync.App.ViewModels;

public partial class PendingMappingRow : ObservableObject
{
    [ObservableProperty] private string _mdmModel = "";
    [ObservableProperty] private int _deviceCount;
    [ObservableProperty] private SnipeItLookup? _selectedSnipeItModel;
    [ObservableProperty] private bool _isSaved;
    [ObservableProperty] private string _statusText = "Pending";
}
