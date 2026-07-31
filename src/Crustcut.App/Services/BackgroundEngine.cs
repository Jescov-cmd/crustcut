using Crustcut.Presentation;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Core.Lifecycle;
using PrimeOSTuner.Core.Memory;
using PrimeOSTuner.Core.Monitoring;
using PrimeOSTuner.Core.Performance;
using PrimeOSTuner.Core.Profiles;
using PrimeOSTuner.Core.Sentinel;
using PrimeOSTuner.Core.Settings;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win.Suspension;

namespace Crustcut.App.Services;

/// <summary>
/// Everything Crustcut does with no window open: watching for game launches, applying
/// per-game profiles, recording frame times, showing the overlay, re-enforcing tweaks
/// Windows quietly reverted, and the scheduled/threshold RAM cleanups. The WPF build wired
/// all of this in App.OnStartup; without this class the Avalonia app was a UI over nothing.
/// Every subsystem is failure-isolated: one dying must not take the app down.
/// </summary>
public sealed class BackgroundEngine : IDisposable
{
    private readonly GameProcessWatcher _watcher;
    private readonly ISentinelService _sentinel;
    private readonly FrameRecordingService _frames;
    private readonly OverlayService _overlay;
    private readonly AppSettingsStore _settings;
    private readonly IReadOnlyList<ITweak> _tweaks;
    private readonly RamCleanerTweak _ramTweak;
    private readonly SystemSampler _sampler;
    private readonly IUiDispatcher _ui;
    private readonly ProfileApplier _applier;
    private readonly IBackgroundSuspenderService _suspender;
    private readonly ActiveTweaksStore _activeStore;
    private readonly GameProfileStore _profileStore;
    private readonly SessionTweakStore _sessionTweaks;

    private ProfileLifecycleService? _lifecycle;
    private readonly System.Timers.Timer _ramTimer = new(60_000) { AutoReset = true };
    private DateTime _lastAutoRamUtc = DateTime.UtcNow;
    private bool _thresholdFired;
    private bool _ramRunning;

    public BackgroundEngine(
        GameProcessWatcher watcher, ISentinelService sentinel,
        FrameRecordingService frames, OverlayService overlay, AppSettingsStore settings,
        IReadOnlyList<ITweak> tweaks, RamCleanerTweak ramTweak, SystemSampler sampler,
        IUiDispatcher ui, ProfileApplier applier, IBackgroundSuspenderService suspender,
        ActiveTweaksStore activeStore, GameProfileStore profileStore,
        SessionTweakStore sessionTweaks)
    {
        _watcher = watcher; _sentinel = sentinel; _frames = frames;
        _overlay = overlay; _settings = settings; _tweaks = tweaks; _ramTweak = ramTweak;
        _sampler = sampler; _ui = ui; _applier = applier; _suspender = suspender;
        _activeStore = activeStore; _profileStore = profileStore; _sessionTweaks = sessionTweaks;
    }

    public async Task StartAsync()
    {
        EngineLog.Log("engine: StartAsync entered");
        var s = SafeSettings();

        // ── Sentinel on/off follows the saved setting ────────────────────────────────
        try { _sentinel.Enabled = s.SentinelEnabled; }
        catch (Exception ex) { EngineLog.Log($"engine: sentinel enable failed: {ex.Message}"); }

        // ── Per-game profiles + session recording ────────────────────────────────────
        try
        {
            var custom = await new CustomProfileStore(CustomProfileStore.DefaultPath()).LoadAsync();
            var profiles = new Dictionary<string, ModeProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["basic"] = BuiltInProfiles.Basic,
                ["performance"] = BuiltInProfiles.Performance,
                ["aggressive"] = BuiltInProfiles.Aggressive,
                ["custom"] = custom,
            };

            _lifecycle = new ProfileLifecycleService(
                _watcher, _profileStore, _activeStore, profiles, _applier,
                _suspender,
                _sentinel, _frames);

            await _lifecycle.RecoverFromCrashAsync();
            _lifecycle.Start();
        }
        catch (Exception ex) { EngineLog.Log($"engine: profile lifecycle failed: {ex.Message}"); }

        // ── Overlay: honour OverlayOnlyInGame ────────────────────────────────────────
        try
        {
            if (s.OverlayEnabled && !s.OverlayOnlyInGame) _ui.Post(_overlay.Show);

            _watcher.GameStarted += (_, _) =>
            {
                if (SafeSettings().OverlayEnabled) _ui.Post(_overlay.Show);
            };
            _watcher.GameStopped += (_, _) =>
            {
                if (SafeSettings().OverlayOnlyInGame) _ui.Post(_overlay.Hide);
            };
        }
        catch { }

        // ── Re-enforce optimizers the user turned on that Windows reverted ───────────
        try
        {
            var ids = await _sessionTweaks.LoadAsync();
            if (ids.Count > 0)
                await DriftedTweakReapplier.ReapplyAsync(_tweaks, ids);
        }
        catch (Exception ex) { EngineLog.Log($"engine: drift re-enforcement failed: {ex.Message}"); }

