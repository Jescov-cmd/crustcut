namespace PrimeOSTuner.Core.Settings;

/// <summary>
/// User-configurable settings persisted to disk.
/// New sections: just add fields. The store serializes everything as JSON.
/// </summary>
public sealed class AppSettings
{
    // RAM optimization
    public bool RamAutoOptimizeOnInterval { get; set; } = false;
    public int RamAutoIntervalMinutes { get; set; } = 10;

    public bool RamAutoOptimizeOnThreshold { get; set; } = false;
    public int RamThresholdPercent { get; set; } = 70;

    // App behavior
    public bool StartAtBoot { get; set; } = false;
    public bool StartMinimized { get; set; } = false;
    public bool MinimizeToTrayOnClose { get; set; } = false;
    public bool NotificationsEnabled { get; set; } = true;

    // Sentinel
    public bool SentinelEnabled { get; set; } = true;
}
