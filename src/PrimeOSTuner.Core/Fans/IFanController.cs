namespace PrimeOSTuner.Core.Fans;

/// <summary>
/// Live state of one fan the machine exposes.
/// <paramref name="Managed"/> false means Crustcut only watches it: a GPU fan (the card
/// runs its own zero-RPM curve), a pump header, or a header with no fan connected.
/// </summary>
public sealed record FanInfo(
    string Name,
    double? Rpm,
    double? DutyPercent,
    bool Managed = true,
    string? Note = null);

/// <summary>
/// Hardware fan access. The Windows implementation drives motherboard fan headers through
/// LibreHardwareMonitor, which supports the common SuperIO chips (Nuvoton, ITE, Fintek)
/// across desktop boards; machines without a supported chip report IsSupported false and
/// the feature disables itself rather than pretending.
/// </summary>
public interface IFanController : IDisposable
{
    /// <summary>True when the machine exposes at least one controllable, connected fan.</summary>
    bool IsSupported { get; }

    /// <summary>
    /// Re-attempts hardware discovery. Needed because another app (SignalRGB) can hold
    /// the fan chip's mutex during startup, making the hardware invisible at that moment
    /// — giving up forever over a bad first 2 seconds strands the feature until restart.
    /// Returns true when controllable fans are (now) available.
    /// </summary>
    bool TryRediscover();

    /// <summary>Names of the fans this controller drives.</summary>
    IReadOnlyList<string> ManagedFanNames { get; }

    /// <summary>Every fan the machine exposes — managed ones plus monitor-only (GPU,
    /// pumps, empty headers), so the UI can show the full picture.</summary>
    IReadOnlyList<FanInfo> Snapshot();

    /// <summary>Hottest CPU temperature reading, or null when sensors can't be read
    /// (callers MUST treat null as "restore automatic control now").</summary>
    double? ReadCpuTempC();

    /// <summary>Sets one managed fan's duty cycle. False if the fan isn't managed.</summary>
    bool TrySetDuty(string fanName, double percent);

    /// <summary>Sets all managed fans to the given duty cycle (percent).</summary>
    bool TrySetAllDuty(double percent);

    /// <summary>Returns every managed fan to BIOS/automatic control.</summary>
    void RestoreAuto();
}
