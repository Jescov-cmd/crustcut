using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Crustcut.App.Services;
using Crustcut.Presentation;
using PrimeOSTuner.Core.Settings;

namespace Crustcut.App;

public partial class App : Application
{
    private Composition? _composition;
    private SingleInstanceGuard? _guard;
    private MainWindow? _window;
    private readonly ShellViewModel _shell = new();
    private bool _exiting;
    // --navstress measures the close-to-tray footprint, so its window close must always
    // go to the tray whatever the user's setting says — otherwise the run just exits.
    private bool _alwaysCloseToTray;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var (shotPath, shotTab, shotW, shotH) = SelfScreenshot.Parse(Program.Args);
            var overlayShotPath = SelfScreenshot.ParseOverlayShot(Program.Args);
            var navStressPath = NavStress.Parse(Program.Args);

            // The panic button: revert every applied optimization and disable every
            // automatic behaviour, then exit. For "is Crustcut causing my problem?"
            // A/B tests and for support — one command returns the machine to stock.
            if (Array.IndexOf(Program.Args, "--revert-all") >= 0)
            {
                _ = RunRevertAllThenExitAsync();
                base.OnFrameworkInitializationCompleted();
                return;
            }

            // The screenshot run is a throwaway process: skip the single-instance guard so
            // it can run while the real app is open.
            _guard = new SingleInstanceGuard();
            var isDebugCapture = shotPath is not null || navStressPath is not null
                              || overlayShotPath is not null;
            if (!isDebugCapture && !_guard.TryAcquire())
            {
                // Another copy is already running and has been asked to surface. Exit
                // directly: calling desktop.Shutdown() here tears the dispatcher down
                // BEFORE the main loop starts, and the loop then throws
                // "Cannot perform requested operation because the Dispatcher shut down".
                // Nothing has been opened or written at this point, so leaving now is clean.
                _guard.Dispose();
                Environment.Exit(0);
                return;
            }
            _guard.ShowRequested += (_, _) => Dispatcher.UIThread.Post(ShowWindow);

            if (overlayShotPath is not null)
            {
                // Design-review path: the OSD alone, no engine, no main window — captured
                // through the same proven screenshot routine the main window uses.
                // OnExplicitShutdown, exactly as the main path does: with the default
                // (OnLastWindowClose) the lifetime can tear down before the loop starts.
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var osd = new Views.OverlayWindow { DataContext = SelfScreenshot.OverlayPreviewData() };
                desktop.MainWindow = osd;
                osd.Show();
                _ = SelfScreenshot.CaptureThenExitAsync(osd, overlayShotPath, settleMs: 1200);
                base.OnFrameworkInitializationCompleted();
                return;
            }

            _composition = new Composition();
            var settings = new AppSettingsStore(AppSettingsStore.DefaultPath()).Load();

            // Closing the last window must not kill the process while we live in the tray.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // The window exists only while it's on screen. Starting minimised builds none
            // at all, and closing to the tray destroys it (see OnWindowClosing) — a hidden
            // window kept every visited page, every cover image and every page timer alive
            // for the whole session, which was most of the app's idle memory.
            //
            // NOTE: assigning desktop.MainWindow makes Avalonia SHOW that window when the
            // lifetime starts. When starting minimised we therefore create nothing and leave
            // MainWindow unset; the first tray click builds and adopts the window.
            if (shotPath is not null)
            {
                var w = EnsureWindow();
                desktop.MainWindow = w;
                if (shotTab is not null) w.NavigateTo(shotTab);
                if (shotW > 0 && shotH > 0) { w.Width = shotW; w.Height = shotH; }
                w.Show();
                _ = SelfScreenshot.CaptureThenExitAsync(w, shotPath);
            }
            else if (navStressPath is not null)
            {
                _alwaysCloseToTray = true;
                var w = EnsureWindow();
                desktop.MainWindow = w;
                w.Show();
                _ = NavStress.RunThenExitAsync(w, navStressPath, () => Shutdown());
            }
            else if (!settings.StartMinimized)
            {
                desktop.MainWindow = EnsureWindow();
            }
            // Watching, profiles, recording, overlay, enforcement, auto-RAM. On a
            // background thread: started from here, its continuations would otherwise run
            // on the UI thread — crash recovery and tweak re-enforcement spawn subprocesses
            // and made the whole app "not responding" for seconds after launch. UI-touching
            // parts inside marshal themselves via IUiDispatcher.
            // Engine is null on macOS — no Windows subsystems to run.
            // Not in a throwaway measuring/screenshot copy: the engine acts on the real
            // machine — fans, memory caps, enforcement — and a second copy doing that fights
            // the user's own Crustcut (it once "restored BIOS fan control" under it).
            // --engine opts back in, for measuring the engine's own footprint.
            var runEngine = !isDebugCapture || Array.IndexOf(Program.Args, "--engine") >= 0;
            if (runEngine && _composition.Engine is { } engine)
                _ = Task.Run(() => engine.StartAsync());
            if (runEngine) _composition.GameOnlyApps?.Start();
            StartMemoryWatch();

