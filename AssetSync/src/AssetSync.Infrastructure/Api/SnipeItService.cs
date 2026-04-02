using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AssetSync.Core.Interfaces;
using AssetSync.Core.Models;

namespace AssetSync.Infrastructure.Api;

public class SnipeItService : ISnipeItService
{
    private const int MaxRetries = 3;
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
        // Use the dedicated byserial endpoint for exact matching — avoids broad search misses
        var url = $"{_baseUrl}/api/v1/hardware/byserial/{Uri.EscapeDataString(searchTerm)}";
        var response = await SendWithRetryAsync(HttpMethod.Get, url, null, cancellationToken).ConfigureAwait(false);

        // 404 means no asset found — not an error
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return new List<Device>();

        if (!response.IsSuccessStatusCode)
            return new List<Device>();

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // byserial can return a single object or a {"rows":[...]} collection
        JsonElement rows;
        if (root.ValueKind == JsonValueKind.Array)
            rows = root;
        else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("rows", out var rowsEl) && rowsEl.ValueKind == JsonValueKind.Array)
            rows = rowsEl;
        else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("id", out _))
        {
            // Single asset returned directly
            var single = ParseDevice(root);
            return single != null ? new List<Device> { single } : new List<Device>();
        }
        else
            return new List<Device>();

        var list = new List<Device>();
        if (rows.ValueKind != JsonValueKind.Array) return list;
        foreach (var r in rows.EnumerateArray())
        {
            if (r.ValueKind != JsonValueKind.Object) continue;
            var device = ParseDevice(r);
            if (device != null) list.Add(device);
        }
        return list;
    }

    private static Device? ParseDevice(JsonElement r)
    {
        if (r.ValueKind != JsonValueKind.Object) return null;
        var id = r.TryGetProperty("id", out var idEl) ? idEl.GetInt32() : 0;
        var serial = r.TryGetProperty("serial", out var s) ? s.GetString() : null;
        // Snipe-IT HTML-encodes special characters (e.g. apostrophes become &#039;) — decode before storing
        var name = r.TryGetProperty("name", out var n) ? WebUtility.HtmlDecode(n.GetString()) : null;
        var modelId = r.TryGetProperty("model", out var mo) && mo.ValueKind == JsonValueKind.Object && mo.TryGetProperty("id", out var mid) ? mid.GetInt32() : (int?)null;
        var assignedTo = r.TryGetProperty("assigned_to", out var at) && at.ValueKind == JsonValueKind.Object && at.TryGetProperty("id", out var aid) ? aid.GetInt32() : (int?)null;
        var assetTag = r.TryGetProperty("asset_tag", out var tag) ? WebUtility.HtmlDecode(tag.GetString()) : null;
        return new Device
        {
            SnipeItAssetId = id,
            SerialNumber = serial,
            NormalizedSerial = serial != null ? Core.Services.SerialNumberNormalizer.Normalize(serial) : "",
            DeviceName = name,
            SnipeItModelId = modelId,
            SnipeItAssignedUserId = assignedTo,
            SnipeItAssetTag = assetTag
        };
    }

    public async Task<Device?> CreateAssetAsync(Device device, CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["name"] = device.DeviceName ?? device.SerialNumber,
            ["serial"] = device.SerialNumber,
            ["model_id"] = device.SnipeItModelId,
            ["status_id"] = 1, // Default to "Ready to Deploy"
        };
        // Only include optional fields when they have values — omitting null avoids Snipe-IT validation errors
        if (device.SnipeItAssignedUserId.HasValue)
            payload["assigned_to"] = device.SnipeItAssignedUserId.Value;
        if (device.SnipeItCategoryId.HasValue)
            payload["category_id"] = device.SnipeItCategoryId.Value;
        // Use the MDM asset tag if present, otherwise generate a unique TEMP tag (e.g. TEMP1A2B3C4D)
        // Snipe-IT requires an asset_tag on create. Use GUID-derived hex for ~4B combinations.
        var assetTag = !string.IsNullOrEmpty(device.MdmAssetTag)
            ? device.MdmAssetTag
            : $"TEMP{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        payload["asset_tag"] = assetTag;

        var payloadJson = JsonSerializer.Serialize(payload);
        var response = await SendWithRetryAsync(HttpMethod.Post, $"{_baseUrl}/api/v1/hardware", () => new StringContent(payloadJson, Encoding.UTF8, "application/json"), cancellationToken).ConfigureAwait(false);
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        // Snipe-IT returns HTTP 200 even on validation errors — check the status field in the body
        JsonDocument doc;
        try { doc = JsonDocument.Parse(responseJson); }
        catch { return null; }

        var root = doc.RootElement;

        // {"status":"error","messages":{...}} means the create failed
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("status", out var statusEl) &&
            statusEl.GetString() == "error")
        {
            // Surface the actual validation message so callers can log it
            var messages = root.TryGetProperty("messages", out var msgEl) ? msgEl.GetRawText() : responseJson;
            throw new InvalidOperationException($"Snipe-IT create rejected: {messages}");
        }

        if (!response.IsSuccessStatusCode) return null;

        var created = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("payload", out var p) ? p : root;
        var id = created.ValueKind == JsonValueKind.Object && created.TryGetProperty("id", out var idEl) ? idEl.GetInt32() : 0;
        device.SnipeItAssetId = id;
        return device;
    }

    public async Task<bool> UpdateAssetAsync(int assetId, IReadOnlyDictionary<string, object?> updates, CancellationToken cancellationToken = default)
    {
        if (updates.Count == 0) return true;
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
        var payloadJson = JsonSerializer.Serialize(payload);
        var response = await SendWithRetryAsync(HttpMethod.Patch, $"{_baseUrl}/api/v1/hardware/{assetId}", () => new StringContent(payloadJson, Encoding.UTF8, "application/json"), cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<SnipeItLookup>> GetModelsAsync(CancellationToken cancellationToken = default)
        => await FetchLookupsAsync("/api/v1/models", cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<SnipeItLookup>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        => await FetchLookupsAsync("/api/v1/categories", cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<SnipeItLookup>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<SnipeItLookup>();
        var offset = 0;
        const int limit = 500;
        while (true)
        {
            var response = await SendWithRetryAsync(HttpMethod.Get, $"{_baseUrl}/api/v1/users?limit={limit}&offset={offset}", null, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) break;
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var rows = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("rows", out var r) ? r : root;
            if (rows.ValueKind != JsonValueKind.Array) break;
            int count = 0;
            foreach (var u in rows.EnumerateArray())
            {
                if (u.ValueKind != JsonValueKind.Object) continue;
                count++;
                var id = u.TryGetProperty("id", out var idEl) ? idEl.GetInt32() : 0;
                var first = u.TryGetProperty("first_name", out var fn) ? fn.GetString() ?? "" : "";
                var last = u.TryGetProperty("last_name", out var ln) ? ln.GetString() ?? "" : "";
                var uname = u.TryGetProperty("username", out var un) ? un.GetString() ?? "" : "";
                var name = !string.IsNullOrEmpty(first) || !string.IsNullOrEmpty(last) ? $"{first} {last}".Trim() : uname;
                list.Add(new SnipeItLookup { Id = id, Name = name });
            }
            if (count < limit) break;
            offset += count;
        }
        return list;
    }

    private async Task<IReadOnlyList<SnipeItLookup>> FetchLookupsAsync(string endpoint, CancellationToken cancellationToken)
    {
        var list = new List<SnipeItLookup>();
        var offset = 0;
        const int limit = 500;
        while (true)
        {
            var response = await SendWithRetryAsync(HttpMethod.Get, $"{_baseUrl}{endpoint}?limit={limit}&offset={offset}", null, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) break;
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var rows = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("rows", out var r) ? r : root;
            if (rows.ValueKind != JsonValueKind.Array) break;
            int count = 0;
            foreach (var item in rows.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                count++;
                var id = item.TryGetProperty("id", out var idEl) ? idEl.GetInt32() : 0;
                var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                list.Add(new SnipeItLookup { Id = id, Name = name });
            }
            if (count < limit) break;
            offset += count;
        }
        return list;
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(HttpMethod method, string url, Func<HttpContent?>? bodyFactory, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            var client = CreateClient();
            var request = new HttpRequestMessage(method, url) { Content = bodyFactory?.Invoke() };
            var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.TooManyRequests || attempt == MaxRetries)
                return response;

            // Honor Retry-After header if present, otherwise exponential backoff
            var delay = response.Headers.RetryAfter?.Delta
                        ?? TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
        throw new InvalidOperationException("Unreachable");
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _getApiKey());
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        return client;
    }
}
