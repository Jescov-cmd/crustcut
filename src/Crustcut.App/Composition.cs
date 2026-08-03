using Crustcut.App.Services;
using Crustcut.Presentation;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Core.History;
using PrimeOSTuner.Core.Memory;
using PrimeOSTuner.Core.Monitoring;
using PrimeOSTuner.Core.Performance;
using PrimeOSTuner.Core.Pipeline;
using PrimeOSTuner.Core.Profiles;
using PrimeOSTuner.Core.Settings;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using PrimeOSTuner.Win.Launchers;
using PrimeOSTuner.Win.Steam;
using PrimeOSTuner.Win.SteamGridDb;
using PrimeOSTuner.Win.Xbox;
#if WINDOWS
using PrimeOSTuner.Core.Bloatware;
using PrimeOSTuner.Core.Diagnosis;
using PrimeOSTuner.Core.Lifecycle;
using PrimeOSTuner.Core.Sentinel;
using PrimeOSTuner.Win.Suspension;
#else
using PrimeOSTuner.Mac;
#endif

namespace Crustcut.App;

/// <summary>
/// Builds the object graph by hand. A container buys little here — the graph is shallow,
/// explicit construction keeps startup cost visible. On Windows this is the full product;
/// on macOS only the platform-neutral subsystems are constructed (Overview monitoring,
/// Games library, Guides, Settings) and the rest stay null with their tabs hidden by
/// <see cref="Crustcut.Presentation.Navigation.NavCatalog"/>.
/// </summary>
public sealed class Composition
{
    public IReadOnlyList<ITweak> Tweaks { get; }
    public OverviewViewModel Overview { get; }
    public OptimizeViewModel? Optimize { get; }
    public CleanupViewModel? Cleanup { get; }
    public MemoryPriorityViewModel? Memory { get; }
    public DiagnosisViewModel? Diagnosis { get; }
    public HistoryViewModel? History { get; }
    public SettingsViewModel Settings { get; }
    public GamesViewModel Games { get; }
    public SessionsViewModel? Sessions { get; }
    public GameBoostViewModel? GameBoost { get; }
    public GuidesViewModel Guides { get; }
    public OverlayService? Overlay { get; }
    public BackgroundEngine? Engine { get; }
    public AppRegistrationService Registration { get; }

#if WINDOWS
    public Composition()
    {
        var dialogs = new AvaloniaDialogService();
        var ui = new AvaloniaDispatcher();
        var settingsStore = new AppSettingsStore(AppSettingsStore.DefaultPath());
        var history = new TweakHistory(TweakHistory.DefaultPath());

        // ── Games (needed early: tweaks and the watcher key off the library) ──────────
        var gameRegistry = new GameRegistry(
            new SteamLibraryScanner(),
            new XboxLibraryScanner(),
            new IExternalGameScanner[]
            {
                new EpicGameScanner(),
                new UbisoftGameScanner(),
                new EaGameScanner(),
                new GogGameScanner(),
            },
            new AddedGamesStore(AddedGamesStore.DefaultPath()));

        // Game install paths, resolved once on first use and never during startup — the
        // library scan hits Steam/Xbox/Epic/Ubisoft/EA/GOG and takes seconds.
        var lazyGamePaths = new Lazy<IReadOnlyList<string>>(() =>
        {
            try
            {
                return gameRegistry.GetAllAsync().GetAwaiter().GetResult()
                    .Where(g => g.InstallPath is not null)
                    .Select(g => g.InstallPath!)
                    .ToList();
            }
            catch { return Array.Empty<string>(); }
        });

        // ── Memory cleaner (built early: it joins the tweak list as a one-shot action) ─
        var priorityStore = new PriorityRuleStore(PriorityRuleStore.DefaultPath());
        var priorityClient = new PriorityClient();
        var protectList = new StorePriorityProtectList(priorityStore);
        var trimmer = new WorkingSetTrimmer();
        var ramCleaner = new SafeRamCleaner(trimmer);
        // Clean our own house first: before any cleanup pass, Crustcut compacts its own
        // heap and trims its own working set. A memory optimizer idling at 150+ MB
        // undermines everything it claims.
        Action selfTrim = () =>
        {
            GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
            trimmer.TrimWorkingSet(Environment.ProcessId);
        };
        var ramTweak = new RamCleanerTweak(ramCleaner, protectList, priorityClient);
        var deepRamTweak = new RamCleanerTweak(ramCleaner, protectList, priorityClient, RamCleanMode.Deep);
        var standbyClient = new StandbyListClient();

        // ── Tweaks: full set — hand-written + registry-driven catalog ─────────────────
        var registryClient = new RegistryClient();
        var powerClient = new PowerPlanClient();
        var serviceClient = new ServiceClient();

        var handWritten = new ITweak[]
        {
            new PowerPlanTweak(powerClient),
            new DnsFlushTweak(),
            new WindowsUpdateCacheTweak(),
            new DriverHealthCheckTweak(),
            new DriverStoreCleanupTweak(),
            new ShaderCacheCleanupTweak(),
            new MouseAccelTweak(registryClient),
            new TimerResolutionTweak(new TimerResolutionClient()),
            new GameModeTweak(registryClient),
            new HwGpuSchedulingTweak(registryClient),
            new CpuCoreParkingTweak(powerClient),
            new TelemetryDisableTweak(registryClient, serviceClient),
            new CortanaDisableTweak(registryClient),
            new UltimatePerformanceTweak(powerClient),
            new HibernationTweak(registryClient, powerClient),
            new VisualEffectsTweak(registryClient),
            new MmcssGamesPriorityTweak(registryClient),
            new SnappyUiTweak(registryClient),
            new WidgetsDisableTweak(registryClient),
            new StickyKeysTweak(registryClient),
            new VbsHvciTweak(registryClient),
            new DefenderGameExclusionsTweak(() => lazyGamePaths.Value),
            new PerAppGpuPreferenceTweak(registryClient, new DeferredEnumerable<string>(() => lazyGamePaths.Value)),
            new TcpGamingLatencyTweak(),
            new StandbyPurgeTweak(standbyClient),
        };

        var catalog = RegistryTweakCatalog
            .LoadFromFile(RegistryTweakCatalog.DefaultPath())
            .Select(d => (ITweak)new RegistryTweak(d, registryClient));

        // The RAM cleaners ride along as one-shot actions: RUN buttons on Optimize,
        // excluded from the boost score and the one-click bundle by IOneShotTweak.
        Tweaks = handWritten.Concat(catalog).Append(ramTweak).Append(deepRamTweak).ToList();

        var oneClick = new OneClickOptimizer(Tweaks, history);
        var applier = new ProfileApplier(Tweaks, history);
        var suspender = new BackgroundSuspenderService(new NtProcessSuspender());

        // ── Monitoring + frame recording (shared instances) ───────────────────────────
        // One sampler feeds Overview, the overlay and the auto-RAM threshold; one frame
        // store feeds the recorder, Overview and Sessions — the store raises Updated, so
        // every consumer must share the instance that the recorder saves through.
        var sampler = new SystemSampler(new HardwareClient());
        var frameStore = new FrameSessionStore(FrameSessionStore.DefaultPath());

        var presentMonPath = Path.Combine(
            AppContext.BaseDirectory, "Assets", "PresentMon", "PresentMon-x64.exe");
        var framesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrimeOSTuner", "frames");

        var frames = new FrameRecordingService(new PresentMonRunner(presentMonPath), frameStore, framesDir);

        // ── Pages ─────────────────────────────────────────────────────────────────────
        Overview = new OverviewViewModel(
            sampler, new ActiveTweaksStore(ActiveTweaksStore.DefaultPath()),
            Tweaks, frameStore, ui, oneClick);

        Optimize = new OptimizeViewModel(
            Tweaks, history,
            new SessionTweakStore(SessionTweakStore.DefaultPath()),
            new PendingUndoStore(PendingUndoStore.DefaultPath()),
            dialogs);

        History = new HistoryViewModel(history);
        Diagnosis = new DiagnosisViewModel(new DiagnosisService(new DiagnosisProbes()));
        Overlay = new OverlayService(new OverlayViewModel(sampler, frames, ui), settingsStore);

        Registration = new AppRegistrationService();
        Settings = new SettingsViewModel(settingsStore, Registration, Overlay);

        var appx = new AppxClient();
        Cleanup = new CleanupViewModel(
            new BloatwareDetector(appx, BloatwareCatalog.LoadFromFile(BloatwareCatalog.DefaultPath())),
            new InstalledProgramsClient(),
            DesktopBloatwareCatalog.LoadFromFile(DesktopBloatwareCatalog.DefaultPath()),
            new BloatwareUninstallService(appx),
            new BloatwareDisableService(serviceClient),
            dialogs);

        // ── Memory ────────────────────────────────────────────────────────────────────
        var booster = new GameBooster(ramCleaner);
        var engine = new PriorityRuleEngine(new WmiProcessWatcher(), priorityClient, booster);

        // Migrate the legacy BelowNormal defaults FIRST (old builds bulk-deprioritised
        // every scanned app, VS Code included), then feed the engine. Start() needs admin
        // for WMI process events — guarded so a non-elevated run degrades to no
        // enforcement instead of crashing.
        var migrationMarker = Path.Combine(
            Path.GetDirectoryName(PriorityRuleStore.DefaultPath())!, "priority-rules.migrated-v08");
        _ = Task.Run(async () =>
        {
            await PriorityRuleMigrations.RunOnceAsync(priorityStore, migrationMarker);
            try { await engine.ReloadAsync(await priorityStore.LoadAsync()); } catch { }
        });
        try { engine.Start(); } catch { /* WMI denied when not elevated */ }

        Memory = new MemoryPriorityViewModel(priorityStore, engine, gameRegistry, priorityClient, ramTweak, deepRamTweak);

        // ── Game watcher + lifecycle inputs (lifecycle itself starts in the engine) ───
        var watcher = new GameProcessWatcher(
            knownGamesProvider: () => gameRegistry.GetAllAsync(),
            processSnapshotProvider: null,
            pollIntervalMs: 2000);

        var specsHttp = new HttpClient
        {
            BaseAddress = new Uri("https://store.steampowered.com"),
            Timeout = TimeSpan.FromSeconds(15),
        };
        var sentinel = new SentinelService(new SteamSpecFetcher(specsHttp), new GpuPerfCounterMetricsSampler());

        // ── Art / lookup clients ──────────────────────────────────────────────────────
        // One HttpClient for every art/lookup client — one per client is the classic
        // socket-exhaustion mistake.
        var http = new HttpClient();
        var artCache = new ArtCache(ArtCache.DefaultDir(), http);

        Games = new GamesViewModel(
            gameRegistry,
            new GameProfileStore(GameProfileStore.DefaultPath()),
            new SteamGridDbClient(http, SteamGridDbSettings.Load()),
            artCache,
            new SteamCdnCoverFetcher(artCache),
            new SteamAppLookup(http),
            ui);

        Sessions = new SessionsViewModel(frameStore, sentinel, settingsStore, ui);
        GameBoost = new GameBoostViewModel(applier, settingsStore, suspender, ui);
        Guides = new GuidesViewModel();

        // ── Background engine: everything that must run without any tab open ──────────
        Engine = new BackgroundEngine(
            watcher, sentinel, frames, Overlay, settingsStore,
            Tweaks, ramTweak, sampler, ui, applier, suspender,
            new ActiveTweaksStore(ActiveTweaksStore.DefaultPath()),
            new GameProfileStore(GameProfileStore.DefaultPath()),
            new SessionTweakStore(SessionTweakStore.DefaultPath()),
            standbyClient, deepRamTweak, selfTrim,
            priorityStore, engine, priorityClient);
    }
#else
    /// <summary>
    /// macOS graph — UNTESTED ON REAL HARDWARE. Monitoring comes from
    /// <see cref="MacHardwareClient"/>, the game library from Steam's on-disk manifests.
    /// No tweak surface exists on macOS, so Tweaks is empty and the boost score reports
    /// "no tweaks available" honestly.
    /// </summary>
    public Composition()
    {
        var ui = new AvaloniaDispatcher();
        var settingsStore = new AppSettingsStore(AppSettingsStore.DefaultPath());

        var gameRegistry = new GameRegistry(
            new MacSteamLibraryScanner(),
            new NullXboxLibraryScanner(),
            Array.Empty<IExternalGameScanner>(),
            new AddedGamesStore(AddedGamesStore.DefaultPath()));

        Tweaks = Array.Empty<ITweak>();

        var sampler = new SystemSampler(new MacHardwareClient());
        var frameStore = new FrameSessionStore(FrameSessionStore.DefaultPath());

        Overview = new OverviewViewModel(
            sampler, new ActiveTweaksStore(ActiveTweaksStore.DefaultPath()),
            Tweaks, frameStore, ui);

        Registration = new AppRegistrationService();
        Settings = new SettingsViewModel(settingsStore, Registration, Overlay);

        var http = new HttpClient();
        var artCache = new ArtCache(ArtCache.DefaultDir(), http);
        Games = new GamesViewModel(
            gameRegistry,
            new GameProfileStore(GameProfileStore.DefaultPath()),
            new SteamGridDbClient(http, SteamGridDbSettings.Load()),
            artCache,
            new SteamCdnCoverFetcher(artCache),
            new SteamAppLookup(http),
            ui);

        Guides = new GuidesViewModel();
    }
#endif
}

/// <summary>Enumerable that resolves its source on first enumeration, not at construction.</summary>
public sealed class DeferredEnumerable<T> : IEnumerable<T>
{
    private readonly Func<IReadOnlyList<T>> _source;
    public DeferredEnumerable(Func<IReadOnlyList<T>> source) => _source = source;
    public IEnumerator<T> GetEnumerator() => _source().GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>RAM-cleaner protect list backed by the Memory tab's priority rules.</summary>
public sealed class StorePriorityProtectList : IRamCleanerProtectList
{
    private readonly PriorityRuleStore _store;
    public StorePriorityProtectList(PriorityRuleStore store) => _store = store;

    public IReadOnlyList<string> Get() =>
        _store.LoadAsync().GetAwaiter().GetResult()
            .Where(r => r.ProtectFromRamCleanup)
            .Select(r => r.ExePath)
            .ToList();
}
