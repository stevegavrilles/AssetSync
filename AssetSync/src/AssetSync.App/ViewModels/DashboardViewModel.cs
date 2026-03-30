using AssetSync.Core.Enums;
using AssetSync.Core.Interfaces;
using AssetSync.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AssetSync.App.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly ISyncEngine _syncEngine;
    private readonly IConnectivityTester _connectivity;
    private readonly IConfigRepository _config;
    private readonly ILogRepository _logRepository;

    // Connection status
    [ObservableProperty] private string _snipeItStatus = "Unknown";
    [ObservableProperty] private string _intuneStatus = "Unknown";
    [ObservableProperty] private string _iruStatus = "Unknown";
    [ObservableProperty] private string _snipeItStatusColor = "Gray";
    [ObservableProperty] private string _intuneStatusColor = "Gray";
    [ObservableProperty] private string _iruStatusColor = "Gray";

    // Sync state
    [ObservableProperty] private bool _isSyncing;
    [ObservableProperty] private string _statusText = "Ready.";
    [ObservableProperty] private string _lastSyncText = "Last sync: Never";
    [ObservableProperty] private string _nextSyncText = "Next sync: --";

    // Summary stats
    [ObservableProperty] private int _lastCreated;
    [ObservableProperty] private int _lastUpdated;
    [ObservableProperty] private int _lastSkipped;
    [ObservableProperty] private int _lastErrors;
    [ObservableProperty] private bool _lastDryRun;
    [ObservableProperty] private bool _hasSyncResult;

    public DashboardViewModel(ISyncEngine syncEngine, IConnectivityTester connectivity, IConfigRepository config, ILogRepository logRepository)
    {
        _syncEngine = syncEngine;
        _connectivity = connectivity;
        _config = config;
        _logRepository = logRepository;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await RefreshConnectionStatusAsync();
        await LoadLastSyncInfoAsync();
    }

    private bool CanSync() => !IsSyncing;

    [RelayCommand(CanExecute = nameof(CanSync))]
    private async Task SyncNowAsync(CancellationToken ct)
    {
        await RunSyncAsync(false, ct);
    }

    [RelayCommand(CanExecute = nameof(CanSync))]
    private async Task DryRunAsync(CancellationToken ct)
    {
        await RunSyncAsync(true, ct);
    }

    private async Task RunSyncAsync(bool dryRun, CancellationToken ct)
    {
        IsSyncing = true;
        SyncNowCommand.NotifyCanExecuteChanged();
        DryRunCommand.NotifyCanExecuteChanged();
        StatusText = dryRun ? "Dry run in progress..." : "Sync in progress...";

        try
        {
            var summary = await _syncEngine.RunSyncAsync(dryRun, ct);
            LastCreated = summary.Created;
            LastUpdated = summary.Updated;
            LastSkipped = summary.Skipped;
            LastErrors = summary.Errors;
            LastDryRun = summary.DryRun;
            HasSyncResult = true;
            LastSyncText = $"Last sync: {summary.CompletedAtUtc.LocalDateTime:g}" + (summary.DryRun ? " (dry run)" : "");
            StatusText = summary.Errors > 0
                ? $"Completed with {summary.Errors} error(s)."
                : "Sync completed successfully.";
            await ComputeNextSync();
            await RefreshConnectionStatusAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Sync failed: {ex.Message}";
        }
        finally
        {
            IsSyncing = false;
            SyncNowCommand.NotifyCanExecuteChanged();
            DryRunCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private async Task RefreshConnectionStatusAsync()
    {
        var tasks = new[]
        {
            _connectivity.TestSnipeItAsync(),
            _connectivity.TestIntuneAsync(),
            _connectivity.TestIruAsync()
        };
        var results = await Task.WhenAll(tasks);

        SnipeItStatus = FormatStatus(results[0]);
        SnipeItStatusColor = StatusColor(results[0].State);
        IntuneStatus = FormatStatus(results[1]);
        IntuneStatusColor = StatusColor(results[1].State);
        IruStatus = FormatStatus(results[2]);
        IruStatusColor = StatusColor(results[2].State);
    }

    private async Task LoadLastSyncInfoAsync()
    {
        var entries = await _logRepository.GetEntriesAsync(new LogFilter
        {
            Action = "sync_complete",
            Limit = 1
        });
        if (entries.Count > 0)
        {
            var last = entries[0];
            LastSyncText = $"Last sync: {last.TimestampUtc.LocalDateTime:g}";
            await ComputeNextSync();
        }
    }

    private async Task ComputeNextSync()
    {
        var interval = await _config.GetSyncIntervalHoursAsync();
        var entries = await _logRepository.GetEntriesAsync(new LogFilter { Action = "sync_complete", Limit = 1 });
        if (entries.Count > 0)
        {
            var next = entries[0].TimestampUtc.AddHours(interval);
            NextSyncText = $"Next sync: {next.LocalDateTime:g}";
        }
    }

    private static string FormatStatus(ConnectionStatus s)
    {
        var msg = s.State switch
        {
            ConnectionState.Connected => "Connected",
            ConnectionState.NotConfigured => "Not configured",
            _ => s.Message ?? "Error"
        };
        return s.ResponseTimeMs > 0 ? $"{msg} ({s.ResponseTimeMs}ms)" : msg;
    }

    private static string StatusColor(ConnectionState state) => state switch
    {
        ConnectionState.Connected => "Green",
        ConnectionState.NotConfigured => "Gray",
        _ => "Red"
    };
}
