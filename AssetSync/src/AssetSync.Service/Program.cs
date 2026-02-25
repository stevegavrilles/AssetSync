using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AssetSync.Service;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // TODO: Register Core and Infrastructure services (connection string from config/env)
        // builder.Services.AddSingleton<ISyncEngine, SyncEngine>();
        // builder.Services.AddHostedService<SyncWorker>();

        await builder.Build().RunAsync().ConfigureAwait(false);
    }
}
