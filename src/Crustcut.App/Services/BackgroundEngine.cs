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
        var s = SafeSettings();

        // ── Sentinel on/off follows the saved setting ────────────────────────────────
        try { _sentinel.Enabled = s.SentinelEnabled; } catch { }

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
        catch { /* profiles unavailable — plain monitoring still works */ }

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
        catch { }

        // ── Scheduled + threshold RAM cleanup ────────────────────────────────────────
        _ramTimer.Elapsed += async (_, _) => await AutoRamTickAsync();
        _ramTimer.Start();
        _sampler.Sampled += OnSampled;
    }

    private async Task AutoRamTickAsync()
    {
        try
        {
            var s = SafeSettings();
            if (!s.RamAutoOptimizeOnInterval || s.RamAutoIntervalMinutes <= 0) return;
            if ((DateTime.UtcNow - _lastAutoRamUtc).TotalMinutes < s.RamAutoIntervalMinutes) return;
            await RunRamCleanupAsync();
        }
        catch { }
    }

    private void OnSampled(object? sender, SystemSample sample)
    {
        try
        {
            var s = SafeSettings();
            if (!s.RamAutoOptimizeOnThreshold) { _thresholdFired = false; return; }

            if (sample.RamPercent >= s.RamThresholdPercent)
            {
                if (!_thresholdFired)
                {
                    _thresholdFired = true;
                    _ = RunRamCleanupAsync();
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

    private async Task RunRamCleanupAsync()
    {
        if (_ramRunning) return;
        _ramRunning = true;
        try
        {
            _lastAutoRamUtc = DateTime.UtcNow;
            await _ramTweak.ApplyAsync();
        }
        catch { }
        finally { _ramRunning = false; }
    }

    private AppSettings SafeSettings()
    {
        try { return _settings.Load(); }
        catch { return new AppSettings(); }
    }

    public void Dispose()
    {
        _ramTimer.Stop();
        _ramTimer.Dispose();
        _sampler.Sampled -= OnSampled;
        // ProfileLifecycleService has no Dispose; its watcher stops with the process.
    }
}
