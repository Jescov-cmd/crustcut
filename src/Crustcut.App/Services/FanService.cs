using PrimeOSTuner.Core.Fans;
using PrimeOSTuner.Core.Settings;

namespace Crustcut.App.Services;

/// <summary>
/// The fan-control loop: every 2 seconds, read the hottest CPU temperature, evaluate the
/// active mode's curve, write the duty to every managed fan. Safety story:
/// - temperature unreadable → restore BIOS control immediately (never fly blind)
/// - FanPolicy's failsafe (≥85°C → 100%) and floor (never under 25%) apply to every write
/// - disable/exit/dispose → restore BIOS control
/// - crash: a marker file exists while control is engaged; if it's present at startup the
///   previous run died mid-control, so BIOS control is restored before anything else
/// - conflict detection: if the hardware's duty readback keeps disagreeing with what we
///   wrote, another app (SignalRGB's fan control, e.g.) is fighting us — surfaced in the
///   UI instead of silently losing.
/// </summary>
public sealed class FanService : IFanControlService, IDisposable
{
    private readonly IFanController _fans;
    private readonly AppSettingsStore _settings;
    private readonly FanTuningStore _tuning;
    private readonly System.Timers.Timer _timer = new(2000) { AutoReset = true };
    private readonly object _gate = new();

    private bool _engaged;
    private double? _lastTemp;
    private double? _lastDuty;
    private int _conflictStrikes;
    private bool _conflictSuspected;

    private static string MarkerPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PrimeOSTuner", "fan-control-active");

    public FanService(IFanController fans, AppSettingsStore settings, FanTuningStore tuning)
    {
        _fans = fans;
        _settings = settings;
        _tuning = tuning;

        // Crash recovery FIRST: if the marker survived, the last run died while it owned
        // the fans — hand them back to the BIOS before doing anything else.
        try
        {
            if (File.Exists(MarkerPath()))
            {
                _fans.RestoreAuto();
                File.Delete(MarkerPath());
                EngineLog.Log("fans: previous run crashed while controlling fans — BIOS control restored");
            }
        }
        catch { }

        _timer.Elapsed += (_, _) => Tick();
        _timer.Start();
    }

    public bool IsSupported => _fans.IsSupported;

    /// <summary>Fed by the game watcher (via Composition) — drives Auto mode.</summary>
    public volatile bool GameRunning;

    public bool Enabled
    {
        get => SafeSettings().FanControlEnabled;
        set
        {
            var s = SafeSettings();
            s.FanControlEnabled = value;
            try { _settings.Save(s); } catch { }
            if (!value) Disengage("disabled by user");
            else Tick();   // take effect immediately, not at the next timer tick
        }
    }

    public FanMode Mode
    {
        get => Enum.TryParse<FanMode>(SafeSettings().FanMode, out var m) ? m : FanMode.Balanced;
        set
        {
            var s = SafeSettings();
            s.FanMode = value.ToString();
            try { _settings.Save(s); } catch { }
            Tick();
        }
    }

    public FanStatus Status()
    {
        // RPM display correction (tachometer pulses-per-revolution differ by fan).
        var fans = _fans.Snapshot()
            .Select(f => f.Rpm is double r
                ? f with { Rpm = r * _tuning.RpmScaleFor(f.Name) }
                : f)
            .ToList();
        return new FanStatus(_engaged, _lastTemp, _lastDuty, fans, _conflictSuspected);
    }

    public bool IsCalibrated => _tuning.HasCalibration;

    public void CycleRpmScale(string fanName)
    {
        var next = _tuning.RpmScaleFor(fanName) switch
        {
            < 0.75 => 1.0,      // 0.5 -> 1
            < 1.5 => 2.0,       // 1   -> 2
            _ => 0.5,           // 2   -> 0.5
        };
        _tuning.SetRpmScale(fanName, next);
    }

    /// <summary>
    /// Steps each managed fan down until its tachometer reads zero, then records the last
    /// speed that still span (plus a safety step). Runs one fan at a time so a stall is
    /// unambiguous, and restores control afterwards.
    /// </summary>
    public async Task<string> CalibrateAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (!_fans.IsSupported) return "No controllable fans found.";
        var wasEnabled = Enabled;
        var results = new List<string>();
        try
        {
            _timer.Stop();   // the control loop must not fight the calibration
            foreach (var name in _fans.ManagedFanNames)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report($"Measuring {name}…");
                var min = await MeasureMinDutyAsync(name, ct);
                _tuning.Set(new FanTuning(name, min, _tuning.RpmScaleFor(name)));
                results.Add($"{name}: {min:F0}%");
                EngineLog.Log($"fans: calibrated {name} minimum {min:F0}%");
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { EngineLog.Log($"fans: calibration failed: {ex.Message}"); }
        finally
        {
            _fans.RestoreAuto();
            _engaged = false;
            _timer.Start();
            if (wasEnabled) Tick();   // resume control immediately
        }
        return results.Count == 0
            ? "Calibration could not measure any fan."
            : "Lowest safe speed — " + string.Join(", ", results);
    }

