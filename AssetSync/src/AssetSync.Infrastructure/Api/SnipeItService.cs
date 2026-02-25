using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AssetSync.Core.Interfaces;
using AssetSync.Core.Models;

namespace AssetSync.Infrastructure.Api;

public class SnipeItService : ISnipeItService
{
    private readonly string _baseUrl;
    private readonly Func<string> _getApiKey;
    private readonly IHttpClientFactory _httpClientFactory;

    public SnipeItService(string baseUrl, Func<string> getApiKey, IHttpClientFactory httpClientFactory)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _getApiKey = getApiKey;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<Device?> GetAssetBySerialAsync(string normalizedSerial, CancellationToken cancellationToken = default)
    {
        var list = await SearchAssetsBySerialAsync(normalizedSerial, cancellationToken).ConfigureAwait(false);
        return list.Count == 1 ? list[0] : null;
    }

    public async Task<IReadOnlyList<Device>> SearchAssetsBySerialAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        var url = $"{_baseUrl}/api/v1/hardware?search={Uri.EscapeDataString(searchTerm)}";
        var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var doc = JsonDocument.Parse(json);
        var rows = doc.RootElement.TryGetProperty("rows", out var rowsEl) ? rowsEl : doc.RootElement;
        var list = new List<Device>();
        foreach (var r in rows.EnumerateArray())
        {
            var id = r.TryGetProperty("id", out var idEl) ? idEl.GetInt32() : 0;
            var serial = r.TryGetProperty("serial", out var s) ? s.GetString() : null;
            var name = r.TryGetProperty("name", out var n) ? n.GetString() : null;
            var modelId = r.TryGetProperty("model", out var mo) && mo.TryGetProperty("id", out var mid) ? mid.GetInt32() : (int?)null;
            var assignedTo = r.TryGetProperty("assigned_to", out var at) && at.ValueKind != JsonValueKind.Null && at.TryGetProperty("id", out var aid) ? aid.GetInt32() : (int?)null;
            var assetTag = r.TryGetProperty("asset_tag", out var tag) ? tag.GetString() : null;
            list.Add(new Device
            {
                SnipeItAssetId = id,
                SerialNumber = serial,
                NormalizedSerial = serial != null ? Core.Services.SerialNumberNormalizer.Normalize(serial) : "",
                DeviceName = name,
                SnipeItModelId = modelId,
                SnipeItAssignedUserId = assignedTo,
                SnipeItAssetTag = assetTag
            });
        }
        return list;
    }

    public async Task<Device?> CreateAssetAsync(Device device, CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        var payload = new Dictionary<string, object?>
        {
            ["name"] = device.DeviceName ?? device.SerialNumber,
            ["serial"] = device.SerialNumber,
            ["model_id"] = device.SnipeItModelId,
            ["assigned_to"] = device.SnipeItAssignedUserId,
            ["category_id"] = device.SnipeItCategoryId
        };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{_baseUrl}/api/v1/hardware", content, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var doc = JsonDocument.Parse(responseJson);
        var created = doc.RootElement.TryGetProperty("payload", out var p) ? p : doc.RootElement;
        var id = created.TryGetProperty("id", out var idEl) ? idEl.GetInt32() : 0;
        device.SnipeItAssetId = id;
        return device;
    }

    public async Task<bool> UpdateAssetAsync(int assetId, IReadOnlyDictionary<string, object?> updates, CancellationToken cancellationToken = default)
    {
        if (updates.Count == 0) return true;
        var client = CreateClient();
        var payload = new Dictionary<string, object?>();
        var customFields = new Dictionary<string, object?>();
        foreach (var kv in updates)
        {
            if (kv.Key == "os_version" || kv.Key == "windows_feature_update")
                customFields[kv.Key] = kv.Value;
            else
                payload[kv.Key] = kv.Value;
        }
        if (customFields.Count > 0)
            payload["custom_fields"] = customFields;
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PatchAsync($"{_baseUrl}/api/v1/hardware/{assetId}", content, cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _getApiKey());
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        return client;
    }
}
