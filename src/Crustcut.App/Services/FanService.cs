using PrimeOSTuner.Core.Fans;
using PrimeOSTuner.Core.Settings;

namespace Crustcut.App.Services;

/// <summary>
/// The fan-control loop: every 2 seconds, read the hottest CPU temperature, evaluate the
/// active mode's curve, write the resulting duty to each managed fan. Safety story:
/// - temperature unreadable → restore BIOS control immediately (never fly blind)
/// - FanPolicy's failsafe (≥85°C → 100%) applies to every write, and each fan is held at
///   or above its own calibrated minimum (or a conservative default when unmeasured)
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

    // Auto follows SUSTAINED load, not momentary spikes: an exponential moving average
    // over roughly half a minute, so opening a folder doesn't spin the fans up and a real
    // workload still registers within seconds.
    private const double LoadSmoothing = 0.08;
    private double _smoothedLoad;
    private FanMode _resolvedMode = FanMode.Silent;

    /// <summary>Fed by the system sampler (via Composition). Load is the higher of CPU and
    /// GPU utilisation — a GPU-bound game barely touches the CPU but still makes heat.</summary>
    public void ReportLoad(double cpuPercent, double gpuPercent)
    {
        var load = Math.Clamp(Math.Max(cpuPercent, gpuPercent), 0, 100);
        _smoothedLoad = _smoothedLoad == 0 ? load : _smoothedLoad + LoadSmoothing * (load - _smoothedLoad);
    }

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
        return new FanStatus(_engaged, _lastTemp, _lastDuty, fans, _conflictSuspected,
            _resolvedMode, Math.Round(_smoothedLoad));
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

    private const double CalibrationStartPercent = 60;
    private const double CalibrationStepPercent = 5;

    // Auto-calibration guards: never sweep a machine that is already hot or busy, since
    // the sweep briefly runs fans slower than normal.
    private const double AutoCalibrationMaxTempC = 70;
    private const double AutoCalibrationMaxLoadPercent = 40;
    private bool _autoCalibrationStarted;

    private async Task<double> MeasureMinDutyAsync(string name, CancellationToken ct)
    {
        // Start HIGH and walk down. Starting low and walking down would record a minimum
        // below the stall point for any fan that already stalls at the first step (3-pin
        // DC fans routinely need 40%+), which is the one mistake this must never make.
        var lastGood = CalibrationStartPercent;
        for (var duty = CalibrationStartPercent; duty >= FanPolicy.HardMinDutyPercent; duty -= CalibrationStepPercent)
        {
            // Hold long enough for the fan to settle at the new speed before judging it.
            for (var i = 0; i < 4; i++)
            {
                _fans.TrySetDuty(name, duty);
                await Task.Delay(1000, ct);
            }
            var fan = _fans.Snapshot().FirstOrDefault(f =>
                f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (fan?.Rpm is not > 0) break;    // stalled — the previous step was the limit
            lastGood = duty;

            var temp = _fans.ReadCpuTempC();
            if (temp is > 80) break;           // never calibrate a machine into overheating
        }
        // One step of margin above the slowest speed the fan actually sustained.
        return Math.Min(100, lastGood + CalibrationStepPercent);
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

            // First run on this machine: measure the fans instead of assuming. Kept to a
            // cool, idle moment so the sweep can't compound an existing thermal problem,
            // and only once — the result persists.
            if (!_tuning.HasCalibration && !_autoCalibrationStarted
                && t < AutoCalibrationMaxTempC && _smoothedLoad < AutoCalibrationMaxLoadPercent)
            {
                _autoCalibrationStarted = true;
                EngineLog.Log("fans: first run — measuring this machine's fans automatically");
                _ = Task.Run(() => CalibrateAsync(
                    new Progress<string>(m => EngineLog.Log($"fans: {m}"))));
                return;
            }

            var selected = Enum.TryParse<FanMode>(s.FanMode, out var m) ? m : FanMode.Balanced;
            var mode = FanPolicy.ResolveMode(selected, _smoothedLoad, t, _resolvedMode);
            if (selected == FanMode.Auto && mode != _resolvedMode)
                EngineLog.Log($"fans: auto → {mode} (load {_smoothedLoad:F0}%, {t:F0}°C)");
            _resolvedMode = mode;
            var curveDuty = FanPolicy.Evaluate(mode, t);

            // Per-fan floors: a fan that calibrated at 22% idles at 22%, one that stalls
            // at 45% never goes below 45% — same curve, hardware-appropriate minimums.
            var applied = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in _fans.ManagedFanNames)
                applied[name] = Math.Max(curveDuty, _tuning.MinDutyFor(name));

            // Headline number reflects what the machine will actually do — the loudest fan
            // — not the curve's ideal, which a floor may be overriding.
            _lastDuty = applied.Count > 0 ? applied.Values.Max() : curveDuty;

            if (!_engaged)
            {
                try { File.WriteAllText(MarkerPath(), DateTime.UtcNow.ToString("O")); } catch { }
                _engaged = true;
                EngineLog.Log($"fans: engaged — {mode} mode at {t:F0}°C → {_lastDuty:F0}%");
            }

            foreach (var (name, duty) in applied) _fans.TrySetDuty(name, duty);
            DetectConflict(applied);
        }
        catch { /* never let the loop die; next tick retries */ }
        finally { System.Threading.Monitor.Exit(_gate); }
    }

    /// <summary>
    /// Flags another app fighting us for the fans. Compares each fan against the duty WE
    /// asked that fan for — comparing everything against one number would cry conflict at
    /// any fan legitimately held above the curve by its own calibrated floor.
    /// </summary>
    private void DetectConflict(IReadOnlyDictionary<string, double> applied)
    {
        var readback = _fans.Snapshot().Where(f => f.Managed).ToList();
        var comparable = readback
            .Where(f => f.DutyPercent is not null && applied.ContainsKey(f.Name))
            .ToList();

        var fighting = comparable.Count > 0 && comparable.All(f =>
            Math.Abs(f.DutyPercent!.Value - applied[f.Name]) > 12);

        _conflictStrikes = fighting ? _conflictStrikes + 1 : 0;
        var suspected = _conflictStrikes >= 3;
        if (suspected && !_conflictSuspected)
            EngineLog.Log("fans: hardware keeps overriding our duty — another program is fighting for the fans");
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