            // Update check: quiet, once per launch, never blocking startup.
            if (_composition.Updates is { } updates)
            {
                // The window's banner wiring happens in EnsureWindow, per window instance.
                updates.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(updates.UpdateAvailable) && updates.UpdateAvailable)
                        Dispatcher.UIThread.Post(() => NotifyUpdate(updates.Headline));
                };
                _ = Task.Run(updates.CheckAsync);
            }
            // Start Menu shortcut + heal the autostart task if the exe moved (Windows-only
            // concepts: schtasks + .lnk shortcuts).
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    _composition.Registration.EnsureStartMenuShortcut();
                    if (settings.StartAtBoot) _composition.Registration.SetStartAtBoot(true);
                }
                catch { }
            }

            desktop.ShutdownRequested += (_, _) =>
            {
                _composition?.FanService?.Dispose();   // hands fans back to the BIOS
                _composition?.GameOnlyApps?.Dispose(); // never leave an app frozen behind us
                _composition?.Engine?.Dispose();
                _composition?.Overlay?.Dispose();
                _composition?.Overview.Dispose();
                _guard?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_exiting) return;

        var settings = new AppSettingsStore(AppSettingsStore.DefaultPath()).Load();
        if (!settings.MinimizeToTrayOnClose && !_alwaysCloseToTray) { Shutdown(); return; }

        // Let the close go through: the window is destroyed and rebuilt on the next tray
        // click (EnsureWindow). ShutdownMode.OnExplicitShutdown keeps the process alive.
    }

    /// <summary>The main window, built on demand. The shell view model is shared so the
    /// reopened window lands on the tab the user left.</summary>
    private MainWindow EnsureWindow()
    {
        if (_window is not null) return _window;
        var window = new MainWindow(_shell, _composition);
        window.Closing += OnWindowClosing;
        window.Closed += OnWindowClosed;
        if (_composition?.Updates is { } updates) window.AttachUpdates(updates);
        _window = window;
        return window;
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (sender is MainWindow w)
        {
            w.Closing -= OnWindowClosing;
            w.Closed -= OnWindowClosed;
        }
        _window = null;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d) d.MainWindow = null;
        if (_exiting) return;

        // Give the freed pages, images and render surfaces back to Windows now rather than
        // whenever the GC next gets round to it: the user just sent us to the tray, which is
        // exactly when our footprint should shrink. Aggressive = compact and decommit.
        _ = Task.Run(async () =>
        {
            await Task.Delay(1500);
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            // Then release our working set. Sitting in the tray, Crustcut touches only a sliver
            // of its pages each minute — the rest (UI code, fonts, render state) is exactly
            // the idle memory the RAM cleaner reclaims from other apps, so it goes back too.
            // What we still use faults back in quietly; this is the one app that can't
            // start a refault war with itself.
            _composition?.TrimOwnWorkingSet?.Invoke();
        });
    }

    private static System.Threading.Timer? _memoryWatch;

    /// <summary>Crustcut's own memory: an early cleanup whenever native memory piles up
    /// (see <see cref="PrimeOSTuner.Core.Diagnostics.MemoryWatch"/>), and a line in the log
    /// every 5 minutes saying where it stands and which job made it grow.</summary>
    private static void StartMemoryWatch()
    {
        var ticks = 0;
        _memoryWatch ??= new System.Threading.Timer(_ =>
        {
            try
            {
                if (PrimeOSTuner.Core.Diagnostics.MemoryWatch.CollectIfNativeGrew() is { } first) EngineLog.Log(first);
                if (++ticks % 150 == 0 && PrimeOSTuner.Core.Diagnostics.MemoryWatch.Report() is { } line) EngineLog.Log(line);
                else if (ticks == 1) PrimeOSTuner.Core.Diagnostics.MemoryWatch.Report();   // baseline
            }
            catch { /* diagnostics must never take the app down */ }
        }, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// Reverts every applied tweak (stored undo first, tweak's own default second),
    /// empties the re-enforcement list so nothing comes back, and switches off every
    /// automatic behaviour in settings. Writes a report next to the exe and exits.
    /// </summary>
    private static async Task RunRevertAllThenExitAsync()
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine($"Crustcut --revert-all  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        try
        {
            var comp = new Composition();
            var undoStore = new PrimeOSTuner.Core.Tweaks.PendingUndoStore(
                PrimeOSTuner.Core.Tweaks.PendingUndoStore.DefaultPath());
            var session = new PrimeOSTuner.Core.Profiles.SessionTweakStore(
                PrimeOSTuner.Core.Profiles.SessionTweakStore.DefaultPath());

            foreach (var tweak in comp.Tweaks)
            {
                try
                {
                    if (tweak is PrimeOSTuner.Core.Tweaks.IOneShotTweak) continue;
                    if (tweak.IsDestructive) continue;   // reverting an uninstall isn't a thing
                    if (await tweak.ProbeAsync() != PrimeOSTuner.Core.Tweaks.TweakState.Applied) continue;

                    var undo = await undoStore.GetAsync(tweak.Id);
                    PrimeOSTuner.Core.Tweaks.TweakResult r;
                    if (undo is not null) r = await tweak.RevertAsync(undo);
                    else if (tweak is PrimeOSTuner.Core.Tweaks.ISelfRevertingTweak selfRevert) r = await selfRevert.RevertToDefaultAsync();
                    else { report.AppendLine($"SKIP  {tweak.Id} (no undo data and no known default)"); continue; }

                    report.AppendLine($"{(r.Succeeded ? "OFF " : "FAIL")}  {tweak.Id}{(r.Succeeded ? "" : " — " + r.Error)}");
                    if (r.Succeeded) { await undoStore.RemoveAsync(tweak.Id); await session.RemoveAsync(tweak.Id); }
                }
                catch (Exception ex) { report.AppendLine($"FAIL  {tweak.Id} — {ex.Message}"); }
            }

            // Nothing automatic stays armed: the point of stock is that Crustcut only
            // watches. Fan control is left as the user set it — it's cooling, not an
            // optimization, and turning it off mid-test would change noise, not FPS.
            var store = new PrimeOSTuner.Core.Settings.AppSettingsStore(
                PrimeOSTuner.Core.Settings.AppSettingsStore.DefaultPath());
            var s = store.Load();
            s.RamAdaptiveEnabled = false;
            s.RamAutoOptimizeOnInterval = false;
            s.RamAutoOptimizeOnThreshold = false;
            s.AutoMemoryLimitsEnabled = false;
            s.ThermalGovernorEnabled = false;
            store.Save(s);
            report.AppendLine("OFF   adaptive/scheduled/threshold RAM cleanup, auto memory limits, Cool-Down Assist");
        }
        catch (Exception ex) { report.AppendLine($"ABORT — {ex.Message}"); }

        var outPath = Path.Combine(AppContext.BaseDirectory, "revert-all-report.txt");
        try { File.WriteAllText(outPath, report.ToString()); } catch { }
        EngineLog.Log("revert-all: completed — machine returned to stock, automatics disabled");
        Environment.Exit(0);
    }

    private void ShowWindow()
    {
        if (_composition is null || _exiting) return;
        var window = EnsureWindow();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: null } d)
            d.MainWindow = window;   // adopted on display (start-minimised / reopened path)
        window.Show();
        window.WindowState = WindowState.Normal;
        window.Activate();
    }

    private void Shutdown()
    {
        _exiting = true;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    /// <summary>
    /// The only correct way for anything outside this class to end the process. Calling
    /// desktop.Shutdown() directly does NOT work: shutdown closes the window, our Closing
    /// handler sees "minimise to tray on close" and cancels it, and the cancel aborts the
    /// whole shutdown — the app just hides and keeps running. The update swapper hit exactly
    /// that, and sat waiting forever for a process that was never going to exit.
    /// </summary>
    public void RequestExit() => Shutdown();

    /// <summary>
    /// Surfaces a new version even when the window is hidden in the tray: the tooltip
    /// carries the news, and the tray menu gains a direct way to act on it.
    /// </summary>
    private void NotifyUpdate(string headline)
    {
        try
        {
            var icons = TrayIcon.GetIcons(this);
            if (icons is null || icons.Count == 0) return;
            icons[0].ToolTipText = $"Crustcut — {headline}";
        }
        catch { /* the banner still carries the message */ }
    }

    private void TrayUpdateClick(object? sender, EventArgs e)
    {
        ShowWindow();   // the banner in the shell is where the UPDATE button lives
    }

    private void TrayClicked(object? sender, EventArgs e) => ShowWindow();

    private void TrayOpenClick(object? sender, EventArgs e) => ShowWindow();

    private async void TrayOptimizeClick(object? sender, EventArgs e)
    {
        try { if (_composition is not null) await _composition.Overview.RunOneClickAsync(); }
        catch { /* tray action must never crash the app */ }
    }

    private void TrayExitClick(object? sender, EventArgs e) => Shutdown();
}
