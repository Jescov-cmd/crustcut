using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Settings;

namespace Crustcut.Presentation;

/// <summary>
/// Settings page. Overlay and tray-notification settings are deliberately absent until
/// those services are ported — showing a toggle that controls nothing would be worse than
/// not showing it.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettingsStore _store;
    private readonly IAppRegistration? _registration;
    private readonly IOverlayControl? _overlay;
    private bool _loading;

    [ObservableProperty] private bool _ramAutoOptimizeOnInterval;
    [ObservableProperty] private int _ramAutoIntervalMinutes = 10;
    [ObservableProperty] private bool _ramAutoOptimizeOnThreshold;
    [ObservableProperty] private int _ramThresholdPercent = 90;

    [ObservableProperty] private bool _startAtBoot;
    [ObservableProperty] private bool _startMinimized;
    [ObservableProperty] private bool _minimizeToTrayOnClose;
    [ObservableProperty] private bool _sentinelEnabled;

    [ObservableProperty] private bool _overlayEnabled;
    [ObservableProperty] private bool _overlayOnlyInGame;
    [ObservableProperty] private bool _overlayShowFps;
    [ObservableProperty] private bool _overlayShowCpu;
    [ObservableProperty] private bool _overlayShowGpu;
    [ObservableProperty] private bool _overlayShowRam;

    [ObservableProperty] private string _status = "";

    public SettingsViewModel(
        AppSettingsStore store, IAppRegistration? registration = null, IOverlayControl? overlay = null)
    {
        _store = store;
        _registration = registration;
        _overlay = overlay;
        Load();
    }

    /// <summary>
    /// Warns when the cleanup interval is aggressive enough to be worth reconsidering.
    /// Two minutes means roughly thirty full process sweeps an hour.
    /// </summary>
    public bool IntervalIsAggressive => RamAutoOptimizeOnInterval && RamAutoIntervalMinutes < 5;

    /// <summary>Settings cards for Windows-only subsystems (RAM cleaner, schtasks
    /// autostart, PresentMon overlay/sentinel) bind their visibility to this.</summary>
    public bool IsWindows => OperatingSystem.IsWindows();

    public string AboutText =>
        // Entry assembly = Crustcut.App, which carries the real <Version>.
        $"Crustcut {System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "?"}" +
        $" on {(OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsMacOS() ? "macOS" : "Linux")}";

    public void Load()
    {
        _loading = true;
        try
        {
            var s = _store.Load();
            RamAutoOptimizeOnInterval = s.RamAutoOptimizeOnInterval;
            RamAutoIntervalMinutes = s.RamAutoIntervalMinutes;
            RamAutoOptimizeOnThreshold = s.RamAutoOptimizeOnThreshold;
            RamThresholdPercent = s.RamThresholdPercent;
            StartAtBoot = s.StartAtBoot;
            StartMinimized = s.StartMinimized;
            MinimizeToTrayOnClose = s.MinimizeToTrayOnClose;
            SentinelEnabled = s.SentinelEnabled;
            OverlayEnabled = s.OverlayEnabled;
            OverlayOnlyInGame = s.OverlayOnlyInGame;
            OverlayShowFps = s.OverlayShowFps;
            OverlayShowCpu = s.OverlayShowCpu;
            OverlayShowGpu = s.OverlayShowGpu;
            OverlayShowRam = s.OverlayShowRam;
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>
    /// Load-mutate-save rather than writing a freshly constructed AppSettings. Several
    /// owners write this file (the overlay persists its drag position, for one), so
    /// replacing the whole object would silently clobber fields this page never shows.
    /// </summary>
    public void Save()
    {
        if (_loading) return;

        var s = _store.Load();
        s.RamAutoOptimizeOnInterval = RamAutoOptimizeOnInterval;
        s.RamAutoIntervalMinutes = RamAutoIntervalMinutes;
        s.RamAutoOptimizeOnThreshold = RamAutoOptimizeOnThreshold;
        s.RamThresholdPercent = RamThresholdPercent;
        s.StartAtBoot = StartAtBoot;
        s.StartMinimized = StartMinimized;
        s.MinimizeToTrayOnClose = MinimizeToTrayOnClose;
        s.SentinelEnabled = SentinelEnabled;
        s.OverlayEnabled = OverlayEnabled;
        s.OverlayOnlyInGame = OverlayOnlyInGame;
        s.OverlayShowFps = OverlayShowFps;
        s.OverlayShowCpu = OverlayShowCpu;
        s.OverlayShowGpu = OverlayShowGpu;
        s.OverlayShowRam = OverlayShowRam;
        _store.Save(s);

        Status = $"Saved at {DateTime.Now:HH:mm:ss}.";
    }

    partial void OnRamAutoOptimizeOnIntervalChanged(bool value) { Notify(); Save(); }
    partial void OnRamAutoIntervalMinutesChanged(int value) { Notify(); Save(); }
    partial void OnRamAutoOptimizeOnThresholdChanged(bool value) => Save();
    partial void OnRamThresholdPercentChanged(int value) => Save();
    partial void OnStartAtBootChanged(bool value)
    {
        // Persist the flag AND make it real: a scheduled task with highest privileges is
        // the only way an admin-manifest app can start at logon without a UAC prompt.
        Save();
        if (!_loading) _registration?.SetStartAtBoot(value);
    }
    partial void OnStartMinimizedChanged(bool value) => Save();
    partial void OnMinimizeToTrayOnCloseChanged(bool value) => Save();
    partial void OnSentinelEnabledChanged(bool value) => Save();
    partial void OnOverlayEnabledChanged(bool value) { Save(); _overlay?.Sync(); }
    partial void OnOverlayOnlyInGameChanged(bool value) { Save(); _overlay?.Sync(); }
    partial void OnOverlayShowFpsChanged(bool value) => Save();
    partial void OnOverlayShowCpuChanged(bool value) => Save();
    partial void OnOverlayShowGpuChanged(bool value) => Save();
    partial void OnOverlayShowRamChanged(bool value) => Save();

    /// <summary>Shows the overlay (if hidden) and enters drag-to-reposition mode.</summary>
    public void RepositionOverlay()
    {
        if (_overlay is null) return;
        if (!OverlayEnabled) OverlayEnabled = true;   // saves + syncs via the hook
        _overlay.ToggleEditMode();
    }

    private void Notify() => OnPropertyChanged(nameof(IntervalIsAggressive));
}
