using System.Diagnostics;
using AssetSync.Core.Enums;
using AssetSync.Core.Interfaces;
using AssetSync.Core.Models;

namespace AssetSync.Infrastructure.Connectivity;

public class ConnectivityTester : IConnectivityTester
{
    private readonly Func<Task<ConnectionStatus>> _testSnipeIt;
    private readonly Func<Task<ConnectionStatus>> _testIntune;
    private readonly Func<Task<ConnectionStatus>> _testIru;

    public ConnectivityTester(
        Func<Task<ConnectionStatus>> testSnipeIt,
        Func<Task<ConnectionStatus>> testIntune,
        Func<Task<ConnectionStatus>> testIru)
    {
        _testSnipeIt = testSnipeIt;
        _testIntune = testIntune;
        _testIru = testIru;
    }

    public async Task<ConnectionStatus> TestSnipeItAsync(CancellationToken cancellationToken = default)
    {
        return await _testSnipeIt().ConfigureAwait(false);
    }

    public async Task<ConnectionStatus> TestIntuneAsync(CancellationToken cancellationToken = default)
    {
        return await _testIntune().ConfigureAwait(false);
    }

    public async Task<ConnectionStatus> TestIruAsync(CancellationToken cancellationToken = default)
    {
        return await _testIru().ConfigureAwait(false);
    }

    public static async Task<ConnectionStatus> TestGetAsync(string url, string bearerToken, SourceSystem service, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
            var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            sw.Stop();
            return new ConnectionStatus
            {
                Service = service,
                State = response.IsSuccessStatusCode ? ConnectionState.Connected : ConnectionState.Error,
                Message = response.IsSuccessStatusCode ? "OK" : $"HTTP {(int)response.StatusCode}",
                ResponseTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ConnectionStatus
            {
                Service = service,
                State = ConnectionState.Error,
                Message = ex.Message,
                ResponseTimeMs = sw.ElapsedMilliseconds
            };
        }
    }
}
