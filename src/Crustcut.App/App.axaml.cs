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
    private bool _exiting;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var (shotPath, shotTab, shotW, shotH) = SelfScreenshot.Parse(Program.Args);
            var overlayShotPath = SelfScreenshot.ParseOverlayShot(Program.Args);
            var navStressPath = NavStress.Parse(Program.Args);

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

            _window = new MainWindow(new ShellViewModel(), _composition);
            _window.Closing += OnWindowClosing;

            // Closing the last window must not kill the process while we live in the tray.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // NOTE: assigning desktop.MainWindow makes Avalonia SHOW that window when the
            // lifetime starts. Setting it here and calling Hide() afterwards is why
            // "start minimised to the tray" still flashed the window open on every boot —
            // Hide() ran before the lifetime had shown it, and the show won. When starting
            // minimised we leave MainWindow unset and adopt the window on first display.
            if (!settings.StartMinimized) desktop.MainWindow = _window;

            if (shotPath is not null)
            {
                desktop.MainWindow = _window;
                if (shotTab is not null) _window.NavigateTo(shotTab);
                if (shotW > 0 && shotH > 0) { _window.Width = shotW; _window.Height = shotH; }
                _window.Show();
                _ = SelfScreenshot.CaptureThenExitAsync(_window, shotPath);
            }
            else if (navStressPath is not null)
            {
                desktop.MainWindow = _window;
                _window.Show();
                _ = NavStress.RunThenExitAsync(_window, navStressPath, () => Shutdown());
            }
            else if (settings.StartMinimized)
            {
                // Nothing to do: the window was never shown and MainWindow was left unset,
                // so the app lives in the tray until the user asks for it. The Settings
                // page explains that the tray icon is the only affordance.
            }
            // Watching, profiles, recording, overlay, enforcement, auto-RAM. On a
            // background thread: started from here, its continuations would otherwise run
            // on the UI thread — crash recovery and tweak re-enforcement spawn subprocesses
            // and made the whole app "not responding" for seconds after launch. UI-touching
            // parts inside marshal themselves via IUiDispatcher.
            // Engine is null on macOS — no Windows subsystems to run.
            if (_composition.Engine is { } engine)
                _ = Task.Run(() => engine.StartAsync());

            // Update check: quiet, once per launch, never blocking startup.
            if (_composition.Updates is { } updates)
            {
                _window.AttachUpdates(updates);
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
        if (!settings.MinimizeToTrayOnClose) { Shutdown(); return; }

        e.Cancel = true;
        _window?.Hide();
    }

    private void ShowWindow()
    {
        if (_window is null) return;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: null } d)
            d.MainWindow = _window;   // adopted on first display (start-minimised path)
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
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
