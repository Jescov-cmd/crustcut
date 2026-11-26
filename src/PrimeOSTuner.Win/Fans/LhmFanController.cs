using LibreHardwareMonitor.Hardware;
using PrimeOSTuner.Core.Fans;

namespace PrimeOSTuner.Win.Fans;

/// <summary>
/// Motherboard fan control via LibreHardwareMonitor (SuperIO chip access; needs admin for
/// its kernel driver). LHM covers the common Nuvoton/ITE/Fintek chips, so this works
/// across most desktop boards; anything unsupported reports IsSupported false.
///
/// Managed = a motherboard header that is controllable AND has a fan actually spinning on
/// it AND isn't a pump. Everything else (GPU fans, pump headers, empty headers) is
/// reported for display only:
/// - GPU fans run the card's own zero-RPM curve, which beats anything we'd impose
/// - slowing an AIO pump is a genuine hardware risk, never worth it for noise
/// - empty headers have nothing to control
///
/// Footprint rules, because this object lives for the whole session in a tray app:
/// - The GPU group is OFF unless the Fans tab is on screen. Opening it loads the vendor
///   libraries (NVAPI/NVML/ADL) and their driver worker threads — dozens of native threads
///   and tens of MB — purely so the tab can list the card's fans.
/// - Every sensor's value history is disabled. LHM keeps a day of samples per sensor by
///   default; nothing here reads it, and at one update every 2 s it only ever grows.
/// - Each piece of hardware is updated once per read, never once per sensor.
/// - All LHM access is serialised: the fan loop and the Fans tab read from different
///   threads, and the library is not thread-safe.
/// </summary>
public sealed class LhmFanController : IFanController
{
    private sealed record ManagedFan(ISensor Control, ISensor? Rpm);
    private sealed record MonitorFan(ISensor Rpm, string Note, bool IsGpu);

    private readonly Computer _computer;
    private readonly object _lhm = new();
    private readonly List<ManagedFan> _fans = new();
    private readonly List<MonitorFan> _monitorOnly = new();
    private readonly List<ISensor> _cpuTemps = new();
    private bool _disposed;

    private static readonly string[] PumpMarkers = { "Pump", "AIO", "W_PUMP", "Water" };

    public bool IsSupported { get { lock (_lhm) return _fans.Count > 0; } }

    public IReadOnlyList<string> ManagedFanNames
    {
        get { lock (_lhm) return _fans.Select(f => f.Control.Name).ToList(); }
    }

    public LhmFanController()
    {
        _computer = new Computer
        {
            IsMotherboardEnabled = true,
            IsControllerEnabled = true,
            IsCpuEnabled = true,
            IsGpuEnabled = false,    // opened on demand — see SetDetailedMonitoring
        };
        // One attempt only. RGB/monitoring software (SignalRGB, MSI Center, HWiNFO) can
        // hold the chip's mutex at the wrong instant; the fan service re-tries on its own
        // timer, so there's no reason to block app startup sleeping here.
        try { TryRediscover(); }
        catch
        {
            // Driver refused (another LHM instance running, or blocked) — IsSupported
            // stays false and the feature reports itself unavailable instead of crashing.
        }
    }

