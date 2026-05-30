using System.Collections.Generic;
using System.Windows;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Core.Lifecycle;
using PrimeOSTuner.Core.Monitoring;
using PrimeOSTuner.Core.Settings;
using PrimeOSTuner.UI.ViewModels;
using PrimeOSTuner.UI.Views;

namespace PrimeOSTuner.UI.Services;

/// <summary>
/// Owns the in-game performance overlay window: creates it, shows/hides it based on the
/// "enabled" setting and whether a game is running, persists its dragged position, and
/// exposes an entry point for the Settings tab to enter edit mode for repositioning.
/// All window operations are marshalled to the UI thread (game events arrive off-thread).
/// </summary>
public sealed class OverlayService : IDisposable
{
    private readonly AppSettingsStore _store;
    private readonly OverlayViewModel _vm;
    private readonly IGameProcessWatcher _watcher;
    private readonly SystemSampler _sampler;
    private readonly HashSet<string> _runningGameIds = new(StringComparer.OrdinalIgnoreCase);

    private OverlayWindow? _window;
    private bool _enabled;
    private bool _onlyInGame = true;

    public OverlayService(AppSettingsStore store, SystemSampler sampler, IGameProcessWatcher watcher,
        PrimeOSTuner.Core.Performance.FrameRecordingService frames)
    {
        _store = store;
        _watcher = watcher;
        _sampler = sampler;
        _vm = new OverlayViewModel(sampler, frames);
    }

    public void Initialize()
    {
        // The overlay needs the live metric stream even when the Dashboard tab is closed.
        // Start() is idempotent, so this is safe alongside the Dashboard also starting it.
        _sampler.Start();

        var s = _store.Load();
        _enabled = s.OverlayEnabled;
        _onlyInGame = s.OverlayOnlyInGame;
        _vm.ApplySettings(s);

        _window = new OverlayWindow(_vm) { Left = s.OverlayX, Top = s.OverlayY };
        _window.PositionChanged += SavePosition;
        // After the user finishes repositioning, hide the overlay again if it shouldn't be
        // showing (e.g. "only in game" is on and no game is running) — otherwise a reposition
        // done on the desktop leaves it stuck on screen.
        _window.EditModeEnded += () => Dispatch(UpdateVisibility);

        _watcher.GameStarted += OnGameStarted;
        _watcher.GameStopped += OnGameStopped;

        UpdateVisibility();
    }

    /// <summary>Re-read the toggle + metric/scale settings (e.g. after the Settings tab
    /// changed them). Does not move the window — position is owned by drag.</summary>
    public void RefreshFromSettings()
    {
        var s = _store.Load();
        _enabled = s.OverlayEnabled;
        _onlyInGame = s.OverlayOnlyInGame;
        Dispatch(() => { _vm.ApplySettings(s); UpdateVisibility(); });
    }

    /// <summary>Show the overlay and drop straight into edit mode so the user can drag it
    /// (used by the Settings "Reposition overlay" button).</summary>
    public void EnterEditMode()
    {
        Dispatch(() =>
        {
            if (_window is null) return;
            if (!_window.IsVisible) _window.Show();
            _window.SetEditMode(true);
        });
    }

    private void OnGameStarted(object? sender, KnownGame g)
    {
        lock (_runningGameIds) _runningGameIds.Add(g.Id);
        Dispatch(UpdateVisibility);
    }

    private void OnGameStopped(object? sender, GameStoppedArgs e)
    {
        lock (_runningGameIds) _runningGameIds.Remove(e.Game.Id);
        Dispatch(UpdateVisibility);
    }

    private void UpdateVisibility()
    {
        if (_window is null) return;
        bool gameRunning;
        lock (_runningGameIds) gameRunning = _runningGameIds.Count > 0;

        var shouldShow = _enabled && (!_onlyInGame || gameRunning);
        if (shouldShow && !_window.IsVisible) _window.Show();
        else if (!shouldShow && _window.IsVisible) _window.Hide();
    }

    private void SavePosition(double x, double y)
    {
        try
        {
            var s = _store.Load();
            s.OverlayX = x;
            s.OverlayY = y;
            _store.Save(s);
        }
        catch { /* position persistence is best-effort */ }
    }

    private static void Dispatch(Action a)
    {
        var d = Application.Current?.Dispatcher;
        if (d is null || d.CheckAccess()) a();
        else d.BeginInvoke(a);
    }

    public void Dispose()
    {
        _watcher.GameStarted -= OnGameStarted;
        _watcher.GameStopped -= OnGameStopped;
        _vm.Dispose();
        Dispatch(() => _window?.Close());
    }
}
