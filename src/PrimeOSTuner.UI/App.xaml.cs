using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Core.History;
using PrimeOSTuner.Core.Lifecycle;
using PrimeOSTuner.Core.Monitoring;
using PrimeOSTuner.Core.Pipeline;
using PrimeOSTuner.Core.Profiles;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.UI.ViewModels;
using PrimeOSTuner.Win;
using PrimeOSTuner.Win.Network;
using PrimeOSTuner.Win.Steam;
using PrimeOSTuner.Win.SteamGridDb;
using Serilog;
using System.IO;
using System.Net.Http;
using System.Windows;

namespace PrimeOSTuner.UI;

public partial class App : Application
{
    public IHost Host { get; private set; } = null!;


    protected override async void OnStartup(StartupEventArgs e)
    {
        var logsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrimeOSTuner", "logs");
        Directory.CreateDirectory(logsDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(logsDir, "primeos-.log"), rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(s =>
            {
                // Win layer
                s.AddSingleton<IRegistryClient, RegistryClient>();
                s.AddSingleton<IProcessClient, ProcessClient>();
                s.AddSingleton<IPowerPlanClient, PowerPlanClient>();
                s.AddSingleton<IRestorePointClient, RestorePointClient>();
                s.AddSingleton<IHardwareClient, HardwareClient>();

                // Core
                s.AddSingleton(_ => new TweakHistory(TweakHistory.DefaultPath()));
                s.AddSingleton<SystemSampler>();
                s.AddSingleton<PowerPlanTweak>();
                s.AddSingleton<RamCleanerTweak>();

                // System cleanup tweaks (replaces the old junk-files / visual-effects pair)
                s.AddSingleton<DnsFlushTweak>();
                s.AddSingleton<WindowsUpdateCacheTweak>();
                s.AddSingleton<DriverHealthCheckTweak>();
                s.AddSingleton<DriverStoreCleanupTweak>();
                s.AddSingleton<SafeRegistryCleanupTweak>();

                // Win-layer additions
                s.AddSingleton<INetworkInterfaceClient, NetworkInterfaceClient>();
                s.AddSingleton<ITimerResolutionClient, TimerResolutionClient>();
                s.AddSingleton<ISteamLibraryScanner, SteamLibraryScanner>();
                s.AddSingleton(_ => SteamGridDbSettings.Load());
                s.AddHttpClient<ISteamGridDbClient, SteamGridDbClient>(c =>
                {
                    c.BaseAddress = new Uri("https://www.steamgriddb.com");
                    c.Timeout = TimeSpan.FromSeconds(20);
                });
                s.AddSingleton<ArtCache>(sp =>
                    new ArtCache(ArtCache.DefaultDir(),
                        sp.GetRequiredService<IHttpClientFactory>().CreateClient("art-download")));
                s.AddHttpClient("art-download");

                // Core additions — new tweaks
                s.AddSingleton<MouseAccelTweak>();
                s.AddSingleton<TimerResolutionTweak>();
                s.AddSingleton<GameModeTweak>();
                s.AddSingleton<HwGpuSchedulingTweak>();
                s.AddSingleton<NagleAlgorithmTweak>();
                s.AddSingleton<CpuCoreParkingTweak>();
                s.AddSingleton<Func<IEnumerable<string>, PerAppGpuPreferenceTweak>>(sp =>
                    paths => new PerAppGpuPreferenceTweak(sp.GetRequiredService<IRegistryClient>(), paths));

                // Registry-driven tweak catalog (data file → many ITweak instances)
                s.AddSingleton<IReadOnlyList<RegistryTweak>>(sp =>
                {
                    var registry = sp.GetRequiredService<IRegistryClient>();
                    var defs = RegistryTweakCatalog.LoadFromFile(RegistryTweakCatalog.DefaultPath());
                    return defs.Select(d => new RegistryTweak(d, registry)).ToList();
                });

                // Profiles
                s.AddSingleton(_ => new CustomProfileStore(CustomProfileStore.DefaultPath()));
                s.AddSingleton(_ => new ActiveTweaksStore(ActiveTweaksStore.DefaultPath()));
                s.AddSingleton<ProfileApplier>();

                // Games
                s.AddSingleton(_ => new AddedGamesStore(AddedGamesStore.DefaultPath()));
                s.AddSingleton<GameRegistry>();
                s.AddSingleton(_ => new GameProfileStore(GameProfileStore.DefaultPath()));

                // Lifecycle
                s.AddSingleton<IGameProcessWatcher>(sp =>
                {
                    var registry = sp.GetRequiredService<GameRegistry>();
                    return new GameProcessWatcher(
                        knownGamesProvider: () => registry.GetAllAsync(),
                        processSnapshotProvider: null,
                        pollIntervalMs: 2000);
                });
                s.AddSingleton<ProfileLifecycleService>(sp =>
                {
                    var custom = sp.GetRequiredService<CustomProfileStore>();
                    var customProfile = custom.LoadAsync().GetAwaiter().GetResult();
                    var dict = new Dictionary<string, ModeProfile>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["basic"] = BuiltInProfiles.Basic,
                        ["performance"] = BuiltInProfiles.Performance,
                        ["custom"] = customProfile,
                    };
                    return new ProfileLifecycleService(
                        sp.GetRequiredService<IGameProcessWatcher>(),
                        sp.GetRequiredService<GameProfileStore>(),
                        sp.GetRequiredService<ActiveTweaksStore>(),
                        dict,
                        sp.GetRequiredService<ProfileApplier>());
                });

                s.AddSingleton<IEnumerable<ITweak>>(sp =>
                {
                    var perAppFactory = sp.GetRequiredService<Func<IEnumerable<string>, PerAppGpuPreferenceTweak>>();
                    var registry = sp.GetRequiredService<GameRegistry>();
                    var gamePaths = registry.GetAllAsync().GetAwaiter().GetResult()
                        .Where(g => g.InstallPath is not null)
                        .Select(g => g.InstallPath!)
                        .ToList();
                    var custom = new ITweak[]
                    {
                        sp.GetRequiredService<PowerPlanTweak>(),
                        sp.GetRequiredService<RamCleanerTweak>(),
                        sp.GetRequiredService<DnsFlushTweak>(),
                        sp.GetRequiredService<WindowsUpdateCacheTweak>(),
                        sp.GetRequiredService<DriverHealthCheckTweak>(),
                        sp.GetRequiredService<DriverStoreCleanupTweak>(),
                        sp.GetRequiredService<SafeRegistryCleanupTweak>(),
                        sp.GetRequiredService<MouseAccelTweak>(),
                        sp.GetRequiredService<TimerResolutionTweak>(),
                        sp.GetRequiredService<GameModeTweak>(),
                        sp.GetRequiredService<HwGpuSchedulingTweak>(),
                        sp.GetRequiredService<NagleAlgorithmTweak>(),
                        sp.GetRequiredService<CpuCoreParkingTweak>(),
                        perAppFactory(gamePaths),
                    };
                    var catalog = sp.GetRequiredService<IReadOnlyList<RegistryTweak>>();
                    return custom.Concat(catalog).ToArray();
                });
                s.AddSingleton<OneClickOptimizer>();

                // ViewModels & MainWindow
                s.AddSingleton<ShellViewModel>();
                s.AddSingleton<DashboardViewModel>();
                s.AddTransient<HistoryViewModel>();
                s.AddTransient<Views.DashboardView>();
                s.AddTransient<Views.OptimizeView>();
                s.AddTransient<Views.HistoryView>();
                s.AddSingleton<GameLibraryViewModel>();
                s.AddTransient<Views.GameLibraryView>();
                s.AddTransient<Dialogs.AddGameDialog>();
                s.AddTransient<CustomModeViewModel>();
                s.AddTransient<Views.CustomModeView>();
                s.AddSingleton<GameBoostViewModel>();
                s.AddTransient<Views.GameBoostView>();
                s.AddSingleton<WatcherStatusViewModel>();

                // Settings
                s.AddSingleton(_ => new PrimeOSTuner.Core.Settings.AppSettingsStore(
                    PrimeOSTuner.Core.Settings.AppSettingsStore.DefaultPath()));
                s.AddSingleton<SettingsViewModel>();
                s.AddTransient<Views.SettingsView>();

                s.AddSingleton<Services.TrayIconService>();
                s.AddSingleton<MainWindow>();
            })
            .Build();

        Host.Start();

        var lifecycle = Host.Services.GetRequiredService<ProfileLifecycleService>();
        await lifecycle.RecoverFromCrashAsync();
        lifecycle.Start();

        // Tray icon — eager-init so the icon is in the system tray immediately.
        var tray = Host.Services.GetRequiredService<Services.TrayIconService>();
        var window = Host.Services.GetRequiredService<MainWindow>();
        var settings = Host.Services.GetRequiredService<SettingsViewModel>();

        tray.ShowRequested += (_, _) =>
        {
            window.Show();
            window.WindowState = WindowState.Normal;
            window.Activate();
        };
        tray.OptimizeRequested += async (_, _) =>
        {
            try
            {
                var report = await Host.Services.GetRequiredService<PrimeOSTuner.Core.Pipeline.OneClickOptimizer>().RunAsync();
                if (settings.NotificationsEnabled)
                    tray.ShowNotification("PrimeOS Tuner",
                        $"Optimization complete — {report.SuccessCount} succeeded, {report.FailureCount} failed.");
            }
            catch (Exception ex) { Log.Error(ex, "Tray Optimize Now failed"); }
        };
        tray.ExitRequested += (_, _) =>
        {
            _exitingForReal = true;
            Shutdown();
        };

        if (settings.StartMinimized)
        {
            // Don't Show() — only the tray icon is visible until the user clicks Show.
        }
        else
        {
            window.Show();
        }

        base.OnStartup(e);
    }

    private bool _exitingForReal;
    public bool IsExitingForReal => _exitingForReal;
    public void RequestRealExit() { _exitingForReal = true; Shutdown(); }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        Host?.StopAsync().Wait(TimeSpan.FromSeconds(5));
        Host?.Dispose();
        base.OnExit(e);
    }
}
