using AssetSync.Core.Interfaces;
using AssetSync.Core.Models;
using AssetSync.Core.Services;
using Azure.Identity;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Authentication.Azure;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace AssetSync.Infrastructure.Api;

public class IntuneService : IIntuneService
{
    private const int PageSize = 1000;
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

    private static Task CollectPageAsync(ManagedDeviceCollectionResponse? page, List<Device> list)
    {
        if (page?.Value == null) return Task.CompletedTask;
        foreach (var m in page.Value)
        {
            var serial = m.SerialNumber ?? "";
            list.Add(new Device
            {
                NormalizedSerial = SerialNumberNormalizer.Normalize(serial),
                SerialNumber = serial,
                DeviceName = m.DeviceName,
                Model = m.Model,
                AssignedUserUpn = m.UserPrincipalName,
                OsVersion = m.OSVersion,
                DeviceType = m.DeviceType?.ToString(),
                PlatformSource = "Intune",
                AzureAdDeviceId = m.AzureADDeviceId,
                OperatingSystem = m.OperatingSystem
            });
        }
        return Task.CompletedTask;
    }
}
