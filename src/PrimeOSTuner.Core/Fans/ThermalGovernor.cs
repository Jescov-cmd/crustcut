namespace PrimeOSTuner.Core.Fans;

public enum GovernorAction { None, ReduceBoost, RestoreBoost }

/// <summary>
/// The safe implementation of "pull the CPU's power back when it's hot, give it back when
/// it cools". Real voltage control needs a kernel driver writing undocumented SMU
/// registers — crash-your-machine territory that no consumer app should ship. Turbo boost
/// through powercfg is the supported lever that moves the same needle (measured on the
/// reference machine: 79°C → 57°C idle), so heat-based power management here means
/// toggling boost, never voltage.
///
/// Pure state machine: temperatures in, one action out. Sustained-time thresholds on both
/// edges because instantaneous Ryzen temperatures swing 10°C in a second by design — a
/// governor that reacted to single readings would flap boost on and off constantly, which
/// is worse than either steady state.
/// </summary>
public sealed class ThermalGovernor
{
    /// <summary>Trim boost when the CPU has held at or above this…</summary>
    public const double ReduceAtC = 85;
    /// <summary>…for this long. Short enough to catch real heat soak, long enough to
    /// ignore a boost spike (which lasts a second or two).</summary>
    public const double ReduceAfterSeconds = 20;

    /// <summary>Restore boost once the CPU has stayed at or below this…</summary>
    public const double RestoreBelowC = 72;
    /// <summary>…for this long. Deliberately slower than the reduce edge: giving power
    /// back reheats the chip, so flapping costs more than a late restore.</summary>
    public const double RestoreAfterSeconds = 60;

    // The powercfg identity of the lever, shared with CpuBoostLimitTweak.
    public const string SubProcessorGuid = "54533251-82be-4824-96c1-47b60b740d00";
    public const string PerfBoostModeGuid = "be337238-0d82-4146-a960-4f3749d470c7";
    public const int BoostDisabled = 0;
    public const int BoostWindowsDefault = 2;

    public bool Reduced { get; private set; }

    private DateTime? _hotSinceUtc;
    private DateTime? _coolSinceUtc;

    public GovernorAction Update(double tempC, DateTime nowUtc)
    {
        if (tempC >= ReduceAtC)
        {
            _coolSinceUtc = null;
            _hotSinceUtc ??= nowUtc;
            if (!Reduced && (nowUtc - _hotSinceUtc.Value).TotalSeconds >= ReduceAfterSeconds)
            {
                Reduced = true;
                return GovernorAction.ReduceBoost;
            }
            return GovernorAction.None;
        }

        _hotSinceUtc = null;

        if (tempC <= RestoreBelowC)
        {
            _coolSinceUtc ??= nowUtc;
            if (Reduced && (nowUtc - _coolSinceUtc.Value).TotalSeconds >= RestoreAfterSeconds)
            {
                Reduced = false;
                return GovernorAction.RestoreBoost;
            }
            return GovernorAction.None;
        }

        // The in-between band (72–85): hold whatever state we're in. This gap is the
        // hysteresis that stops boost flapping while the chip hovers around warm.
        _coolSinceUtc = null;
        return GovernorAction.None;
    }

    /// <summary>The service restored boost outside the state machine (shutdown, toggle
    /// off, crash heal) — resync so a later engage starts clean.</summary>
    public void NotifyRestored()
    {
        Reduced = false;
        _hotSinceUtc = null;
        _coolSinceUtc = null;
    }
}
