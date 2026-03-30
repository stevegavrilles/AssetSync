using AssetSync.Core;
using AssetSync.Core.Interfaces;
using AssetSync.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AssetSync.App.ViewModels;

public partial class SetupWizardViewModel : ObservableObject
{
    private readonly IConfigRepository _config;
    private readonly ICredentialStore _credentials;
    private readonly IConnectivityTester _connectivity;
    private readonly ISyncEngine _syncEngine;

    [ObservableProperty] private int _currentStepIndex;
    [ObservableProperty] private string _stepTitle = "Welcome";
    [ObservableProperty] private bool _canGoBack;
    [ObservableProperty] private bool _canGoNext = true;
    [ObservableProperty] private bool _isLastStep;
    [ObservableProperty] private string _nextButtonText = "Next";

    // Snipe-IT
    [ObservableProperty] private string _snipeItUrl = "";
    [ObservableProperty] private string _snipeItApiKey = "";
    [ObservableProperty] private string _snipeItTestResult = "";

    // Intune
    [ObservableProperty] private string _intuneTenantId = "";
    [ObservableProperty] private string _intuneClientId = "";
    [ObservableProperty] private string _intuneClientSecret = "";
    [ObservableProperty] private string _intuneTestResult = "";

    // Iru
    [ObservableProperty] private string _iruBaseUrl = "";
    [ObservableProperty] private string _iruApiToken = "";
    [ObservableProperty] private string _iruTestResult = "";

    // Dry run
    [ObservableProperty] private string _dryRunResult = "";
    [ObservableProperty] private bool _isDryRunning;

    private const int TotalSteps = 6;

    public event Action? RequestClose;

    public SetupWizardViewModel(IConfigRepository config, ICredentialStore credentials, IConnectivityTester connectivity, ISyncEngine syncEngine)
    {
        _config = config;
        _credentials = credentials;
        _connectivity = connectivity;
        _syncEngine = syncEngine;
    }

    [RelayCommand]
    private async Task GoNextAsync()
    {
        // Save current step data
        await SaveCurrentStepAsync();

        if (CurrentStepIndex >= TotalSteps - 1)
        {
            // Finish
            await _config.SetAsync(ConfigKeys.SetupComplete, "true");
            RequestClose?.Invoke();
            return;
        }

        CurrentStepIndex++;
        UpdateStepState();
    }

    [RelayCommand]
    private void GoBack()
    {
        if (CurrentStepIndex <= 0) return;
        CurrentStepIndex--;
        UpdateStepState();
    }

    private void UpdateStepState()
    {
        CanGoBack = CurrentStepIndex > 0;
        IsLastStep = CurrentStepIndex >= TotalSteps - 1;
        NextButtonText = IsLastStep ? "Finish" : "Next";

        StepTitle = CurrentStepIndex switch
        {
            0 => "Welcome",
            1 => "Snipe-IT Connection",
            2 => "Intune Connection",
            3 => "Iru Connection",
            4 => "Initial Sync (Dry Run)",
            5 => "Setup Complete",
            _ => ""
        };
    }

    private async Task SaveCurrentStepAsync()
    {
        switch (CurrentStepIndex)
        {
            case 1:
                await _config.SetAsync(ConfigKeys.SnipeItUrl, SnipeItUrl);
                await _credentials.SetAsync(CredentialKeys.SnipeItApiKey, SnipeItApiKey);
                break;
            case 2:
                await _config.SetAsync(ConfigKeys.IntuneTenantId, IntuneTenantId);
                await _config.SetAsync(ConfigKeys.IntuneClientId, IntuneClientId);
                await _credentials.SetAsync(CredentialKeys.IntuneClientSecret, IntuneClientSecret);
                break;
            case 3:
                await _config.SetAsync(ConfigKeys.IruBaseUrl, IruBaseUrl);
                await _credentials.SetAsync(CredentialKeys.IruApiToken, IruApiToken);
                break;
        }
    }

    [RelayCommand]
    private async Task TestSnipeItAsync()
    {
        await SaveCurrentStepAsync();
        SnipeItTestResult = "Testing...";
        var result = await _connectivity.TestSnipeItAsync();
        SnipeItTestResult = $"{result.State} - {result.Message}";
    }

    [RelayCommand]
    private async Task TestIntuneAsync()
    {
        await SaveCurrentStepAsync();
        IntuneTestResult = "Testing...";
        var result = await _connectivity.TestIntuneAsync();
        IntuneTestResult = $"{result.State} - {result.Message}";
    }

    [RelayCommand]
    private async Task TestIruAsync()
    {
        await SaveCurrentStepAsync();
        IruTestResult = "Testing...";
        var result = await _connectivity.TestIruAsync();
        IruTestResult = $"{result.State} - {result.Message}";
    }

    [RelayCommand]
    private async Task RunDryRunAsync(CancellationToken ct)
    {
        IsDryRunning = true;
        DryRunResult = "Running dry run...";
        try
        {
            var summary = await _syncEngine.RunSyncAsync(true, ct);
            DryRunResult = $"Dry run complete. Created: {summary.Created}, Updated: {summary.Updated}, Skipped: {summary.Skipped}, Errors: {summary.Errors}";
        }
        catch (Exception ex)
        {
            DryRunResult = $"Dry run failed: {ex.Message}";
        }
        finally
        {
            IsDryRunning = false;
        }
    }
}
