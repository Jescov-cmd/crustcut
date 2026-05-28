using CommunityToolkit.Mvvm.ComponentModel;
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

    [ObservableProperty] private string _ramStatusMessage = "";

    public SettingsViewModel(
        AppSettingsStore store,
        RamCleanerTweak ramCleaner,
        SystemSampler sampler,
        TrayIconService tray,
        ISentinelService sentinel,
        ProfileLifecycleService lifecycle,
        AppRegistrationService registration)
    {
        _store = store;
        _ramCleaner = ramCleaner;
        _sampler = sampler;
        _tray = tray;
        _sentinel = sentinel;
        _lifecycle = lifecycle;
        _registration = registration;

        var loaded = store.Load();
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

    private void SaveIfNeeded()
    {
        if (_suspendSave) return;
        _store.Save(new AppSettings
        {
            RamAutoOptimizeOnInterval = RamAutoOptimizeOnInterval,
            RamAutoIntervalMinutes = RamAutoIntervalMinutes,
            RamAutoOptimizeOnThreshold = RamAutoOptimizeOnThreshold,
            RamThresholdPercent = RamThresholdPercent,
            StartAtBoot = StartAtBoot,
            StartMinimized = StartMinimized,
            MinimizeToTrayOnClose = MinimizeToTrayOnClose,
            NotificationsEnabled = NotificationsEnabled,
            SentinelEnabled = SentinelEnabled,
        });
    }

    public void Dispose()
    {
        _sampler.Sampled -= OnSampled;
        _intervalTimer.Stop();
        _intervalTimer.Dispose();
    }
}