    public bool TryRediscover()
    {
        lock (_lhm)
        {
            if (_disposed) return false;
            if (_fans.Count > 0) return true;
            try
            {
                try { _computer.Close(); } catch { }
                _fans.Clear();
                _monitorOnly.Clear();
                _cpuTemps.Clear();
                _computer.Open();
                foreach (var hw in _computer.Hardware) Walk(hw);
                if (_fans.Count == 0) { try { _computer.Close(); } catch { } }
                return _fans.Count > 0;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>Opens the GPU group while the UI is showing the fan list, closes it
    /// again afterwards. The fan loop never needs it: GPU fans are display-only.</summary>
    public void SetDetailedMonitoring(bool on)
    {
        lock (_lhm)
        {
            if (_disposed || _fans.Count == 0 || _computer.IsGpuEnabled == on) return;
            try
            {
                if (on)
                {
                    _computer.IsGpuEnabled = true;
                    foreach (var hw in _computer.Hardware.Where(IsGpu)) Walk(hw);
                }
                else
                {
                    _monitorOnly.RemoveAll(m => m.IsGpu);
                    _computer.IsGpuEnabled = false;
                }
            }
            catch { /* display nicety — the fan loop is unaffected either way */ }
        }
    }

    private static bool IsPump(string name) =>
        PumpMarkers.Any(m => name.Contains(m, StringComparison.OrdinalIgnoreCase));

    private static bool IsGpu(IHardware hw) =>
        hw.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;

    private void Walk(IHardware hw)
    {
        hw.Update();
        foreach (var s in hw.Sensors) s.ValuesTimeWindow = TimeSpan.Zero;

        var controls = hw.Sensors.Where(s => s.SensorType == SensorType.Control).ToList();
        var rpms = hw.Sensors.Where(s => s.SensorType == SensorType.Fan).ToList();

        // Any chip that exposes fan controls is fair game — Nuvoton, ITE, Fintek and
        // embedded controllers all surface as SuperIO/Cooler hardware in LHM.
        var controllable = hw.HardwareType is HardwareType.SuperIO or HardwareType.Cooler;
        var gpu = IsGpu(hw);

        foreach (var rpm in rpms)
        {
            var control = controls.FirstOrDefault(c =>
                c.Name.Equals(rpm.Name, StringComparison.OrdinalIgnoreCase));

            if (!controllable || control?.Control is null)
            {
                _monitorOnly.Add(new MonitorFan(rpm,
                    gpu ? "graphics card (self-managed)" : "monitor only", gpu));
                continue;
            }
            if (IsPump(rpm.Name))
            {
                _monitorOnly.Add(new MonitorFan(rpm, "pump — never slowed", false));
                continue;
            }
            if (rpm.Value is not > 0)
            {
                _monitorOnly.Add(new MonitorFan(rpm, "no fan detected", false));
                continue;
            }

            _fans.Add(new ManagedFan(control, rpm));
        }

        // Controls with no matching tachometer: a fan may still be attached (some
        // headers don't report RPM). Left unmanaged — driving a fan we cannot verify
        // is spinning is exactly the risk this feature must not take.

        if (hw.HardwareType == HardwareType.Cpu)
            _cpuTemps.AddRange(hw.Sensors.Where(s => s.SensorType == SensorType.Temperature));

        foreach (var sub in hw.SubHardware) Walk(sub);
    }

    public IReadOnlyList<FanInfo> Snapshot()
    {
        lock (_lhm)
        {
            var result = new List<FanInfo>(_fans.Count + _monitorOnly.Count);
            try
            {
                var hardware = _fans.Select(f => f.Control.Hardware)
                    .Concat(_monitorOnly.Select(m => m.Rpm.Hardware))
                    .Distinct();
                foreach (var hw in hardware) hw.Update();

                foreach (var (control, rpm) in _fans)
                    result.Add(new FanInfo(control.Name, rpm?.Value, control.Value));
                foreach (var m in _monitorOnly)
                    result.Add(new FanInfo(m.Rpm.Name, m.Rpm.Value, null, Managed: false, Note: m.Note));
            }
            catch { /* transient sensor failure — return what we have */ }
            return result;
        }
    }

    public double? ReadCpuTempC()
    {
        lock (_lhm)
        {
            try
            {
                foreach (var hw in _cpuTemps.Select(t => t.Hardware).Distinct()) hw.Update();
                double? hottest = null;
                foreach (var t in _cpuTemps)
                {
                    if (t.Value is float v && v > 0 && v < 120)
                        hottest = hottest is double h ? Math.Max(h, v) : v;
                }
                return hottest;
            }
            catch
            {
                return null;   // callers must fail safe on null
            }
        }
    }

    public bool TrySetDuty(string fanName, double percent)
    {
        lock (_lhm)
        {
            if (_disposed) return false;
            var fan = _fans.FirstOrDefault(f =>
                f.Control.Name.Equals(fanName, StringComparison.OrdinalIgnoreCase));
            if (fan is null) return false;
            try
            {
                fan.Control.Control?.SetSoftware((float)Math.Clamp(percent, 0, 100));
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public bool TrySetAllDuty(double percent)
    {
        lock (_lhm)
        {
            if (_disposed || _fans.Count == 0) return false;
            var ok = true;
            foreach (var fan in _fans)
            {
                try { fan.Control.Control?.SetSoftware((float)Math.Clamp(percent, 0, 100)); }
                catch { ok = false; }
            }
            return ok;
        }
    }

    public void RestoreAuto()
    {
        lock (_lhm)
        {
            foreach (var fan in _fans)
            {
                try { fan.Control.Control?.SetDefault(); }
                catch { /* best effort — BIOS reasserts on reboot regardless */ }
            }
        }
    }

    public void Dispose()
    {
        lock (_lhm)
        {
            if (_disposed) return;
            _disposed = true;
        }
        RestoreAuto();
        lock (_lhm) { try { _computer.Close(); } catch { } }
    }
}
