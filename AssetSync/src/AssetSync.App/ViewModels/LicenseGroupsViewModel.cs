using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
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
        // Load licenses first so per-line names and the dropdown resolve to the real software name
        // instead of an id-based label.
        await EnsureLicensesLoadedAsync();
        await LoadMappingsAsync();
    }

    private async Task EnsureLicensesLoadedAsync()
    {
        if (Licenses.Count > 0) return;
        try
        {
            var licenses = await _snipe.GetLicensesAsync();
            Licenses.Clear();
            foreach (var l in licenses) Licenses.Add(l);
        }
        catch
        {
            // Snipe-IT not reachable/configured yet — names fall back to an id label;
            // the "Fetch Licenses" button retries.
        }
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

    /// <summary>Toggles a mapping's direction (read-only Entra→Snipe vs. write Snipe→Entra) and
    /// persists it to the read_only column. Switching to write mode is gated behind a confirmation
    /// because it provisions/deprovisions Entra directory membership.</summary>
    [RelayCommand]
    private async Task ToggleDirectionAsync(LicenseGroupRow? row)
    {
        if (row == null) return;
        var switchingToWrite = row.ReadOnly; // currently read-only -> turning OFF -> write mode

        if (switchingToWrite)
        {
            // At most one write/provisioning group per license.
            var existingWriteGroup = FindWriteGroupName(row.Mapping.SnipeItLicenseId, row.Id);
            if (existingWriteGroup != null)
            {
                MessageBox.Show(
                    $"License already has a write/provisioning group: {existingWriteGroup}.\n\n" +
                    "Only one write group per license is allowed. Set that group back to read-only first " +
                    "if you want this one to be the write group.",
                    "Cannot enable write mode", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var answer = MessageBox.Show(
                $"Switch \"{row.GroupName}\" to WRITE mode (Snipe-IT → Entra)?\n\n" +
                "In write mode this app PROVISIONS and DEPROVISIONS Entra group membership: users " +
                "holding the Snipe-IT license seat are ADDED to the group, and members who no longer " +
                "hold a seat are REMOVED from the group (after the grace period).\n\n" +
                "This writes to your directory and requires the GroupMember.ReadWrite.All Graph permission.",
                "Enable write / provisioning direction?",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return;
        }

        var newReadOnly = !row.ReadOnly;
        try
        {
            row.Mapping.ReadOnly = newReadOnly;
            await _repo.SaveGroupLicenseMappingAsync(row.Mapping);
            row.SetReadOnly(newReadOnly);
            StatusMessage = newReadOnly
                ? $"\"{row.GroupName}\" set to read-only (Entra → Snipe-IT)."
                : $"\"{row.GroupName}\" set to WRITE mode (Snipe-IT → Entra). The next sync will provision/deprovision its group membership.";
        }
        catch (Exception ex)
        {
            row.Mapping.ReadOnly = !newReadOnly; // revert in-memory on failure
            StatusMessage = $"Failed to change direction: {ex.Message}";
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

        // At most one write/provisioning group per license.
        if (!NewReadOnly)
        {
            var existingWriteGroup = FindWriteGroupName(SelectedLicense.Id, excludeRowId: -1);
            if (existingWriteGroup != null)
            {
                StatusMessage = $"License already has a write/provisioning group: {existingWriteGroup}. Add this group as read-only, or change the existing one first.";
                return;
            }
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
                        warning = "group is dynamic (not membership-writable) — fine for read-only, but it cannot be used as the write/provisioning group";
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
        StatusMessage = $"Re-running license for '{row.GroupName}'...";
        try
        {
            // Rerun re-runs the WHOLE license reconcile (read union + the single write group).
            var results = await _engine.RunLicenseAsync(row.Mapping.SnipeItLicenseId, dryRun: false);
            foreach (var r in results)
            {
                var target = Mappings.FirstOrDefault(m => m.Id == r.MappingId);
                target?.ApplyResult(r);
            }
            StatusMessage = $"Re-ran license for '{row.GroupName}': {results.Count} group(s) processed.";
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

    /// <summary>Returns the name of an existing write/provisioning group on the license (other than
    /// <paramref name="excludeRowId"/>), or null if none — enforces one write group per license.</summary>
    private string? FindWriteGroupName(int licenseId, int excludeRowId) =>
        Mappings.FirstOrDefault(m => m.Id != excludeRowId && m.Mapping.SnipeItLicenseId == licenseId && !m.ReadOnly)?.GroupName;
}
