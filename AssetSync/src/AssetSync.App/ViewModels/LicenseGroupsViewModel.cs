using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AssetSync.Core.Interfaces;
using AssetSync.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AssetSync.App.ViewModels;

public partial class LicenseGroupsViewModel : ObservableObject
{
    private readonly IMappingRepository _repo;
    private readonly ISnipeItService _snipe;
    private readonly IEntraDirectoryService _entra;
    private readonly ILicenseGroupSyncEngine _engine;

    public ObservableCollection<SnipeItLookup> Licenses { get; } = new();
    public ObservableCollection<LicenseGroupRow> Mappings { get; } = new();

    [ObservableProperty] private string _newGroupId = "";
    [ObservableProperty] private SnipeItLookup? _selectedLicense;
    [ObservableProperty] private bool _newReadOnly = true; // default ON — read-only (no directory writes)
    [ObservableProperty] private string _statusMessage =
        "Phase 1: read-only sync (Entra → Snipe-IT seats). Click 'Fetch Licenses', then add a group mapping.";
    [ObservableProperty] private bool _isLoading;

    public LicenseGroupsViewModel(IMappingRepository repo, ISnipeItService snipe, IEntraDirectoryService entra, ILicenseGroupSyncEngine engine)
    {
        _repo = repo;
        _snipe = snipe;
        _entra = entra;
        _engine = engine;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await LoadMappingsAsync();
    }

    private async Task LoadMappingsAsync()
    {
        var mappings = await _repo.GetGroupLicenseMappingsAsync();
        Mappings.Clear();
        foreach (var m in mappings)
        {
            var licenseName = Licenses.FirstOrDefault(l => l.Id == m.SnipeItLicenseId)?.Name ?? $"License #{m.SnipeItLicenseId}";
            Mappings.Add(new LicenseGroupRow(m, licenseName));
        }
    }

    [RelayCommand]
    private async Task FetchLicensesAsync()
    {
        IsLoading = true;
        StatusMessage = "Fetching licenses from Snipe-IT...";
        try
        {
            var licenses = await _snipe.GetLicensesAsync();
            Licenses.Clear();
            foreach (var l in licenses) Licenses.Add(l);
            await LoadMappingsAsync(); // refresh license-name display
            StatusMessage = $"Loaded {licenses.Count} Snipe-IT license(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fetch failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddMappingAsync()
    {
        if (string.IsNullOrWhiteSpace(NewGroupId) || SelectedLicense == null)
        {
            StatusMessage = "Enter an Entra group ID and select a Snipe-IT license first.";
            return;
        }

        IsLoading = true;
        try
        {
            // Best-effort: resolve the group name and surface a warning if it's missing/dynamic.
            var groupName = NewGroupId.Trim();
            string? warning = null;
            try
            {
                var info = await _entra.GetGroupInfoAsync(NewGroupId.Trim());
                if (!info.Exists)
                    warning = "group not found in Entra — saved anyway; verify the ID";
                else
                {
                    groupName = string.IsNullOrEmpty(info.DisplayName) ? groupName : info.DisplayName;
                    if (!info.IsMembershipWritable)
                        warning = "group is dynamic (not membership-writable) — fine for read-only, but it cannot be used for the Phase 2 write direction";
                }
            }
            catch (Exception ex)
            {
                warning = $"could not resolve group name from Entra ({ex.Message}) — saved with the raw ID";
            }

            await _repo.SaveGroupLicenseMappingAsync(new GroupLicenseMapping
            {
                EntraGroupId = NewGroupId.Trim(),
                EntraGroupName = groupName,
                SnipeItLicenseId = SelectedLicense.Id,
                ReadOnly = NewReadOnly
            });

            await LoadMappingsAsync();
            NewGroupId = "";
            StatusMessage = warning == null ? $"Added mapping for '{groupName}'." : $"Added mapping for '{groupName}' ({warning}).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Add failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DeleteMappingAsync(LicenseGroupRow? row)
    {
        if (row == null) return;
        await _repo.DeleteGroupLicenseMappingAsync(row.Id);
        Mappings.Remove(row);
        StatusMessage = $"Deleted mapping for '{row.GroupName}'.";
    }

    [RelayCommand]
    private Task RunAllAsync() => RunAsync(dryRun: false);

    [RelayCommand]
    private Task DryRunAllAsync() => RunAsync(dryRun: true);

    private async Task RunAsync(bool dryRun)
    {
        IsLoading = true;
        StatusMessage = dryRun ? "Running dry run..." : "Running license-group sync...";
        try
        {
            var summary = await _engine.RunAsync(dryRun);
            foreach (var result in summary.Mappings)
            {
                var row = Mappings.FirstOrDefault(m => m.Id == result.MappingId);
                row?.ApplyResult(result);
            }
            var halted = summary.Mappings.Count(m => m.Status == LicenseGroupRunStatus.Halted);
            var errors = summary.Mappings.Count(m => m.Status == LicenseGroupRunStatus.Error);
            StatusMessage = $"{(dryRun ? "[DRY RUN] " : "")}Completed {summary.Mappings.Count} mapping(s): {errors} error, {halted} halted. Halted/error rows can be re-run individually.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Sync failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RerunMappingAsync(LicenseGroupRow? row)
    {
        if (row == null) return;
        IsLoading = true;
        StatusMessage = $"Re-running '{row.GroupName}'...";
        try
        {
            var result = await _engine.RunMappingAsync(row.Mapping, dryRun: false);
            row.ApplyResult(result);
            StatusMessage = $"'{row.GroupName}': {result.StatusText} — {result.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Rerun failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
