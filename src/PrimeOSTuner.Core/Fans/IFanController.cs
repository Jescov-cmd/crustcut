namespace PrimeOSTuner.Core.Fans;

/// <summary>Live state of one controllable fan.</summary>
public sealed record FanInfo(string Name, double? Rpm, double? DutyPercent);

/// <summary>
/// Hardware fan access. The Windows implementation drives the motherboard's SuperIO chip
/// via LibreHardwareMonitor; only fans that were actually spinning at startup are managed
/// (empty headers and the GPU's self-managed fans are left alone).
/// </summary>
public interface IFanController : IDisposable
{
    /// <summary>True when the machine exposes at least one controllable, connected fan.</summary>
    bool IsSupported { get; }

    /// <summary>Current RPM/duty for every managed fan.</summary>
    IReadOnlyList<FanInfo> Snapshot();

    /// <summary>Hottest CPU temperature reading, or null when sensors can't be read
    /// (callers MUST treat null as "restore automatic control now").</summary>
    double? ReadCpuTempC();

    /// <summary>Sets all managed fans to the given duty cycle (percent).</summary>
    bool TrySetAllDuty(double percent);

    /// <summary>Returns every managed fan to BIOS/automatic control.</summary>
    void RestoreAuto();
}
