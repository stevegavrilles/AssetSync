using System.Net.Http.Headers;
using System.Text.Json;
using AssetSync.Core.Interfaces;
using AssetSync.Core.Models;
using AssetSync.Core.Services;

namespace AssetSync.Infrastructure.Api;

public class IruService : IIruService
{
    private const int PageSize = 300;
    private readonly string _baseUrl;
    private readonly Func<string> _getToken;
    private readonly IHttpClientFactory _httpClientFactory;

    public IruService(string baseUrl, Func<string> getToken, IHttpClientFactory httpClientFactory)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _getToken = getToken;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IReadOnlyList<Device>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _getToken());
        var list = new List<Device>();
        var nextUrl = $"{_baseUrl}/api/v1/devices?limit={PageSize}";

        while (nextUrl != null)
        {
            var response = await client.GetAsync(nextUrl, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("results", out var results))
            {
                foreach (var r in results.EnumerateArray())
                {
                    var serial = r.TryGetProperty("serial_number", out var sn) ? sn.GetString() ?? "" : "";
                    var deviceName = r.TryGetProperty("device_name", out var dn) ? dn.GetString() : null;
                    var model = r.TryGetProperty("model", out var m) ? m.GetString() : null;
                    var osVersion = r.TryGetProperty("os_version", out var ov) ? ov.GetString() : null;
                    var deviceId = r.TryGetProperty("id", out var id) ? id.GetString() : null;
                    string? userEmail = null;
                    if (r.TryGetProperty("user", out var user) && user.TryGetProperty("email", out var em))
                        userEmail = em.GetString();
                    list.Add(new Device
                    {
                        NormalizedSerial = SerialNumberNormalizer.Normalize(serial),
                        SerialNumber = serial,
                        DeviceName = deviceName,
                        Model = model,
                        AssignedUserUpn = userEmail,
                        OsVersion = osVersion,
                        PlatformSource = "Iru",
                        IruDeviceId = deviceId,
                        DeviceType = r.TryGetProperty("device_family", out var df) ? df.GetString() : null
                    });
                }
            }
            nextUrl = root.TryGetProperty("next", out var next) ? next.GetString() : null;
        }

        return list;
    }
}
