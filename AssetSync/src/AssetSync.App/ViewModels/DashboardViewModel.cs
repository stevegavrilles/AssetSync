using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AssetSync.App.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    [ObservableProperty]
    private string _statusText = "Ready.";

    [ObservableProperty]
    private string _lastSyncText = "Last sync: Never";

    [ObservableProperty]
    private string _nextSyncText = "Next sync: —";

    [RelayCommand]
    private async Task SyncNowAsync()
    {
        StatusText = "Sync started…";
        await Task.Delay(500).ConfigureAwait(true);
        StatusText = "Sync completed (configure services to run real sync).";
    }

    [RelayCommand]
    private async Task DryRunAsync()
    {
        StatusText = "Dry run started…";
        await Task.Delay(500).ConfigureAwait(true);
        StatusText = "Dry run completed. No changes written.";
    }
}
