using System.IO;
using AssetSync.Core;
using AssetSync.Core.Enums;
using AssetSync.Core.Interfaces;
using AssetSync.Core.Models;
using AssetSync.Core.Services;
using AssetSync.Infrastructure.Api;
using AssetSync.Infrastructure.Connectivity;
using AssetSync.Infrastructure.Data;
using AssetSync.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AssetSync.Service;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AssetSync", "assetsync.db");
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var connectionString = $"Data Source={dbPath}";
        var initializer = new DatabaseInitializer(connectionString);
        initializer.Initialize();

        var services = builder.Services;

        services.AddSingleton(_ => connectionString);
        services.AddSingleton<ICredentialStore>(sp => new DpapiCredentialStore(sp.GetRequiredService<string>()));
        services.AddSingleton<ILogRepository>(sp => new SqliteLogRepository(sp.GetRequiredService<string>()));
        services.AddSingleton<IConfigRepository>(sp => new SqliteConfigRepository(sp.GetRequiredService<string>()));
        services.AddSingleton<IMappingRepository>(sp => new SqliteMappingRepository(sp.GetRequiredService<string>()));
        services.AddHttpClient();

        services.AddSingleton<DeviceMerger>();
        services.AddSingleton<ConflictResolver>();
        services.AddTransient<BuildVersionMapper>();

        services.AddTransient<ISnipeItService>(sp =>
        {
            var config = sp.GetRequiredService<IConfigRepository>();
            var creds = sp.GetRequiredService<ICredentialStore>();
            var url = config.GetAsync(ConfigKeys.SnipeItUrl).GetAwaiter().GetResult() ?? "";
            return new SnipeItService(url, () => creds.GetAsync(CredentialKeys.SnipeItApiKey).GetAwaiter().GetResult() ?? "", sp.GetRequiredService<IHttpClientFactory>());
        });

        services.AddTransient<IIntuneService>(sp =>
        {
            var config = sp.GetRequiredService<IConfigRepository>();
            var creds = sp.GetRequiredService<ICredentialStore>();
            var tenantId = config.GetAsync(ConfigKeys.IntuneTenantId).GetAwaiter().GetResult() ?? "";
            var clientId = config.GetAsync(ConfigKeys.IntuneClientId).GetAwaiter().GetResult() ?? "";
            return new IntuneService(tenantId, clientId, () => creds.GetAsync(CredentialKeys.IntuneClientSecret).GetAwaiter().GetResult() ?? "");
        });

        services.AddTransient<IIruService>(sp =>
        {
            var config = sp.GetRequiredService<IConfigRepository>();
            var creds = sp.GetRequiredService<ICredentialStore>();
            var baseUrl = config.GetAsync(ConfigKeys.IruBaseUrl).GetAwaiter().GetResult() ?? "";
            return new IruService(baseUrl, () => creds.GetAsync(CredentialKeys.IruApiToken).GetAwaiter().GetResult() ?? "", sp.GetRequiredService<IHttpClientFactory>());
        });

        services.AddTransient<IConnectivityTester>(sp =>
        {
            var config = sp.GetRequiredService<IConfigRepository>();
            var creds = sp.GetRequiredService<ICredentialStore>();
            return new ConnectivityTester(
                async () =>
                {
                    var url = await config.GetAsync(ConfigKeys.SnipeItUrl);
                    var key = await creds.GetAsync(CredentialKeys.SnipeItApiKey);
                    if (string.IsNullOrEmpty(url)) return new ConnectionStatus { Service = SourceSystem.SnipeIt, State = ConnectionState.NotConfigured, Message = "Not configured" };
                    return await ConnectivityTester.TestGetAsync($"{url.TrimEnd('/')}/api/v1/hardware?limit=1", key ?? "", SourceSystem.SnipeIt);
                },
                async () =>
                {
                    var tenantId = await config.GetAsync(ConfigKeys.IntuneTenantId);
                    if (string.IsNullOrEmpty(tenantId)) return new ConnectionStatus { Service = SourceSystem.Intune, State = ConnectionState.NotConfigured, Message = "Not configured" };
                    var clientId = await config.GetAsync(ConfigKeys.IntuneClientId);
                    var secret = await creds.GetAsync(CredentialKeys.IntuneClientSecret);
                    if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(secret)) return new ConnectionStatus { Service = SourceSystem.Intune, State = ConnectionState.NotConfigured, Message = "Credentials incomplete" };
                    try
                    {
                        var svc = new IntuneService(tenantId, clientId!, secret!);
                        await svc.GetManagedDevicesAsync(new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token);
                        return new ConnectionStatus { Service = SourceSystem.Intune, State = ConnectionState.Connected, Message = "OK" };
                    }
                    catch (Exception ex)
                    {
                        return new ConnectionStatus { Service = SourceSystem.Intune, State = ConnectionState.Error, Message = ex.Message };
                    }
                },
                async () =>
                {
                    var url = await config.GetAsync(ConfigKeys.IruBaseUrl);
                    var token = await creds.GetAsync(CredentialKeys.IruApiToken);
                    if (string.IsNullOrEmpty(url)) return new ConnectionStatus { Service = SourceSystem.Iru, State = ConnectionState.NotConfigured, Message = "Not configured" };
                    return await ConnectivityTester.TestGetAsync($"{url.TrimEnd('/')}/api/v1/devices?limit=1", token ?? "", SourceSystem.Iru);
                });
        });

        services.AddTransient<IWebhookService>(sp =>
        {
            var config = sp.GetRequiredService<IConfigRepository>();
            var url = config.GetAsync(ConfigKeys.WebhookUrl).GetAwaiter().GetResult();
            var type = config.GetAsync(ConfigKeys.WebhookType).GetAwaiter().GetResult() ?? "Generic";
            return new WebhookService(url, type, sp.GetRequiredService<IHttpClientFactory>());
        });

        services.AddTransient<ISyncEngine, SyncEngine>();
        services.AddHostedService<SyncWorker>();

        await builder.Build().RunAsync().ConfigureAwait(false);
    }
}