        // ── Scheduled + threshold RAM cleanup ────────────────────────────────────────
        _ramTimer.Elapsed += async (_, _) => await AutoRamTickAsync();
        _ramTimer.Start();
        _sampler.Sampled += OnSampled;
        EngineLog.Log($"engine: RAM cleanup armed (interval={s.RamAutoOptimizeOnInterval} every {s.RamAutoIntervalMinutes}m, threshold={s.RamAutoOptimizeOnThreshold} at {s.RamThresholdPercent}%)");
    }

    /// <summary>Trimming below this much RAM pressure is pure harm: evicted pages don't
    /// free (their apps still own them) — Windows just compresses them and burns CPU
    /// faulting them back. Verified live: a 2-minute sweep drove Memory Compression to
    /// the top of the process list while the user sat at 90%+ "used".</summary>
    private const double RamPressureFloorPercent = 70;

    /// <summary>Sweeping every process's working set more often than this guarantees
    /// thrash — trimmed pages barely settle before the next eviction.</summary>
    private const int MinIntervalMinutes = 5;

    private double _lastRamPercent;
    private string _lastSkipReason = "";

    private async Task AutoRamTickAsync()
    {
        try
        {
            var s = SafeSettings();
            string? skip = null;
            var minutes = Math.Max(MinIntervalMinutes, s.RamAutoIntervalMinutes);
            if (!s.RamAutoOptimizeOnInterval || s.RamAutoIntervalMinutes <= 0)
                skip = "schedule off";
            else if ((DateTime.UtcNow - _lastAutoRamUtc).TotalMinutes < minutes)
                skip = $"waiting ({minutes}m interval)";
            else if (_lastRamPercent < RamPressureFloorPercent)
                skip = $"RAM {_lastRamPercent:F0}% below {RamPressureFloorPercent:F0}% pressure floor";

            if (skip is not null)
            {
                // Log skip-reason CHANGES only — a steady state shouldn't spam the log.
                if (skip != _lastSkipReason) { EngineLog.Log($"tick: skipping — {skip}"); _lastSkipReason = skip; }
                return;
            }
            _lastSkipReason = "";
            EngineLog.Log($"tick: running scheduled cleanup (RAM {_lastRamPercent:F0}%)");
            await RunRamCleanupAsync("scheduled");
        }
        catch (Exception ex) { EngineLog.Log($"tick: failed: {ex.Message}"); }
    }

    private void OnSampled(object? sender, SystemSample sample)
    {
        try
        {
            _lastRamPercent = sample.RamPercent;
            var s = SafeSettings();
            if (!s.RamAutoOptimizeOnThreshold) { _thresholdFired = false; return; }

            if (sample.RamPercent >= s.RamThresholdPercent)
            {
                if (!_thresholdFired)
                {
                    _thresholdFired = true;
                    EngineLog.Log($"threshold: RAM {sample.RamPercent:F0}% crossed {s.RamThresholdPercent}% — cleaning");
                    _ = RunRamCleanupAsync("threshold");
                }
            }
            else if (sample.RamPercent < s.RamThresholdPercent - 5)
            {
                // Hysteresis: re-arm only once usage drops 5pp below the threshold, so a
                // machine hovering at the line doesn't get swept every sample.
                _thresholdFired = false;
            }
        }
        catch { }
    }

    private async Task RunRamCleanupAsync(string trigger)
    {
        if (_ramRunning) return;
        _ramRunning = true;
        try
        {
            _lastAutoRamUtc = DateTime.UtcNow;
            var result = await _ramTweak.ApplyAsync();
            EngineLog.Log($"cleanup ({trigger}): {(result.Succeeded ? result.Message : "FAILED: " + result.Error)}");
        }
        catch (Exception ex) { EngineLog.Log($"cleanup ({trigger}): threw: {ex.Message}"); }
        finally { _ramRunning = false; }
    }

    // Settings are consulted on every sampler tick (~1/s); reading + parsing the JSON
    // from disk each time was pure waste. A short TTL keeps edits near-instant.
    private AppSettings? _cachedSettings;
    private DateTime _cachedSettingsAt;

    private AppSettings SafeSettings()
    {
        if (_cachedSettings is not null && (DateTime.UtcNow - _cachedSettingsAt).TotalSeconds < 3)
            return _cachedSettings;
        try { _cachedSettings = _settings.Load(); }
        catch { _cachedSettings ??= new AppSettings(); }
        _cachedSettingsAt = DateTime.UtcNow;
        return _cachedSettings;
    }

    public void Dispose()
    {
        _ramTimer.Stop();
        _ramTimer.Dispose();
        _sampler.Sampled -= OnSampled;
        // ProfileLifecycleService has no Dispose; its watcher stops with the process.
    }
}
