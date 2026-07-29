using Avalonia;
using Avalonia.Controls;
using Crustcut.App.Views;
using Crustcut.Presentation;
using PrimeOSTuner.Core.Settings;

namespace Crustcut.App.Services;

/// <summary>
/// Owns the overlay window: creates it on demand, applies the saved position and metric
/// choices, and persists the position after a drag.
/// </summary>
public sealed class OverlayService : IDisposable, Crustcut.Presentation.IOverlayControl
{
    private readonly OverlayViewModel _vm;
    private readonly AppSettingsStore _store;
    private readonly GlobalHotkey _hotkey = new();

    private OverlayWindow? _window;

    public OverlayService(OverlayViewModel vm, AppSettingsStore store)
    {
        _vm = vm;
        _store = store;
    }

    public bool IsVisible => _window is not null;

    public void Show()
    {
        var settings = _store.Load();
        _vm.ApplySettings(settings);

        if (_window is null)
        {
            _window = new OverlayWindow { DataContext = _vm };
            _window.Position = new PixelPoint((int)settings.OverlayX, (int)settings.OverlayY);
            _window.Closed += (_, _) => _window = null;
            _window.Show();

            // Hotkey has to be registered after the window exists — it needs the HWND.
            _hotkey.Register(_window);
        }
    }

    public void Hide()
    {
        if (_window is null) return;
        PersistPosition();
        _window.Close();
        _window = null;
    }

    /// <summary>Toggles drag-to-reposition. Persists the new position when leaving edit mode.</summary>
    public void ToggleEditMode()
    {
        if (_window is null) return;
        _window.EditMode = !_window.EditMode;
        if (!_window.EditMode) PersistPosition();
    }

    private void PersistPosition()
    {
        if (_window is null) return;

        // Load-mutate-save: the Settings page writes this same file, so replacing the whole
        // object here would clobber whatever the user last changed there.
        var s = _store.Load();
        s.OverlayX = _window.Position.X;
        s.OverlayY = _window.Position.Y;
        _store.Save(s);
    }

    void Crustcut.Presentation.IOverlayControl.Sync()
        => Avalonia.Threading.Dispatcher.UIThread.Post(SyncWithSettings);

    /// <summary>Shows or hides the overlay to match saved settings. With OverlayOnlyInGame
    /// set, the overlay stays hidden here and the game-start event shows it.</summary>
    public void SyncWithSettings()
    {
        var s = _store.Load();
        if (s.OverlayEnabled && !s.OverlayOnlyInGame) Show();
        else Hide();
    }

    public void Dispose()
    {
        _hotkey.Dispose();
        _window?.Close();
        _window = null;
    }
}
