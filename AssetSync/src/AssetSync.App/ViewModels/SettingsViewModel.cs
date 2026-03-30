using System.Collections.ObjectModel;
using AssetSync.Core;
using AssetSync.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AssetSync.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IConfigRepository _config;
    private readonly ICredentialStore _credentials;
    private readonly IConnectivityTester _connectivity;
    private readonly IWebhookService _webhookService;

    // Connections - Snipe-IT
    [ObservableProperty] private string _snipeItUrl = "";
    [ObservableProperty] private string _snipeItApiKey = "";
    [ObservableProperty] private string _snipeItConnectionStatus = "";

    // Connections - Intune
    [ObservableProperty] private string _intuneTenantId = "";
    [ObservableProperty] private string _intuneClientId = "";
    [ObservableProperty] private string _intuneClientSecret = "";
    [ObservableProperty] private string _intuneConnectionStatus = "";

    // Connections - Iru
    [ObservableProperty] private string _iruBaseUrl = "";
    [ObservableProperty] private string _iruApiToken = "";
    [ObservableProperty] private string _iruConnectionStatus = "";

    // Sync
    [ObservableProperty] private int _syncIntervalHours = 1;
    [ObservableProperty] private bool _dryRunDefault;

    // Write-Back
    [ObservableProperty] private bool _writeBackIntuneEnabled;
    [ObservableProperty] private bool _writeBackIruEnabled;

    // Notifications
    [ObservableProperty] private string _webhookUrl = "";
    [ObservableProperty] private string _webhookType = "Generic";
    [ObservableProperty] private string _webhookTestStatus = "";

    // Application
    [ObservableProperty] private int _logRetentionDays = 30;

    // State
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "";

    public ObservableCollection<string> WebhookTypes { get; } = new() { "Generic", "Teams", "Slack" };

    public SettingsViewModel(IConfigRepository config, ICredentialStore credentials, IConnectivityTester connectivity, IWebhookService webhookService)
    {
        _config = config;
        _credentials = credentials;
        _connectivity = connectivity;
        _webhookService = webhookService;
        _ = LoadSettingsAsync();
    }

    [RelayCommand]
    private async Task LoadSettingsAsync()
    {
        SnipeItUrl = await _config.GetAsync(ConfigKeys.SnipeItUrl) ?? "";
        SnipeItApiKey = await _credentials.GetAsync(CredentialKeys.SnipeItApiKey) ?? "";
        IntuneTenantId = await _config.GetAsync(ConfigKeys.IntuneTenantId) ?? "";
        IntuneClientId = await _config.GetAsync(ConfigKeys.IntuneClientId) ?? "";
        IntuneClientSecret = await _credentials.GetAsync(CredentialKeys.IntuneClientSecret) ?? "";
        IruBaseUrl = await _config.GetAsync(ConfigKeys.IruBaseUrl) ?? "";
        IruApiToken = await _credentials.GetAsync(CredentialKeys.IruApiToken) ?? "";
        SyncIntervalHours = await _config.GetSyncIntervalHoursAsync();
        DryRunDefault = await _config.GetDryRunDefaultAsync();
        WriteBackIntuneEnabled = await _config.GetWriteBackIntuneEnabledAsync();
        WriteBackIruEnabled = await _config.GetWriteBackIruEnabledAsync();
        WebhookUrl = await _config.GetAsync(ConfigKeys.WebhookUrl) ?? "";
        WebhookType = await _config.GetAsync(ConfigKeys.WebhookType) ?? "Generic";
        var retention = await _config.GetAsync(ConfigKeys.LogRetentionDays);
        LogRetentionDays = int.TryParse(retention, out var d) ? d : 30;
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        IsBusy = true;
        StatusMessage = "";
        try
        {
            await _config.SetAsync(ConfigKeys.SnipeItUrl, SnipeItUrl);
            await _credentials.SetAsync(CredentialKeys.SnipeItApiKey, SnipeItApiKey);
            await _config.SetAsync(ConfigKeys.IntuneTenantId, IntuneTenantId);
            await _config.SetAsync(ConfigKeys.IntuneClientId, IntuneClientId);
            await _credentials.SetAsync(CredentialKeys.IntuneClientSecret, IntuneClientSecret);
            await _config.SetAsync(ConfigKeys.IruBaseUrl, IruBaseUrl);
            await _credentials.SetAsync(CredentialKeys.IruApiToken, IruApiToken);
            await _config.SetSyncIntervalHoursAsync(SyncIntervalHours);
            await _config.SetDryRunDefaultAsync(DryRunDefault);
            await _config.SetWriteBackIntuneEnabledAsync(WriteBackIntuneEnabled);
            await _config.SetWriteBackIruEnabledAsync(WriteBackIruEnabled);
            await _config.SetAsync(ConfigKeys.WebhookUrl, WebhookUrl);
            await _config.SetAsync(ConfigKeys.WebhookType, WebhookType);
            await _config.SetAsync(ConfigKeys.LogRetentionDays, LogRetentionDays.ToString());
            StatusMessage = "Settings saved.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task TestSnipeItConnectionAsync()
    {
        SnipeItConnectionStatus = "Testing...";
        await SaveSettingsAsync();
        var result = await _connectivity.TestSnipeItAsync();
        SnipeItConnectionStatus = $"{result.State} - {result.Message}" + (result.ResponseTimeMs > 0 ? $" ({result.ResponseTimeMs}ms)" : "");
    }

    [RelayCommand]
    private async Task TestIntuneConnectionAsync()
    {
        IntuneConnectionStatus = "Testing...";
        await SaveSettingsAsync();
        var result = await _connectivity.TestIntuneAsync();
        IntuneConnectionStatus = $"{result.State} - {result.Message}";
    }

    [RelayCommand]
    private async Task TestIruConnectionAsync()
    {
        IruConnectionStatus = "Testing...";
        await SaveSettingsAsync();
        var result = await _connectivity.TestIruAsync();
        IruConnectionStatus = $"{result.State} - {result.Message}" + (result.ResponseTimeMs > 0 ? $" ({result.ResponseTimeMs}ms)" : "");
    }

    [RelayCommand]
    private async Task TestWebhookAsync()
    {
        WebhookTestStatus = "Sending test...";
        await SaveSettingsAsync();
        try
        {
            await _webhookService.TestWebhookAsync();
            WebhookTestStatus = "Test sent successfully.";
        }
        catch (Exception ex)
        {
            WebhookTestStatus = $"Failed: {ex.Message}";
        }
    }
}
