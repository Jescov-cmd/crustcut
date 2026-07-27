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
            _guard = new SingleInstanceGuard();
            if (!_guard.TryAcquire())
            {
                // Another copy is already running and has been asked to surface.
                desktop.Shutdown();
                return;
            }
            _guard.ShowRequested += (_, _) => Dispatcher.UIThread.Post(ShowWindow);

            _composition = new Composition();
            var settings = new AppSettingsStore(AppSettingsStore.DefaultPath()).Load();

            _window = new MainWindow(new ShellViewModel(), _composition);
            _window.Closing += OnWindowClosing;

            // Closing the last window must not kill the process while we live in the tray.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.MainWindow = _window;

            if (settings.StartMinimized)
            {
                // Deliberately do not Show(). The tray icon is the only affordance — this is
                // exactly the behaviour that made users think the app had failed to open, so
                // the Settings page now explains it.
                _window.Hide();
            }

            // Off by default; honours whatever the user last chose.
            _composition.Overlay.SyncWithSettings();

            desktop.ShutdownRequested += (_, _) =>
            {
                _composition?.Overlay.Dispose();
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

    private void TrayClicked(object? sender, EventArgs e) => ShowWindow();

    private void TrayOpenClick(object? sender, EventArgs e) => ShowWindow();

    private void TrayExitClick(object? sender, EventArgs e) => Shutdown();
}
