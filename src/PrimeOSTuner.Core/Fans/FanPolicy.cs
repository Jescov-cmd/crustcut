namespace PrimeOSTuner.Core.Fans;

public enum FanMode { Silent, Balanced, Performance, Auto }

/// <summary>One point on a fan curve: at <paramref name="TempC"/>, run at <paramref name="DutyPercent"/>.</summary>
public sealed record CurvePoint(double TempC, double DutyPercent);

/// <summary>
/// Pure fan-curve math. Given a temperature, produce a duty cycle — linear interpolation
/// between curve points, clamped to the safety floor, with a hard failsafe that outranks
/// every mode: at or above the failsafe temperature the answer is always 100%.
/// </summary>
public static class FanPolicy
{
    /// <summary>
    /// Absolute floor, whatever a curve or calibration says. No fan anywhere is asked to
    /// go below this — protection against a bad calibration on exotic hardware.
    /// </summary>
    public const double HardMinDutyPercent = 15;

    /// <summary>
    /// Floor for a fan that has NOT been calibrated on this machine. Deliberately
    /// conservative: 3-pin DC case fans and AIO pumps stall far higher than the PWM fans
    /// this was originally tuned against, and an unknown machine gets the safe answer
    /// until calibration measures the truth.
    /// </summary>
    public const double UncalibratedMinDutyPercent = 30;

    /// <summary>Curve floor. Per-fan calibrated minimums are applied on top of this by the
    /// service, so a fan that can idle at 22% does, and one that stalls at 45% doesn't.</summary>
    public const double MinDutyPercent = 22;

    /// <summary>At or above this CPU temperature every fan goes to 100%, mode be damned.
    /// Ryzen throttles at ~95°C; 85 leaves margin for the fans to actually catch it.</summary>
    public const double FailsafeTempC = 85;

    // Tuned on the user's Ryzen 7 7700, which idles 65-77°C by design: Silent stays near
    // ~1000 RPM through that whole normal band and only wakes up past ~72°C sustained.
    public static readonly IReadOnlyList<CurvePoint> Silent = new CurvePoint[]
    {
        new(45, 22), new(60, 24), new(72, 28), new(80, 40), new(85, 100),
    };

    public static readonly IReadOnlyList<CurvePoint> Balanced = new CurvePoint[]
    {
        new(40, 30), new(60, 45), new(75, 62), new(85, 100),
    };

    public static readonly IReadOnlyList<CurvePoint> Performance = new CurvePoint[]
    {
        new(40, 50), new(60, 75), new(70, 90), new(80, 100),
    };

    public static IReadOnlyList<CurvePoint> CurveFor(FanMode mode) => mode switch
    {
        FanMode.Silent => Silent,
        FanMode.Performance => Performance,
        _ => Balanced,
    };

    // Auto bands, measured against sustained system load (the higher of CPU and GPU).
    // "A game is running" is a bad proxy for heat — a 2D indie game and a AAA title are
    // not the same thermal event — so Auto follows what the machine is actually doing.
    private const double BalancedLoadPercent = 25;
    private const double PerformanceLoadPercent = 65;

    /// <summary>Load must fall this far below a band edge before Auto steps back down, so
    /// a workload hovering at a threshold doesn't oscillate the fans.</summary>
    private const double LoadHysteresisPercent = 10;

    /// <summary>
    /// Auto resolves by how hard the machine is working: quiet when idle or running
    /// something light, firmer under real load, full cooling only when genuinely pinned.
    /// <paramref name="currentlyResolved"/> supplies hysteresis — pass the mode Auto chose
    /// last time. Everything except Auto resolves to itself.
    /// </summary>
    public static FanMode ResolveMode(FanMode selected, double loadPercent, FanMode currentlyResolved)
    {
        if (selected != FanMode.Auto) return selected;

        // Step UP as soon as the band is reached; step DOWN only after clearing the edge
        // by the hysteresis margin.
        return currentlyResolved switch
        {
            FanMode.Performance => loadPercent < PerformanceLoadPercent - LoadHysteresisPercent
                ? Band(loadPercent)
                : FanMode.Performance,
            FanMode.Balanced => loadPercent >= PerformanceLoadPercent ? FanMode.Performance
                : loadPercent < BalancedLoadPercent - LoadHysteresisPercent ? FanMode.Silent
                : FanMode.Balanced,
            _ => Band(loadPercent),
        };
    }

    private static FanMode Band(double loadPercent) =>
        loadPercent >= PerformanceLoadPercent ? FanMode.Performance
        : loadPercent >= BalancedLoadPercent ? FanMode.Balanced
        : FanMode.Silent;

    /// <summary>Duty percent for the given temperature under the given mode.</summary>
    public static double Evaluate(FanMode mode, double tempC)
    {
        if (tempC >= FailsafeTempC) return 100;

        var curve = CurveFor(mode);
        if (tempC <= curve[0].TempC) return Math.Max(MinDutyPercent, curve[0].DutyPercent);
        if (tempC >= curve[^1].TempC) return Math.Max(MinDutyPercent, curve[^1].DutyPercent);

        for (var i = 1; i < curve.Count; i++)
        {
            if (tempC > curve[i].TempC) continue;
            var (t0, d0) = (curve[i - 1].TempC, curve[i - 1].DutyPercent);
            var (t1, d1) = (curve[i].TempC, curve[i].DutyPercent);
            var duty = d0 + (d1 - d0) * (tempC - t0) / (t1 - t0);
            return Math.Max(MinDutyPercent, Math.Min(100, duty));
        }
        return Math.Max(MinDutyPercent, curve[^1].DutyPercent);
    }
}
