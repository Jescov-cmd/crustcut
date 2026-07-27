using Crustcut.App.Services;
using Crustcut.Presentation;
using PrimeOSTuner.Core.Bloatware;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Core.History;
using PrimeOSTuner.Core.Memory;
using PrimeOSTuner.Core.Monitoring;
using PrimeOSTuner.Core.Performance;
using PrimeOSTuner.Core.Profiles;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using PrimeOSTuner.Win.Launchers;
using PrimeOSTuner.Win.Steam;
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
        Overview = new OverviewViewModel(
            new SystemSampler(new HardwareClient()),
            new ActiveTweaksStore(ActiveTweaksStore.DefaultPath()),
            Tweaks,
            new FrameSessionStore(FrameSessionStore.DefaultPath()),
            ui);

        // ── Optimize ──────────────────────────────────────────────────────────────────
        Optimize = new OptimizeViewModel(
            Tweaks,
            new TweakHistory(TweakHistory.DefaultPath()),
            new SessionTweakStore(SessionTweakStore.DefaultPath()),
            new PendingUndoStore(PendingUndoStore.DefaultPath()),
            dialogs);

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
        var booster = new GameBooster(new SafeRamCleaner(new WorkingSetTrimmer()));
        var engine = new PriorityRuleEngine(new WmiProcessWatcher(), new PriorityClient(), booster);

        var games = new GameRegistry(
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

        Memory = new MemoryPriorityViewModel(priorityStore, engine, games);
    }
}
