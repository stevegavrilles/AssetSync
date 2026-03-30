using System.Net.Http.Headers;
using System.Text;
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
        var offset = 0;

        while (true)
        {
            var url = $"{_baseUrl}/api/v1/devices?limit={PageSize}&offset={offset}";
            var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Kandji returns an array of devices directly, or an object with "results"
            JsonElement devices;
            if (root.ValueKind == JsonValueKind.Array)
                devices = root;
            else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
                devices = results;
            else
                break;

            var pageCount = 0;
            foreach (var r in devices.EnumerateArray())
            {
                if (r.ValueKind != JsonValueKind.Object) continue;
                pageCount++;
                var serial = r.TryGetProperty("serial_number", out var sn) ? sn.GetString() ?? "" : "";
                var deviceName = r.TryGetProperty("device_name", out var dn) ? dn.GetString() : null;
                var model = r.TryGetProperty("model", out var m) ? m.GetString() : null;
                var osVersion = r.TryGetProperty("os_version", out var ov) ? ov.GetString() : null;
                var deviceId = r.TryGetProperty("device_id", out var did) ? did.GetString() : null;
                deviceId ??= r.TryGetProperty("id", out var id) ? id.GetString() : null;
                string? userEmail = null;
                if (r.TryGetProperty("user", out var user) && user.ValueKind == JsonValueKind.Object && user.TryGetProperty("email", out var em))
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

            // If we got fewer than PageSize results, we've reached the end
            if (pageCount < PageSize)
                break;

            offset += pageCount;
        }

        return list;
    }

    public async Task<bool> WriteBackAssetTagAsync(string iruDeviceId, string assetTag, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _getToken());
            var payload = JsonSerializer.Serialize(new { asset_tag = assetTag });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await client.PatchAsync($"{_baseUrl}/api/v1/devices/{iruDeviceId}", content, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
