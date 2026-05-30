using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PrimeOSTuner.Core.Lifecycle;
using PrimeOSTuner.Core.Monitoring;
using PrimeOSTuner.Core.Sentinel;
using PrimeOSTuner.Core.Settings;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.UI.Services;

namespace PrimeOSTuner.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "PrimeOSTuner";

    private readonly AppSettingsStore _store;
    private readonly RamCleanerTweak _ramCleaner;
    private readonly SystemSampler _sampler;
    private readonly TrayIconService _tray;
    private readonly ISentinelService _sentinel;
    private readonly ProfileLifecycleService _lifecycle;
    private readonly AppRegistrationService _registration;
    private readonly OverlayService _overlay;
    private readonly System.Timers.Timer _intervalTimer = new() { AutoReset = true };
    private bool _suspendSave;
    private double _lastRamPercent;
    private bool _thresholdFired;

    [ObservableProperty] private bool _ramAutoOptimizeOnInterval;
    [ObservableProperty] private int _ramAutoIntervalMinutes = 10;
    [ObservableProperty] private bool _ramAutoOptimizeOnThreshold;
    [ObservableProperty] private int _ramThresholdPercent = 70;

    [ObservableProperty] private bool _startAtBoot;
    [ObservableProperty] private bool _startMinimized;
    [ObservableProperty] private bool _minimizeToTrayOnClose;
    [ObservableProperty] private bool _notificationsEnabled = true;
    [ObservableProperty] private bool _sentinelEnabled = true;

    // Performance overlay (in-game OSD)
    [ObservableProperty] private bool _overlayEnabled;
    [ObservableProperty] private bool _overlayOnlyInGame = true;
    [ObservableProperty] private bool _overlayShowFps = true;
    [ObservableProperty] private bool _overlayShowCpu = true;
    [ObservableProperty] private bool _overlayShowGpu = true;
    [ObservableProperty] private bool _overlayShowRam = true;
    [ObservableProperty] private bool _overlayShowVram = true;
    [ObservableProperty] private bool _overlayShowNet;
    [ObservableProperty] private double _overlayScale = 1.0;

    [ObservableProperty] private string _ramStatusMessage = "";

    public SettingsViewModel(
        AppSettingsStore store,
        RamCleanerTweak ramCleaner,
        SystemSampler sampler,
        TrayIconService tray,
        ISentinelService sentinel,
        ProfileLifecycleService lifecycle,
        AppRegistrationService registration,
        OverlayService overlay)
    {
        _store = store;
        _ramCleaner = ramCleaner;
        _sampler = sampler;
        _tray = tray;
        _sentinel = sentinel;
        _lifecycle = lifecycle;
        _registration = registration;
        _overlay = overlay;

        var loaded = store.Load();
        // Diagnostic: if these read back as defaults when the user expected saved values,
        // the settings file was missing/unreadable (e.g. clobbered by a second instance).
        Serilog.Log.Information(
            "Settings loaded: StartAtBoot={SAB} StartMin={SM} MinToTray={MTT} Sentinel={SEN} RamInterval={RI}",
            loaded.StartAtBoot, loaded.StartMinimized, loaded.MinimizeToTrayOnClose, loaded.SentinelEnabled, loaded.RamAutoOptimizeOnInterval);
        _suspendSave = true;
        RamAutoOptimizeOnInterval = loaded.RamAutoOptimizeOnInterval;
        RamAutoIntervalMinutes = loaded.RamAutoIntervalMinutes;
        RamAutoOptimizeOnThreshold = loaded.RamAutoOptimizeOnThreshold;
        RamThresholdPercent = loaded.RamThresholdPercent;
        StartAtBoot = loaded.StartAtBoot;
        StartMinimized = loaded.StartMinimized;
        MinimizeToTrayOnClose = loaded.MinimizeToTrayOnClose;
        NotificationsEnabled = loaded.NotificationsEnabled;
        SentinelEnabled = loaded.SentinelEnabled;
        OverlayEnabled = loaded.OverlayEnabled;
        OverlayOnlyInGame = loaded.OverlayOnlyInGame;
        OverlayShowFps = loaded.OverlayShowFps;
        OverlayShowCpu = loaded.OverlayShowCpu;
        OverlayShowGpu = loaded.OverlayShowGpu;
        OverlayShowRam = loaded.OverlayShowRam;
        OverlayShowVram = loaded.OverlayShowVram;
        OverlayShowNet = loaded.OverlayShowNet;
        OverlayScale = loaded.OverlayScale;
        _suspendSave = false;

        // Reconcile the scheduled task with the saved setting on startup. If the
        // user upgraded from an older build where StartAtBoot wrote to HKCU\Run,
        // their saved setting is `true` but no Task Scheduler entry exists yet —
        // ensure one is created. Conversely, if StartAtBoot is false, make sure
        // no task lingers from a prior session.
        try { _registration.SetStartAtBoot(StartAtBoot); } catch { /* not fatal */ }

        _intervalTimer.Elapsed += async (_, _) => await RunAutoRamCleanAsync();
        ConfigureIntervalTimer();

        _sampler.Sampled += OnSampled;
    }

    public async Task RunRamCleanupNowAsync()
    {
        var result = await _ramCleaner.ApplyAsync();
        RamStatusMessage = result.Succeeded
            ? $"Working sets trimmed at {DateTime.Now:HH:mm:ss}."
            : $"Failed: {result.Error}";
    }

    private async Task RunAutoRamCleanAsync()
    {
        try
        {
            await _ramCleaner.ApplyAsync();
            var stamp = $"Working sets auto-trimmed at {DateTime.Now:HH:mm:ss}.";
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            Action update = () => RamStatusMessage = stamp;
            if (dispatcher is null || dispatcher.CheckAccess()) update();
            else dispatcher.Invoke(update);

            if (NotificationsEnabled) _tray.ShowNotification("Crustcut", "Working sets trimmed.");
        }
        catch { }
    }

    private void OnSampled(object? sender, SystemSample s)
    {
        _lastRamPercent = s.RamPercent;
        if (!RamAutoOptimizeOnThreshold) { _thresholdFired = false; return; }

        if (s.RamPercent >= RamThresholdPercent)
        {
            if (!_thresholdFired)
            {
                _thresholdFired = true;
                _ = RunAutoRamCleanAsync();
            }
        }
        else if (s.RamPercent < RamThresholdPercent - 5)
        {
            // Hysteresis: re-arm only when we drop 5pp below the threshold
            _thresholdFired = false;
        }
    }

    private void ConfigureIntervalTimer()
    {
        _intervalTimer.Stop();
        if (RamAutoOptimizeOnInterval && RamAutoIntervalMinutes > 0)
        {
            _intervalTimer.Interval = TimeSpan.FromMinutes(RamAutoIntervalMinutes).TotalMilliseconds;
            _intervalTimer.Start();
        }
    }

    private void ApplyStartAtBoot(bool enabled)
    {
        // Delegate to Task Scheduler — HKCU\Run cannot UAC-elevate at startup,
        // and Crustcut requires admin per app.manifest, so the old Run-key
        // approach silently failed every boot.
        _registration.SetStartAtBoot(enabled);

        // Clean up any stale Run-key entry left over from older versions
        // (PrimeOS Tuner pre-v0.4.4) — those entries point at long-gone exes
        // and would just sit there forever otherwise.
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, writable: true);
            if (key?.GetValue(RunValueName) is not null)
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
        catch { }
    }

    // Auto-save on any property change
    partial void OnRamAutoOptimizeOnIntervalChanged(bool value) { ConfigureIntervalTimer(); SaveIfNeeded(); }
    partial void OnRamAutoIntervalMinutesChanged(int value) { ConfigureIntervalTimer(); SaveIfNeeded(); }
    partial void OnRamAutoOptimizeOnThresholdChanged(bool value) { _thresholdFired = false; SaveIfNeeded(); }
    partial void OnRamThresholdPercentChanged(int value) { _thresholdFired = false; SaveIfNeeded(); }
    partial void OnStartAtBootChanged(bool value) { ApplyStartAtBoot(value); SaveIfNeeded(); }
    partial void OnStartMinimizedChanged(bool value) => SaveIfNeeded();
    partial void OnMinimizeToTrayOnCloseChanged(bool value) => SaveIfNeeded();
    partial void OnNotificationsEnabledChanged(bool value) => SaveIfNeeded();
    partial void OnSentinelEnabledChanged(bool value)
    {
        SaveIfNeeded();
        _sentinel.Enabled = value;
        // If the user turns Sentinel on while a game is already running, pick it up
        // immediately instead of waiting for the next stop/start cycle.
        if (value && _lifecycle.CurrentRunningGame is { } current)
        {
            try { _sentinel.OnGameStarted(current.Game, current.Pid); }
            catch { /* Sentinel must never break a settings toggle */ }
        }
    }

    // Overlay settings: save, then push the change to the live overlay.
    private void SaveAndRefreshOverlay() { SaveIfNeeded(); _overlay.RefreshFromSettings(); }
    partial void OnOverlayEnabledChanged(bool value) => SaveAndRefreshOverlay();
    partial void OnOverlayOnlyInGameChanged(bool value) => SaveAndRefreshOverlay();
    partial void OnOverlayShowFpsChanged(bool value) => SaveAndRefreshOverlay();
    partial void OnOverlayShowCpuChanged(bool value) => SaveAndRefreshOverlay();
    partial void OnOverlayShowGpuChanged(bool value) => SaveAndRefreshOverlay();
    partial void OnOverlayShowRamChanged(bool value) => SaveAndRefreshOverlay();
    partial void OnOverlayShowVramChanged(bool value) => SaveAndRefreshOverlay();
    partial void OnOverlayShowNetChanged(bool value) => SaveAndRefreshOverlay();
    partial void OnOverlayScaleChanged(double value) => SaveAndRefreshOverlay();

    /// <summary>Show the overlay and enter edit mode so the user can drag it where they want.</summary>
    [RelayCommand]
    private void RepositionOverlay()
    {
        if (!OverlayEnabled) OverlayEnabled = true;   // turning it on also saves + refreshes
        _overlay.EnterEditMode();
    }

    private void SaveIfNeeded()
    {
        if (_suspendSave) return;
        // Load-mutate-save so we only touch the fields this VM owns — fields managed
        // elsewhere (e.g. the overlay's position/metric settings) are preserved.
        var s = _store.Load();
        s.RamAutoOptimizeOnInterval = RamAutoOptimizeOnInterval;
        s.RamAutoIntervalMinutes = RamAutoIntervalMinutes;
        s.RamAutoOptimizeOnThreshold = RamAutoOptimizeOnThreshold;
        s.RamThresholdPercent = RamThresholdPercent;
        s.StartAtBoot = StartAtBoot;
        s.StartMinimized = StartMinimized;
        s.MinimizeToTrayOnClose = MinimizeToTrayOnClose;
        s.NotificationsEnabled = NotificationsEnabled;
        // SentinelEnabled is owned by the Sentinel tab now — don't write it here, or we'd
        // clobber a change made there. (Still loaded at startup for the service toggle.)
        // Overlay fields the Settings tab also exposes:
        s.OverlayEnabled = OverlayEnabled;
        s.OverlayShowFps = OverlayShowFps;
        s.OverlayShowCpu = OverlayShowCpu;
        s.OverlayShowGpu = OverlayShowGpu;
        s.OverlayShowRam = OverlayShowRam;
        s.OverlayShowVram = OverlayShowVram;
        s.OverlayShowNet = OverlayShowNet;
        s.OverlayOnlyInGame = OverlayOnlyInGame;
        s.OverlayScale = OverlayScale;
        _store.Save(s);
    }

    public void Dispose()
    {
        _sampler.Sampled -= OnSampled;
        _intervalTimer.Stop();
        _intervalTimer.Dispose();
    }
}