    private async Task<double> MeasureMinDutyAsync(string name, CancellationToken ct)
    {
        double lastGood = FanPolicy.UncalibratedMinDutyPercent;
        for (var duty = 40.0; duty >= FanPolicy.HardMinDutyPercent; duty -= 3)
        {
            // Hold long enough for the fan to settle at the new speed before judging it.
            for (var i = 0; i < 4; i++)
            {
                _fans.TrySetDuty(name, duty);
                await Task.Delay(1000, ct);
            }
            var fan = _fans.Snapshot().FirstOrDefault(f =>
                f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (fan?.Rpm is not > 0) break;    // stalled — previous step was the limit
            lastGood = duty;

            var temp = _fans.ReadCpuTempC();
            if (temp is > 80) break;           // never calibrate a machine into overheating
        }
        // One step of margin above the last speed that actually span.
        return Math.Min(100, lastGood + 3);
    }

    private int _rediscoverCountdown;

    private void Tick()
    {
        if (!System.Threading.Monitor.TryEnter(_gate)) return;   // ticks never overlap
        try
        {
            if (!_fans.IsSupported)
            {
                // Hardware invisible (SignalRGB likely held the chip at startup) —
                // re-scan every ~60s instead of giving up for the whole session.
                if (--_rediscoverCountdown > 0) return;
                _rediscoverCountdown = 30;
                if (_fans.TryRediscover())
                    EngineLog.Log("fans: hardware found on re-scan — control available");
                else
                    return;
            }
            var s = SafeSettings();
            if (!s.FanControlEnabled)
            {
                if (_engaged) Disengage("turned off");
                return;
            }

            var temp = _fans.ReadCpuTempC();
            _lastTemp = temp;
            if (temp is not double t)
            {
                // Blind = dangerous. Give the fans back and try again next tick.
                if (_engaged) Disengage("temperature unreadable — failing safe");
                return;
            }

            var selected = Enum.TryParse<FanMode>(s.FanMode, out var m) ? m : FanMode.Balanced;
            var mode = FanPolicy.ResolveMode(selected, GameRunning);
            var duty = FanPolicy.Evaluate(mode, t);
            _lastDuty = duty;

            if (!_engaged)
            {
                try { File.WriteAllText(MarkerPath(), DateTime.UtcNow.ToString("O")); } catch { }
                _engaged = true;
                EngineLog.Log($"fans: engaged — {mode} mode at {t:F0}°C → {duty:F0}%");
            }

            // Per-fan floors: a fan that calibrated at 22% idles at 22%, one that stalls
            // at 45% never goes below 45% — same curve, hardware-appropriate minimums.
            foreach (var name in _fans.ManagedFanNames)
                _fans.TrySetDuty(name, Math.Max(duty, _tuning.MinDutyFor(name)));
            DetectConflict(duty);
        }
        catch { /* never let the loop die; next tick retries */ }
        finally { System.Threading.Monitor.Exit(_gate); }
    }

    private void DetectConflict(double wantedDuty)
    {
        var readback = _fans.Snapshot();
        var fighting = readback.Count > 0 && readback.All(f =>
            f.DutyPercent is double d && Math.Abs(d - wantedDuty) > 12);
        _conflictStrikes = fighting ? _conflictStrikes + 1 : 0;
        var suspected = _conflictStrikes >= 3;
        if (suspected && !_conflictSuspected)
            EngineLog.Log("fans: hardware keeps overriding our duty — another app (SignalRGB fan control?) is fighting for the fans");
        _conflictSuspected = suspected;
    }

    private void Disengage(string reason)
    {
        _fans.RestoreAuto();
        _engaged = false;
        _conflictStrikes = 0;
        _conflictSuspected = false;
        try { File.Delete(MarkerPath()); } catch { }
        EngineLog.Log($"fans: BIOS control restored ({reason})");
    }

    private AppSettings SafeSettings()
    {
        try { return _settings.Load(); }
        catch { return new AppSettings(); }
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
        if (_engaged) Disengage("app closing");
        _fans.Dispose();
    }
}
