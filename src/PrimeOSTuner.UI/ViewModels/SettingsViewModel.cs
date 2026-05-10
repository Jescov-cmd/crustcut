using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32;
using PrimeOSTuner.Core.Monitoring;
using PrimeOSTuner.Core.Settings;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.UI.Services;

namespace PrimeOSTuner.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "PrimeOSTuner";

    private readonly AppSettingsStore _store;
    private readonly RamCleanerTweak? _ramCleaner;
    private readonly SystemSampler _sampler;
    private readonly TrayIconService _tray;
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

    [ObservableProperty] private string _ramStatusMessage = "";

    public SettingsViewModel(
        AppSettingsStore store,
        IEnumerable<ITweak> tweaks,
        SystemSampler sampler,
        TrayIconService tray)
    {
        _store = store;
        _ramCleaner = tweaks.OfType<RamCleanerTweak>().FirstOrDefault();
        _sampler = sampler;
        _tray = tray;

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
        _suspendSave = false;

        _intervalTimer.Elapsed += async (_, _) => await RunAutoRamCleanAsync();
        ConfigureIntervalTimer();

        _sampler.Sampled += OnSampled;
    }

    public async Task RunRamCleanupNowAsync()
    {
        if (_ramCleaner is null)
        {
            RamStatusMessage = "RAM cleaner not registered.";
            return;
        }
        var result = await _ramCleaner.ApplyAsync();
        RamStatusMessage = result.Succeeded
            ? $"RAM cleaned at {DateTime.Now:HH:mm:ss}."
            : $"Failed: {result.Error}";
    }

    private async Task RunAutoRamCleanAsync()
    {
        if (_ramCleaner is null) return;
        try
        {
            await _ramCleaner.ApplyAsync();
            var stamp = $"Auto-cleaned at {DateTime.Now:HH:mm:ss}.";
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            Action update = () => RamStatusMessage = stamp;
            if (dispatcher is null || dispatcher.CheckAccess()) update();
            else dispatcher.Invoke(update);

            if (NotificationsEnabled) _tray.ShowNotification("PrimeOS Tuner", "RAM auto-cleanup ran.");
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
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, writable: true);
            if (key is null) return;
            if (enabled)
            {
                var exe = Environment.ProcessPath ?? System.Reflection.Assembly.GetEntryAssembly()?.Location;
                if (!string.IsNullOrEmpty(exe))
                    key.SetValue(RunValueName, $"\"{exe}\"");
            }
            else
            {
                if (key.GetValue(RunValueName) is not null)
                    key.DeleteValue(RunValueName, throwOnMissingValue: false);
            }
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
        });
    }

    public void Dispose()
    {
        _sampler.Sampled -= OnSampled;
        _intervalTimer.Stop();
        _intervalTimer.Dispose();
    }
}
