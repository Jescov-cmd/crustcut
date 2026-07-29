using Crustcut.App.Services;
using Crustcut.Presentation;
using PrimeOSTuner.Core.Bloatware;
using PrimeOSTuner.Core.Diagnosis;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Core.History;
using PrimeOSTuner.Core.Memory;
using PrimeOSTuner.Core.Monitoring;
using PrimeOSTuner.Core.Performance;
using PrimeOSTuner.Core.Profiles;
using PrimeOSTuner.Core.Settings;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using PrimeOSTuner.Win.Launchers;
using PrimeOSTuner.Win.Steam;
using PrimeOSTuner.Win.SteamGridDb;
using PrimeOSTuner.Win.Xbox;

namespace Crustcut.App;

/// <summary>
/// Builds the object graph by hand. A container buys little here — the graph is shallow,
/// explicit construction keeps startup cost visible, and it avoids a second DI setup while
/// the WPF app still owns the original one.
/// </summary>
public sealed class Composition
{
    public IReadOnlyList<ITweak> Tweaks { get; }
    public OverviewViewModel Overview { get; }
    public OptimizeViewModel Optimize { get; }
    public CleanupViewModel Cleanup { get; }
    public MemoryPriorityViewModel Memory { get; }
    public DiagnosisViewModel Diagnosis { get; }
    public HistoryViewModel History { get; }
    public SettingsViewModel Settings { get; }
    public GamesViewModel Games { get; }
    public OverlayService Overlay { get; }

    public Composition()
    {
        var dialogs = new AvaloniaDialogService();
        var ui = new AvaloniaDispatcher();

        // ── Tweaks (registry-driven catalog only for now; the hand-written ones arrive
        //    with the pages that need them) ──────────────────────────────────────────────
        var registryClient = new RegistryClient();
        Tweaks = RegistryTweakCatalog
            .LoadFromFile(RegistryTweakCatalog.DefaultPath())
            .Select(d => (ITweak)new RegistryTweak(d, registryClient))
            .ToList();

        // ── Overview ──────────────────────────────────────────────────────────────────
        // One sampler feeds both the Overview page and the overlay; two would mean two sets
        // of performance counters polling the same hardware.
        var sampler = new SystemSampler(new HardwareClient());

        Overview = new OverviewViewModel(
            sampler,
            new ActiveTweaksStore(ActiveTweaksStore.DefaultPath()),
            Tweaks,
            new FrameSessionStore(FrameSessionStore.DefaultPath()),
            ui);

        // ── Optimize + History (share one history store) ───────────────────────────────
        var history = new TweakHistory(TweakHistory.DefaultPath());

        Optimize = new OptimizeViewModel(
            Tweaks,
            history,
            new SessionTweakStore(SessionTweakStore.DefaultPath()),
            new PendingUndoStore(PendingUndoStore.DefaultPath()),
            dialogs);

        History = new HistoryViewModel(history);

        // ── Diagnosis ─────────────────────────────────────────────────────────────────
        Diagnosis = new DiagnosisViewModel(new DiagnosisService(new DiagnosisProbes()));

        // ── Settings ──────────────────────────────────────────────────────────────────
        var settingsStore = new AppSettingsStore(AppSettingsStore.DefaultPath());
        Settings = new SettingsViewModel(settingsStore);

        // ── Overlay ───────────────────────────────────────────────────────────────────
        var presentMonPath = Path.Combine(
            AppContext.BaseDirectory, "Assets", "PresentMon", "PresentMon-x64.exe");
        var framesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrimeOSTuner", "frames");

        var frames = new FrameRecordingService(
            new PresentMonRunner(presentMonPath),
            new FrameSessionStore(FrameSessionStore.DefaultPath()),
            framesDir);

        Overlay = new OverlayService(new OverlayViewModel(sampler, frames, ui), settingsStore);

        // ── Cleanup ───────────────────────────────────────────────────────────────────
        var appx = new AppxClient();
        Cleanup = new CleanupViewModel(
            new BloatwareDetector(appx, BloatwareCatalog.LoadFromFile(BloatwareCatalog.DefaultPath())),
            new InstalledProgramsClient(),
            DesktopBloatwareCatalog.LoadFromFile(DesktopBloatwareCatalog.DefaultPath()),
            new BloatwareUninstallService(appx),
            new BloatwareDisableService(new ServiceClient()),
            dialogs);

        // ── Memory ────────────────────────────────────────────────────────────────────
        var priorityStore = new PriorityRuleStore(PriorityRuleStore.DefaultPath());
        var priorityClient = new PriorityClient();
        var booster = new GameBooster(new SafeRamCleaner(new WorkingSetTrimmer()));
        var engine = new PriorityRuleEngine(new WmiProcessWatcher(), priorityClient, booster);

        // The engine previously only got rules when the Memory tab was first opened, so on
        // most launches it enforced nothing. Migrate the legacy BelowNormal defaults FIRST
        // (old builds bulk-deprioritised every scanned app, VS Code included), then feed the
        // engine. Start() needs admin for WMI process events — guarded so a non-elevated
        // run degrades to no enforcement instead of crashing.
        var migrationMarker = Path.Combine(
            Path.GetDirectoryName(PriorityRuleStore.DefaultPath())!, "priority-rules.migrated-v08");
        _ = Task.Run(async () =>
        {
            await PriorityRuleMigrations.RunOnceAsync(priorityStore, migrationMarker);
            try { await engine.ReloadAsync(await priorityStore.LoadAsync()); } catch { }
        });
        try { engine.Start(); } catch { /* WMI denied when not elevated */ }

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

        Memory = new MemoryPriorityViewModel(priorityStore, engine, gameRegistry, priorityClient);

        // ── Games ─────────────────────────────────────────────────────────────────────
        // One HttpClient for every art/lookup client — creating one per client is the
        // classic socket-exhaustion mistake.
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
    }
}
