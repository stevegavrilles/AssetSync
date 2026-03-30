using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using AssetSync.Core.Interfaces;
using AssetSync.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using LogLevel = AssetSync.Core.Enums.LogLevel;
using SourceSystem = AssetSync.Core.Enums.SourceSystem;

namespace AssetSync.App.ViewModels;

public partial class LogsViewModel : ObservableObject
{
    private readonly ILogRepository _logRepository;
    private const int PageSize = 50;

    public ObservableCollection<LogEntry> LogEntries { get; } = new();

    // Filters
    [ObservableProperty] private DateTime? _filterFromDate;
    [ObservableProperty] private DateTime? _filterToDate;
    [ObservableProperty] private LogLevel? _filterMinLevel;
    [ObservableProperty] private SourceSystem? _filterSourceSystem;
    [ObservableProperty] private string _filterSerialNumber = "";
    [ObservableProperty] private string _filterFreeText = "";

    // Pagination
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private bool _hasPreviousPage;
    [ObservableProperty] private bool _hasNextPage;
    [ObservableProperty] private string _pageInfo = "";

    [ObservableProperty] private string _statusMessage = "";

    // Filter options for combo boxes
    public LogLevel[] LogLevels => [LogLevel.Debug, LogLevel.Info, LogLevel.Warning, LogLevel.Error];
    public SourceSystem[] SourceSystems => [SourceSystem.Application, SourceSystem.SnipeIt, SourceSystem.Intune, SourceSystem.Iru];

    public LogsViewModel(ILogRepository logRepository)
    {
        _logRepository = logRepository;
        _ = SearchAsync();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        CurrentPage = 1;
        await LoadPageAsync();
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (!HasNextPage) return;
        CurrentPage++;
        await LoadPageAsync();
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (!HasPreviousPage) return;
        CurrentPage--;
        await LoadPageAsync();
    }

    [RelayCommand]
    private void ClearFilters()
    {
        FilterFromDate = null;
        FilterToDate = null;
        FilterMinLevel = null;
        FilterSourceSystem = null;
        FilterSerialNumber = "";
        FilterFreeText = "";
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            DefaultExt = ".csv",
            FileName = $"assetsync_logs_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };
        if (dialog.ShowDialog() != true) return;

        var filter = BuildFilter();
        filter.Limit = 10000;
        filter.Offset = 0;
        var entries = await _logRepository.GetEntriesAsync(filter);

        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,Level,Source,Action,Serial,DeviceName,Field,OldValue,NewValue,Success,Error,SyncRunId");
        foreach (var e in entries)
        {
            sb.AppendLine($"\"{e.TimestampUtc:o}\",\"{e.Level}\",\"{e.SourceSystem}\",\"{e.Action}\",\"{Esc(e.SerialNumber)}\",\"{Esc(e.DeviceName)}\",\"{Esc(e.FieldName)}\",\"{Esc(e.OldValue)}\",\"{Esc(e.NewValue)}\",\"{e.Success}\",\"{Esc(e.ErrorDetail)}\",\"{e.SyncRunId}\"");
        }
        await File.WriteAllTextAsync(dialog.FileName, sb.ToString());
        StatusMessage = $"Exported {entries.Count} entries to {dialog.FileName}";
    }

    private async Task LoadPageAsync()
    {
        var filter = BuildFilter();
        filter.Limit = PageSize + 1; // fetch one extra to detect next page
        filter.Offset = (CurrentPage - 1) * PageSize;

        var entries = await _logRepository.GetEntriesAsync(filter);
        HasNextPage = entries.Count > PageSize;
        HasPreviousPage = CurrentPage > 1;

        LogEntries.Clear();
        var count = Math.Min(entries.Count, PageSize);
        for (var i = 0; i < count; i++)
            LogEntries.Add(entries[i]);

        PageInfo = $"Page {CurrentPage}";
    }

    private LogFilter BuildFilter()
    {
        return new LogFilter
        {
            FromUtc = FilterFromDate.HasValue ? new DateTimeOffset(FilterFromDate.Value, TimeSpan.Zero) : null,
            ToUtc = FilterToDate.HasValue ? new DateTimeOffset(FilterToDate.Value.AddDays(1), TimeSpan.Zero) : null,
            MinLevel = FilterMinLevel,
            SourceSystem = FilterSourceSystem,
            SerialNumber = string.IsNullOrWhiteSpace(FilterSerialNumber) ? null : FilterSerialNumber.Trim(),
            FreeText = string.IsNullOrWhiteSpace(FilterFreeText) ? null : FilterFreeText.Trim()
        };
    }

    private static string Esc(string? s) => s?.Replace("\"", "\"\"") ?? "";
}
