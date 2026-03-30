using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace AssetSync.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _services;

    [ObservableProperty]
    private object? _currentView;

    public MainViewModel(IServiceProvider services)
    {
        _services = services;
        CurrentView = _services.GetRequiredService<DashboardViewModel>();
    }

    [RelayCommand]
    private void NavigateDashboard() => CurrentView = _services.GetRequiredService<DashboardViewModel>();

    [RelayCommand]
    private void NavigateSettings() => CurrentView = _services.GetRequiredService<SettingsViewModel>();

    [RelayCommand]
    private void NavigateMappings() => CurrentView = _services.GetRequiredService<MappingsViewModel>();

    [RelayCommand]
    private void NavigateLogs() => CurrentView = _services.GetRequiredService<LogsViewModel>();

    [RelayCommand]
    private void NavigateQueues() => CurrentView = _services.GetRequiredService<QueuesViewModel>();
}
