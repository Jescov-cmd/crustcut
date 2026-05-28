using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PrimeOSTuner.Core.Bloatware;
using PrimeOSTuner.Core.Education;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Core.History;
using PrimeOSTuner.Core.Lifecycle;
using PrimeOSTuner.Core.Memory;
using PrimeOSTuner.Core.Monitoring;
using PrimeOSTuner.Core.Performance;
using PrimeOSTuner.Core.Pipeline;
using PrimeOSTuner.Core.Profiles;
using PrimeOSTuner.Core.Sentinel;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.UI.ViewModels;
using PrimeOSTuner.Win;
using PrimeOSTuner.Win.Network;
using PrimeOSTuner.Win.Steam;
using PrimeOSTuner.Win.SteamGridDb;
using PrimeOSTuner.Win.Suspension;
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

        // Catch crashes so they end up in the log file (and the user gets a friendly popup
        // instead of the app vanishing). Without these handlers, the runtime kills the
        // process before Serilog gets a chance to write anything.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                Log.Fatal(ex, "AppDomain unhandled exception (terminating={Term})", args.IsTerminating);
            else
                Log.Fatal("AppDomain unhandled non-Exception: {Obj}", args.ExceptionObject);
            Log.CloseAndFlush();
        };
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Fatal(args.Exception, "Dispatcher unhandled exception");
            MessageBox.Show(
                $"Something went wrong:\n\n{args.Exception.GetType().Name}: {args.Exception.Message}\n\n" +
                $"The full error has been logged. Click OK to keep the app running.",
                "PrimeOS Tuner — Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };

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
                s.AddSingleton<IServiceClient, ServiceClient>();

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
                s.AddSingleton<ShaderCacheCleanupTweak>();

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
                s.AddSingleton<SteamCdnCoverFetcher>(sp =>
                    new SteamCdnCoverFetcher(sp.GetRequiredService<ArtCache>()));

                // Core additions — new tweaks
                s.AddSingleton<MouseAccelTweak>();
                s.AddSingleton<TelemetryDisableTweak>();
                s.AddSingleton<TimerResolutionTweak>();
                s.AddSingleton<GameModeTweak>();
                s.AddSingleton<HwGpuSchedulingTweak>();
                s.AddSingleton<CpuCoreParkingTweak>();
                s.AddSingleton<CortanaDisableTweak>();
                s.AddSingleton<UltimatePerformanceTweak>();
                s.AddSingleton<HibernationTweak>();
                s.AddSingleton<VisualEffectsTweak>();
                s.AddSingleton<MmcssGamesPriorityTweak>();
                s.AddSingleton<SnappyUiTweak>();
                s.AddSingleton<WidgetsDisableTweak>();
                s.AddSingleton<StickyKeysTweak>();
                s.AddSingleton<VbsHvciTweak>();
                s.AddSingleton<DefenderGameExclusionsTweak>(sp =>
                {
                    var registry = sp.GetRequiredService<GameRegistry>();
                    return new DefenderGameExclusionsTweak(() =>
                        registry.GetAllAsync().GetAwaiter().GetResult()
                            .Select(g => g.InstallPath)
                            .Where(p => !string.IsNullOrWhiteSpace(p))
                            .Select(p => p!)
                            .Distinct()
                            .ToList());
                });
                s.AddSingleton<Func<IEnumerable<string>, PerAppGpuPreferenceTweak>>(sp =>
                    paths => new PerAppGpuPreferenceTweak(sp.GetRequiredService<IRegistryClient>(), paths));

                // Registry-driven tweak catalog (data file → many ITweak instances)
                s.AddSingleton<IReadOnlyList<RegistryTweak>>(sp =>
                {
                    var registry = sp.GetRequiredService<IRegistryClient>();
                    var defs = RegistryTweakCatalog.LoadFromFile(RegistryTweakCatalog.DefaultPath());
                    return defs.Select(d => new RegistryTweak(d, registry)).ToList();
                });

                // Bloatware
                s.AddSingleton<IAppxClient, AppxClient>();
                s.AddSingleton<IReadOnlyList<BloatwareCatalogEntry>>(_ =>
                    BloatwareCatalog.LoadFromFile(BloatwareCatalog.DefaultPath()));
                s.AddSingleton<BloatwareDetector>();
                s.AddSingleton<BloatwareDisableService>();
                s.AddSingleton<BloatwareUninstallService>();

                // Memory Priority
                s.AddSingleton<IPriorityClient, PriorityClient>();
                s.AddSingleton<IProcessWatcher, WmiProcessWatcher>();
                s.AddSingleton<IWorkingSetTrimmer, WorkingSetTrimmer>();
                s.AddSingleton<SafeRamCleaner>();
                s.AddSingleton<IGameBooster, GameBooster>();
                s.AddSingleton<PriorityRuleStore>(_ => new PriorityRuleStore(PriorityRuleStore.DefaultPath()));
                s.AddSingleton<PriorityRuleEngine>();
                s.AddSingleton<IRamCleanerProtectList>(sp =>
                    new PrimeOSTuner.UI.Services.StoreBackedProtectList(sp.GetRequiredService<PriorityRuleStore>()));

                // Profiles
                s.AddSingleton(_ => new CustomProfileStore(CustomProfileStore.DefaultPath()));
                s.AddSingleton(_ => new ActiveTweaksStore(ActiveTweaksStore.DefaultPath()));
                s.AddSingleton<ProfileApplier>();

                // Games
                s.AddSingleton(_ => new AddedGamesStore(AddedGamesStore.DefaultPath()));
                s.AddSingleton<GameRegistry>();
                s.AddSingleton(_ => new GameProfileStore(GameProfileStore.DefaultPath()));

                // Background suspender (foundation in Win.Suspension)
                s.AddSingleton<IProcessSuspender, NtProcessSuspender>();
                s.AddSingleton<IBackgroundSuspenderService>(sp =>
                    new BackgroundSuspenderService(sp.GetRequiredService<IProcessSuspender>()));

                // Sentinel — passive performance watcher
                s.AddSingleton<IMetricsSampler>(_ => new GpuPerfCounterMetricsSampler());
                s.AddHttpClient<ISpecFetcher, SteamSpecFetcher>(c =>
                {
                    c.BaseAddress = new Uri("https://store.steampowered.com");
                    c.Timeout = TimeSpan.FromSeconds(15);
                });
                s.AddSingleton<ISentinelService>(sp =>
                    new SentinelService(
                        sp.GetRequiredService<ISpecFetcher>(),
                        sp.GetRequiredService<IMetricsSampler>()));
                s.AddSingleton<SentinelViewModel>();
                s.AddTransient<Views.SentinelView>();

                // PresentMon frame-time recording
                var presentMonBinaryPath = Path.Combine(
                    AppContext.BaseDirectory, "Assets", "PresentMon", "PresentMon-x64.exe");
                var framesDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PrimeOSTuner", "frames");

                s.AddSingleton<IPresentMonRunner>(_ => new PresentMonRunner(presentMonBinaryPath));
                s.AddSingleton<IFrameSessionStore>(_ => new FrameSessionStore(FrameSessionStore.DefaultPath()));
                s.AddSingleton<FrameRecordingService>(sp =>
                    new FrameRecordingService(
                        sp.GetRequiredService<IPresentMonRunner>(),
                        sp.GetRequiredService<IFrameSessionStore>(),
                        framesDir));

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
                        ["aggressive"] = BuiltInProfiles.Aggressive,
                        ["custom"] = customProfile,
                    };
                    return new ProfileLifecycleService(
                        sp.GetRequiredService<IGameProcessWatcher>(),
                        sp.GetRequiredService<GameProfileStore>(),
                        sp.GetRequiredService<ActiveTweaksStore>(),
                        dict,
                        sp.GetRequiredService<ProfileApplier>(),
                        sp.GetRequiredService<IBackgroundSuspenderService>(),
                        sp.GetRequiredService<ISentinelService>(),
                        sp.GetRequiredService<FrameRecordingService>());
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
                        sp.GetRequiredService<DnsFlushTweak>(),
                        sp.GetRequiredService<WindowsUpdateCacheTweak>(),
                        sp.GetRequiredService<DriverHealthCheckTweak>(),
                        sp.GetRequiredService<DriverStoreCleanupTweak>(),
                        sp.GetRequiredService<ShaderCacheCleanupTweak>(),
                        sp.GetRequiredService<MouseAccelTweak>(),
                        sp.GetRequiredService<TimerResolutionTweak>(),
                        sp.GetRequiredService<GameModeTweak>(),
                        sp.GetRequiredService<HwGpuSchedulingTweak>(),
                        sp.GetRequiredService<CpuCoreParkingTweak>(),
                        sp.GetRequiredService<TelemetryDisableTweak>(),
                        sp.GetRequiredService<CortanaDisableTweak>(),
                        sp.GetRequiredService<UltimatePerformanceTweak>(),
                        sp.GetRequiredService<HibernationTweak>(),
                        sp.GetRequiredService<VisualEffectsTweak>(),
                        sp.GetRequiredService<MmcssGamesPriorityTweak>(),
                        sp.GetRequiredService<SnappyUiTweak>(),
                        sp.GetRequiredService<WidgetsDisableTweak>(),
                        sp.GetRequiredService<StickyKeysTweak>(),
                        sp.GetRequiredService<VbsHvciTweak>(),
                        sp.GetRequiredService<DefenderGameExclusionsTweak>(),
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

                s.AddSingleton<GameBoostViewModel>();
                s.AddTransient<Views.GameBoostView>();
                s.AddSingleton<WatcherStatusViewModel>();

                // Settings
                s.AddSingleton(_ => new PrimeOSTuner.Core.Settings.AppSettingsStore(
                    PrimeOSTuner.Core.Settings.AppSettingsStore.DefaultPath()));
                s.AddSingleton<SettingsViewModel>();
                s.AddTransient<Views.SettingsView>();
                s.AddSingleton<BloatwareViewModel>();
                s.AddTransient<Views.BloatwareView>();
                s.AddSingleton<MemoryPriorityViewModel>();
                s.AddTransient<Views.MemoryPriorityView>();
                s.AddTransient<Views.MaintenanceView>();

                // Optimization 101 — educational guides
                s.AddSingleton<IReadOnlyList<Guide>>(_ =>
                {
                    try { return GuideCatalog.LoadFromDirectory(GuideCatalog.DefaultDirectory()); }
                    catch { return Array.Empty<Guide>(); }
                });
                s.AddSingleton(_ => new GuideCompletionStore(GuideCompletionStore.DefaultPath()));
                s.AddSingleton<Optimization101ViewModel>();
                s.AddTransient<Views.Optimization101View>();

                s.AddSingleton<Services.TrayIconService>();
                s.AddSingleton<MainWindow>();
            })
            .Build();

        CustomModeMigration.RunIfNeeded();
        Host.Start();

        TryCleanupOrphanFrameCsvs();

        var priorityEngine = Host.Services.GetRequiredService<PriorityRuleEngine>();
        var priorityVm = Host.Services.GetRequiredService<MemoryPriorityViewModel>();
        await priorityVm.LoadAsync();   // populates rules + reloads engine
        priorityEngine.Start();

        var lifecycle = Host.Services.GetRequiredService<ProfileLifecycleService>();
        await lifecycle.RecoverFromCrashAsync();
        lifecycle.Start();

        // Tray icon — eager-init so the icon is in the system tray immediately.
        var tray = Host.Services.GetRequiredService<Services.TrayIconService>();
        var window = Host.Services.GetRequiredService<MainWindow>();
        var settings = Host.Services.GetRequiredService<SettingsViewModel>();
        var sentinelSvc = Host.Services.GetRequiredService<ISentinelService>();
        sentinelSvc.Enabled = settings.SentinelEnabled;

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

    private static void TryCleanupOrphanFrameCsvs()
    {
        try
        {
            var framesDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PrimeOSTuner", "frames");
            if (!Directory.Exists(framesDir)) return;

            var cutoff = DateTime.UtcNow.AddHours(-24);
            foreach (var file in Directory.EnumerateFiles(framesDir, "*.csv"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoff) File.Delete(file);
                }
                catch { /* best effort */ }
            }
        }
        catch { /* never block app startup */ }
    }
}
