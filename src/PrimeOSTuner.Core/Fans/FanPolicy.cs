namespace PrimeOSTuner.Core.Fans;

public enum FanMode { Silent, Balanced, Performance }

/// <summary>One point on a fan curve: at <paramref name="TempC"/>, run at <paramref name="DutyPercent"/>.</summary>
public sealed record CurvePoint(double TempC, double DutyPercent);

/// <summary>
/// Pure fan-curve math. Given a temperature, produce a duty cycle — linear interpolation
/// between curve points, clamped to the safety floor, with a hard failsafe that outranks
/// every mode: at or above the failsafe temperature the answer is always 100%.
/// </summary>
public static class FanPolicy
{
    /// <summary>Fans never run below this — a curve can make fans quiet, never stopped.</summary>
    public const double MinDutyPercent = 25;

    /// <summary>At or above this CPU temperature every fan goes to 100%, mode be damned.
    /// Ryzen throttles at ~95°C; 85 leaves margin for the fans to actually catch it.</summary>
    public const double FailsafeTempC = 85;

    public static readonly IReadOnlyList<CurvePoint> Silent = new CurvePoint[]
    {
        new(40, 28), new(60, 40), new(75, 55), new(85, 100),
    };

    public static readonly IReadOnlyList<CurvePoint> Balanced = new CurvePoint[]
    {
        new(40, 35), new(60, 55), new(75, 75), new(85, 100),
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
