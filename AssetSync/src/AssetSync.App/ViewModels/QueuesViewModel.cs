using System.Collections.ObjectModel;
using AssetSync.Core.Interfaces;
using AssetSync.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AssetSync.App.ViewModels;

public partial class QueuesViewModel : ObservableObject
{
    private readonly ILogRepository _logRepository;

    public ObservableCollection<LogEntry> PendingModels { get; } = new();
    public ObservableCollection<LogEntry> UnmatchedUsers { get; } = new();
    public ObservableCollection<LogEntry> DuplicateSerials { get; } = new();

    [ObservableProperty] private int _pendingModelsCount;
    [ObservableProperty] private int _unmatchedUsersCount;
    [ObservableProperty] private int _duplicateSerialsCount;
    [ObservableProperty] private string _statusMessage = "";

    public QueuesViewModel(ILogRepository logRepository)
    {
        _logRepository = logRepository;
        _ = LoadQueuesAsync();
    }

    [RelayCommand]
    private async Task LoadQueuesAsync()
    {
        // Find the most recent sync run
        var recentSync = await _logRepository.GetEntriesAsync(new LogFilter
        {
            Action = "sync_complete",
            Limit = 1
        });
        var syncRunId = recentSync.Count > 0 ? recentSync[0].SyncRunId : null;

        // Pending model mappings
        var pendingFilter = new LogFilter { FreeText = "Pending model mapping", Limit = 500 };
        if (syncRunId != null) pendingFilter.SyncRunId = syncRunId;
        var pending = await _logRepository.GetEntriesAsync(pendingFilter);
        PendingModels.Clear();
        foreach (var e in pending) PendingModels.Add(e);
        PendingModelsCount = pending.Count;

        // Duplicate serials
        var dupeFilter = new LogFilter { FreeText = "Multiple Snipe-IT assets", Limit = 500 };
        if (syncRunId != null) dupeFilter.SyncRunId = syncRunId;
        var dupes = await _logRepository.GetEntriesAsync(dupeFilter);
        DuplicateSerials.Clear();
        foreach (var e in dupes) DuplicateSerials.Add(e);
        DuplicateSerialsCount = dupes.Count;

        // Unmatched users (may be empty until SyncEngine logs user mapping misses)
        var userFilter = new LogFilter { FreeText = "user mapping", Limit = 500 };
        if (syncRunId != null) userFilter.SyncRunId = syncRunId;
        var users = await _logRepository.GetEntriesAsync(userFilter);
        UnmatchedUsers.Clear();
        foreach (var e in users) UnmatchedUsers.Add(e);
        UnmatchedUsersCount = users.Count;

        StatusMessage = syncRunId != null ? $"Showing queues from sync run {syncRunId[..8]}..." : "No sync runs found.";
    }
}
