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

    /// <summary>ISLC-style: purge the standby cache automatically, but ONLY when free
    /// memory is nearly gone while the cache is huge (the game-stutter condition).</summary>
    public bool StandbyAutoPurgeEnabled { get; set; } = false;

    /// <summary>When off, Crustcut never assigns memory caps by itself. Caps the user
    /// set by hand are still enforced — this governs the automatic behaviour only.
    /// Off by default: a hard cap on the wrong process is how the black-rectangle
    /// rendering bug happened, and every other automatic behaviour here is opt-in too.
    /// Someone who wants it can switch it on in Memory; nobody gets it unasked.</summary>
    public bool AutoMemoryLimitsEnabled { get; set; } = false;

    // Fan control
    public bool FanControlEnabled { get; set; } = false;
    public string FanMode { get; set; } = "Balanced";

    /// <summary>Adaptive mode: the engine reads memory pressure each minute and decides
    /// when/how hard to clean (including standby purges and Deep escalation) instead of
    /// running the fixed schedule/threshold settings. Supersedes them while on.</summary>
    public bool RamAdaptiveEnabled { get; set; } = true;

    // App behavior
    public bool StartAtBoot { get; set; } = false;
    public bool StartMinimized { get; set; } = false;
    public bool MinimizeToTrayOnClose { get; set; } = false;
    public bool NotificationsEnabled { get; set; } = true;

    // Sentinel
    public bool SentinelEnabled { get; set; } = true;

    // Game Boost — the last-activated mode ("", "basic", "performance", "aggressive").
    // Persisted so the toggle remembers it was on after a restart.
    public string GameBoostMode { get; set; } = "";

    // In-game performance overlay (RTSS-style OSD, powered by Sentinel metrics).
    public bool OverlayEnabled { get; set; } = false;
    public double OverlayX { get; set; } = 24;
    public double OverlayY { get; set; } = 24;
    public double OverlayScale { get; set; } = 1.0;   // 0.8–1.6 text size multiplier
    public bool OverlayShowFps { get; set; } = true;
    public bool OverlayShowCpu { get; set; } = true;
    public bool OverlayShowGpu { get; set; } = true;
    public bool OverlayShowRam { get; set; } = true;
    public bool OverlayShowVram { get; set; } = true;
    public bool OverlayShowNet { get; set; } = false;
    // Only show the overlay while a detected game is running (vs. always when enabled).
    public bool OverlayOnlyInGame { get; set; } = true;
}
