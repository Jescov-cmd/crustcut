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
    private bool _loading;

    [ObservableProperty] private bool _ramAutoOptimizeOnInterval;
    [ObservableProperty] private int _ramAutoIntervalMinutes = 10;
    [ObservableProperty] private bool _ramAutoOptimizeOnThreshold;
    [ObservableProperty] private int _ramThresholdPercent = 90;

    [ObservableProperty] private bool _startAtBoot;
    [ObservableProperty] private bool _startMinimized;
    [ObservableProperty] private bool _minimizeToTrayOnClose;
    [ObservableProperty] private bool _sentinelEnabled;

    [ObservableProperty] private string _status = "";

    public SettingsViewModel(AppSettingsStore store)
    {
        _store = store;
        Load();
    }

    /// <summary>
    /// Warns when the cleanup interval is aggressive enough to be worth reconsidering.
    /// Two minutes means roughly thirty full process sweeps an hour.
    /// </summary>
    public bool IntervalIsAggressive => RamAutoOptimizeOnInterval && RamAutoIntervalMinutes < 5;

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
        _store.Save(s);

        Status = $"Saved at {DateTime.Now:HH:mm:ss}.";
    }

    partial void OnRamAutoOptimizeOnIntervalChanged(bool value) { Notify(); Save(); }
    partial void OnRamAutoIntervalMinutesChanged(int value) { Notify(); Save(); }
    partial void OnRamAutoOptimizeOnThresholdChanged(bool value) => Save();
    partial void OnRamThresholdPercentChanged(int value) => Save();
    partial void OnStartAtBootChanged(bool value) => Save();
    partial void OnStartMinimizedChanged(bool value) => Save();
    partial void OnMinimizeToTrayOnCloseChanged(bool value) => Save();
    partial void OnSentinelEnabledChanged(bool value) => Save();

    private void Notify() => OnPropertyChanged(nameof(IntervalIsAggressive));
}
