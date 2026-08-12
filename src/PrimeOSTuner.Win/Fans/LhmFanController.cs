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
/// </summary>
public sealed class LhmFanController : IFanController
{
    private sealed record ManagedFan(ISensor Control, ISensor? Rpm);
    private sealed record MonitorFan(ISensor Rpm, string Note);

    private readonly Computer _computer;
    private readonly List<ManagedFan> _fans = new();
    private readonly List<MonitorFan> _monitorOnly = new();
    private readonly List<ISensor> _cpuTemps = new();
    private readonly HashSet<IHardware> _hardware = new();
    private bool _disposed;

    private static readonly string[] PumpMarkers = { "Pump", "AIO", "W_PUMP", "Water" };

    public bool IsSupported => _fans.Count > 0;

    public IReadOnlyList<string> ManagedFanNames => _fans.Select(f => f.Control.Name).ToList();

    public LhmFanController()
    {
        _computer = new Computer
        {
            IsMotherboardEnabled = true,
            IsControllerEnabled = true,
            IsCpuEnabled = true,
            IsGpuEnabled = true,     // monitor-only, so the user sees every fan they hear
        };
        try
        {
            // RGB/monitoring software (SignalRGB, MSI Center, HWiNFO) polls the same chip
            // and briefly holds its hardware mutex; enumerate at the wrong instant and the
            // chip is silently absent. Retry — and the service keeps calling TryRediscover
            // afterwards, so a bad first 2 seconds no longer strands the feature.
            for (var attempt = 0; attempt < 3; attempt++)
            {
                if (TryRediscover()) break;
                Thread.Sleep(750);
            }
        }
        catch
        {
            // Driver refused (another LHM instance running, or blocked) — IsSupported
            // stays false and the feature reports itself unavailable instead of crashing.
        }
    }

    public bool TryRediscover()
    {
        if (_disposed) return false;
        if (_fans.Count > 0) return true;
        try
        {
            try { _computer.Close(); } catch { }
            _fans.Clear();
            _monitorOnly.Clear();
            _cpuTemps.Clear();
            _hardware.Clear();
            _computer.Open();
            Discover();
            if (_fans.Count == 0) { try { _computer.Close(); } catch { } }
            return _fans.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsPump(string name) =>
        PumpMarkers.Any(m => name.Contains(m, StringComparison.OrdinalIgnoreCase));

    private void Discover()
    {
        void Walk(IHardware hw)
        {
            hw.Update();
            var controls = hw.Sensors.Where(s => s.SensorType == SensorType.Control).ToList();
            var rpms = hw.Sensors.Where(s => s.SensorType == SensorType.Fan).ToList();

            // Any chip that exposes fan controls is fair game — Nuvoton, ITE, Fintek and
            // embedded controllers all surface as SuperIO/Cooler hardware in LHM.
            var controllable = hw.HardwareType is HardwareType.SuperIO or HardwareType.Cooler;

            foreach (var rpm in rpms)
            {
                var control = controls.FirstOrDefault(c =>
                    c.Name.Equals(rpm.Name, StringComparison.OrdinalIgnoreCase));

                if (!controllable || control?.Control is null)
                {
                    _monitorOnly.Add(new MonitorFan(rpm,
                        hw.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel
                            ? "graphics card (self-managed)"
                            : "monitor only"));
                    continue;
                }
                if (IsPump(rpm.Name))
                {
                    _monitorOnly.Add(new MonitorFan(rpm, "pump — never slowed"));
                    continue;
                }
                if (rpm.Value is not > 0)
                {
                    _monitorOnly.Add(new MonitorFan(rpm, "no fan detected"));
                    continue;
                }

                _fans.Add(new ManagedFan(control, rpm));
                _hardware.Add(hw);
            }

            // Controls with no matching tachometer: a fan may still be attached (some
            // headers don't report RPM). Left unmanaged — driving a fan we cannot verify
            // is spinning is exactly the risk this feature must not take.

            if (hw.HardwareType == HardwareType.Cpu)
                _cpuTemps.AddRange(hw.Sensors.Where(s => s.SensorType == SensorType.Temperature));

            foreach (var sub in hw.SubHardware) Walk(sub);
        }

        foreach (var hw in _computer.Hardware) Walk(hw);
    }

    public IReadOnlyList<FanInfo> Snapshot()
    {
        var result = new List<FanInfo>(_fans.Count + _monitorOnly.Count);
        try
        {
            // Update each hardware ONCE, not once per sensor.
            foreach (var hw in _fans.Select(f => f.Control.Hardware).Distinct()) hw.Update();
            foreach (var (control, rpm) in _fans)
                result.Add(new FanInfo(control.Name, rpm?.Value, control.Value));

            foreach (var m in _monitorOnly)
            {
                m.Rpm.Hardware.Update();
                result.Add(new FanInfo(m.Rpm.Name, m.Rpm.Value, null, Managed: false, Note: m.Note));
            }
        }
        catch { /* transient sensor failure — return what we have */ }
        return result;
    }

    public double? ReadCpuTempC()
    {
        try
        {
            double? hottest = null;
            foreach (var t in _cpuTemps)
            {
                t.Hardware.Update();
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

    public bool TrySetDuty(string fanName, double percent)
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

    public bool TrySetAllDuty(double percent)
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

    public void RestoreAuto()
    {
        foreach (var fan in _fans)
        {
            try { fan.Control.Control?.SetDefault(); }
            catch { /* best effort — BIOS reasserts on reboot regardless */ }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        RestoreAuto();
        try { _computer.Close(); } catch { }
    }
}
