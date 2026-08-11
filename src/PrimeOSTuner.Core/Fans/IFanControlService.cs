namespace PrimeOSTuner.Core.Fans;

/// <summary>Live picture of the fan-control loop for the UI.</summary>
public sealed record FanStatus(
    bool Engaged,
    double? TempC,
    double? DutyPercent,
    IReadOnlyList<FanInfo> Fans,
    bool ConflictSuspected);

/// <summary>
/// The running fan-control loop. Enabled/Mode persist to settings; the implementation
/// owns the safety story (failsafe temp, restore-on-exit, crash marker).
/// </summary>
public interface IFanControlService
{
    bool IsSupported { get; }
    bool Enabled { get; set; }
    FanMode Mode { get; set; }
    FanStatus Status();
}
