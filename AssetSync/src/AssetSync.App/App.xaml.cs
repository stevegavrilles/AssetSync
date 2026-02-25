using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AssetSync.App.ViewModels;
using AssetSync.Core.Interfaces;
using AssetSync.Infrastructure.Data;
using AssetSync.Infrastructure.Security;

namespace AssetSync.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AssetSync", "assetsync.db");
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var connectionString = $"Data Source={dbPath}";
        var initializer = new DatabaseInitializer(connectionString);
        initializer.Initialize();

        var services = new ServiceCollection();
        services.AddSingleton(_ => connectionString);
        services.AddSingleton<ICredentialStore>(sp => new DpapiCredentialStore(sp.GetRequiredService<string>()));
        services.AddSingleton<ILogRepository>(sp => new SqliteLogRepository(sp.GetRequiredService<string>()));
        services.AddSingleton<IConfigRepository>(sp => new SqliteConfigRepository(sp.GetRequiredService<string>()));
        services.AddSingleton<IMappingRepository>(sp => new SqliteMappingRepository(sp.GetRequiredService<string>()));
        services.AddHttpClient();
        services.AddLogging(builder => builder.AddDebug().SetMinimumLevel(LogLevel.Debug));
        services.AddTransient<MainViewModel>();

        Services = services.BuildServiceProvider();
    }
}
