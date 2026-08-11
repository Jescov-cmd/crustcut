using LibreHardwareMonitor.Hardware;
using PrimeOSTuner.Core.Fans;

namespace PrimeOSTuner.Win.Fans;

/// <summary>
/// Motherboard fan control via LibreHardwareMonitor (SuperIO chip access; needs admin for
/// its kernel driver). Deliberate scope limits:
/// - Only fans that were SPINNING at initialization are managed — writing duty to empty
///   headers is pointless, and a water pump on a "fan" header must never be slowed by us
///   (a pump reads as spinning, but pump headers are named; we skip names containing "Pump").
/// - GPU fans are never touched: modern cards run their own zero-RPM logic that is better
///   than anything we'd impose.
/// </summary>
public sealed class LhmFanController : IFanController
{
    private readonly Computer _computer;
    private readonly List<(ISensor Control, ISensor? Rpm)> _fans = new();
    private readonly List<ISensor> _cpuTemps = new();
    private bool _disposed;

    public bool IsSupported => _fans.Count > 0;

    public LhmFanController()
    {
        _computer = new Computer
        {
            IsMotherboardEnabled = true,
            IsControllerEnabled = true,
            IsCpuEnabled = true,
        };
        try
        {
            // RGB/monitoring software (SignalRGB on this machine) polls the same SuperIO
            // chip and briefly holds its hardware mutex; enumerate at the wrong instant
            // and the chip is silently absent. Retry a few times before giving up.
            for (var attempt = 0; attempt < 3; attempt++)
            {
                _computer.Open();
                Discover();
                if (_fans.Count > 0) break;
                _computer.Close();
                _fans.Clear();
                _cpuTemps.Clear();
                Thread.Sleep(750);
            }
        }
        catch
        {
            // Driver refused (another LHM instance running, or blocked) — IsSupported
            // stays false and the feature reports itself unavailable instead of crashing.
        }
    }

    private void Discover()
    {
        void Walk(IHardware hw)
        {
            hw.Update();
            var controls = hw.Sensors.Where(s => s.SensorType == SensorType.Control).ToList();
            var rpms = hw.Sensors.Where(s => s.SensorType == SensorType.Fan).ToList();

            if (hw.HardwareType == HardwareType.SuperIO)
            {
                foreach (var control in controls)
                {
                    if (control.Name.Contains("Pump", StringComparison.OrdinalIgnoreCase)) continue;
                    var rpm = rpms.FirstOrDefault(r => r.Name.Equals(control.Name, StringComparison.OrdinalIgnoreCase));
                    // Only manage fans that are actually connected and spinning.
                    if (rpm?.Value is > 0) _fans.Add((control, rpm));
                }
            }

            if (hw.HardwareType == HardwareType.Cpu)
                _cpuTemps.AddRange(hw.Sensors.Where(s => s.SensorType == SensorType.Temperature));

            foreach (var sub in hw.SubHardware) Walk(sub);
        }

        foreach (var hw in _computer.Hardware) Walk(hw);
    }

    public IReadOnlyList<FanInfo> Snapshot()
    {
        var result = new List<FanInfo>(_fans.Count);
        try
        {
            foreach (var (control, rpm) in _fans)
            {
                control.Hardware.Update();
                result.Add(new FanInfo(control.Name, rpm?.Value, control.Value));
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

    public bool TrySetAllDuty(double percent)
    {
        if (_disposed || _fans.Count == 0) return false;
        var ok = true;
        foreach (var (control, _) in _fans)
        {
            try { control.Control?.SetSoftware((float)Math.Clamp(percent, 0, 100)); }
            catch { ok = false; }
        }
        return ok;
    }

    public void RestoreAuto()
    {
        foreach (var (control, _) in _fans)
        {
            try { control.Control?.SetDefault(); }
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
