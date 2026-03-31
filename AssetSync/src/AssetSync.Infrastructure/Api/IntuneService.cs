using System.Text.RegularExpressions;
using AssetSync.Core.Interfaces;
using AssetSync.Core.Services;
using Device = AssetSync.Core.Models.Device;
using Azure.Identity;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Authentication.Azure;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace AssetSync.Infrastructure.Api;

public class IntuneService : IIntuneService
{
    private const int PageSize = 1000;
    // Matches PM followed by exactly 7 digits anywhere in a string (e.g. Notes field)
    private static readonly Regex PmTagRegex = new(@"\bPM\d{7}\b", RegexOptions.IgnoreCase);
    private readonly string _tenantId;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly Func<string> _getClientSecret;

    public IntuneService(string tenantId, string clientId, Func<string> getClientSecret)
    {
        _tenantId = tenantId;
        _clientId = clientId;
        _getClientSecret = getClientSecret;
        _clientSecret = string.Empty;
    }

    public IntuneService(string tenantId, string clientId, string clientSecret)
    {
        _tenantId = tenantId;
        _clientId = clientId;
        _clientSecret = clientSecret;
        _getClientSecret = () => _clientSecret;
    }

    public async Task<IReadOnlyList<Device>> GetManagedDevicesAsync(CancellationToken cancellationToken = default)
    {
        var credential = new ClientSecretCredential(_tenantId, _clientId, _getClientSecret());
        var authProvider = new AzureIdentityAuthenticationProvider(credential, scopes: new[] { "https://graph.microsoft.com/.default" });
        var adapter = new HttpClientRequestAdapter(authProvider);
        var client = new Microsoft.Graph.GraphServiceClient(adapter);

        var list = new List<Device>();
        var page = await client.DeviceManagement.ManagedDevices.GetAsync(c => c.QueryParameters.Top = PageSize, cancellationToken).ConfigureAwait(false);
        await CollectPageAsync(page, list).ConfigureAwait(false);

        while (page?.OdataNextLink != null)
        {
            page = await client.DeviceManagement.ManagedDevices.WithUrl(page.OdataNextLink!).GetAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            await CollectPageAsync(page, list).ConfigureAwait(false);
        }

        return list;
    }

    public async Task<bool> WriteBackAssetTagAsync(string intuneDeviceId, string assetTag, string? existingNotes, CancellationToken cancellationToken = default)
    {
        try
        {
            var credential = new ClientSecretCredential(_tenantId, _clientId, _getClientSecret());
            var authProvider = new AzureIdentityAuthenticationProvider(credential, scopes: new[] { "https://graph.microsoft.com/.default" });
            var adapter = new HttpClientRequestAdapter(authProvider);
            var client = new Microsoft.Graph.GraphServiceClient(adapter);

            // Build updated notes: replace existing PM tag line if present, otherwise append
            string updatedNotes;
            var tagLine = $"Asset Tag: {assetTag}";
            if (!string.IsNullOrEmpty(existingNotes))
            {
                // Replace any existing "Asset Tag: PMxxxxxxx" line, or append if not found
                var replaced = System.Text.RegularExpressions.Regex.Replace(
                    existingNotes,
                    @"Asset Tag:\s*PM\d{7}",
                    tagLine,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                updatedNotes = replaced == existingNotes
                    ? existingNotes.TrimEnd() + "\n" + tagLine
                    : replaced;
            }
            else
            {
                updatedNotes = tagLine;
            }

            // Uses the Intune managed device ID (m.Id), NOT the Azure AD device ID
            var body = new ManagedDevice { Notes = updatedNotes };
            await client.DeviceManagement.ManagedDevices[intuneDeviceId].PatchAsync(body, cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            // Re-throw so SyncEngine can log the real error detail
            throw new InvalidOperationException($"Intune write-back failed for device {intuneDeviceId}: {ex.Message}", ex);
        }
    }

    private static Task CollectPageAsync(ManagedDeviceCollectionResponse? page, List<Device> list)
    {
        if (page?.Value == null) return Task.CompletedTask;
        foreach (var m in page.Value)
        {
            var serial = m.SerialNumber ?? "";
            // Extract a PM-format asset tag from the Notes field if present
            string? mdmAssetTag = null;
            if (!string.IsNullOrEmpty(m.Notes))
            {
                var match = PmTagRegex.Match(m.Notes);
                if (match.Success) mdmAssetTag = match.Value.ToUpperInvariant();
            }
            list.Add(new Device
            {
                NormalizedSerial = SerialNumberNormalizer.Normalize(serial),
                SerialNumber = serial,
                DeviceName = m.DeviceName,
                Model = m.Model,
                AssignedUserUpn = m.UserPrincipalName,
                OsVersion = m.OsVersion,
                DeviceType = m.DeviceCategoryDisplayName ?? m.ManagementAgent?.ToString(),
                PlatformSource = "Intune",
                AzureAdDeviceId = m.AzureADDeviceId,
                OperatingSystem = m.OperatingSystem,
                MdmAssetTag = mdmAssetTag,
                IntuneNotes = m.Notes,
                IntuneDeviceId = m.Id
            });
        }
        return Task.CompletedTask;
    }
}
