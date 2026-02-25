using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AssetSync.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private object? _currentView;

    public MainViewModel()
    {
        CurrentView = new DashboardViewModel();
    }

    [RelayCommand]
    private void NavigateDashboard() => CurrentView = new DashboardViewModel();

    [RelayCommand]
    private void NavigateSettings() => CurrentView = new SettingsViewModel();

    [RelayCommand]
    private void NavigateMappings() => CurrentView = new MappingsViewModel();

    [RelayCommand]
    private void NavigateLogs() => CurrentView = new LogsViewModel();

    [RelayCommand]
    private void NavigateQueues() => CurrentView = new QueuesViewModel();
}
