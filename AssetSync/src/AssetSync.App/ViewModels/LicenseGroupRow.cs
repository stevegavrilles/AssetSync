using System;
using AssetSync.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AssetSync.App.ViewModels;

/// <summary>Observable row for the License Groups grid. Wraps a <see cref="GroupLicenseMapping"/> and
/// surfaces the last run status; <see cref="CanRerun"/> is true only when the mapping is halted/error
/// (the Rerun button is enabled only in that state).</summary>
public partial class LicenseGroupRow : ObservableObject
{
    public GroupLicenseMapping Mapping { get; }

    public LicenseGroupRow(GroupLicenseMapping mapping, string licenseName)
    {
        Mapping = mapping;
        _licenseName = licenseName;
        _status = string.IsNullOrEmpty(mapping.LastRunStatus) ? "—" : mapping.LastRunStatus!;
        _error = mapping.LastError;
    }

    public int Id => Mapping.Id;
    public string GroupId => Mapping.EntraGroupId;
    public string GroupName => Mapping.EntraGroupName;
    public bool ReadOnly => Mapping.ReadOnly;
    public string DirectionText => Mapping.ReadOnly ? "Read only (Entra → Snipe)" : "Write (Snipe → Entra) ⚠";

    /// <summary>Updates the mapping's direction and notifies the bound display.</summary>
    public void SetReadOnly(bool value)
    {
        Mapping.ReadOnly = value;
        OnPropertyChanged(nameof(ReadOnly));
        OnPropertyChanged(nameof(DirectionText));
    }

    [ObservableProperty] private string _licenseName;
    [ObservableProperty] private string _status;
    [ObservableProperty] private string? _error;

    public bool CanRerun =>
        string.Equals(Status, "halted", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Status, "error", StringComparison.OrdinalIgnoreCase);

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(CanRerun));

    public void ApplyResult(LicenseGroupMappingResult result)
    {
        Status = result.StatusText;
        Error = result.Status == LicenseGroupRunStatus.Ok ? result.Message : result.Message;
    }
}
