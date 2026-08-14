namespace PrimeOSTuner.Core.Fans;

/// <summary>Live picture of the fan-control loop for the UI.</summary>
/// <param name="ResolvedMode">What Auto currently resolves to (equals the selected mode
/// when the user picked one explicitly), so the UI can explain itself.</param>
/// <param name="LoadPercent">Smoothed system load Auto is reacting to.</param>
public sealed record FanStatus(
    bool Engaged,
    double? TempC,
    double? DutyPercent,
    IReadOnlyList<FanInfo> Fans,
    bool ConflictSuspected,
    FanMode ResolvedMode = FanMode.Balanced,
    double LoadPercent = 0,
    bool BoostTrimmed = false);

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

    /// <summary>Heat-based power management: trim turbo boost while the CPU holds at
    /// sustained high temperature, restore it once cool. Persists to settings.</summary>
    bool ThermalGovernorEnabled { get; set; }

    /// <summary>True once this machine's fans have been measured.</summary>
    bool IsCalibrated { get; }

    /// <summary>
    /// Measures each managed fan: steps its duty down until it stalls, then records the
    /// lowest safe speed for that specific fan. This is what makes the feature correct on
    /// hardware it has never seen — PWM fans, 3-pin DC fans and laptop blowers all stall
    /// at wildly different points. Reports progress as human-readable lines.
    /// </summary>
    Task<string> CalibrateAsync(IProgress<string>? progress = null, CancellationToken ct = default);

    /// <summary>Cycles a fan's RPM display correction (x0.5 / x1 / x2) for tachometers
    /// whose pulse-per-revolution count makes the reading half or double the truth.</summary>
    void CycleRpmScale(string fanName);
}
