using PrimeOSTuner.Core.Tweaks;

namespace PrimeOSTuner.Core.Monitoring;

/// <summary>
/// Computes a 0-100 boost score from the live state of tweaks the app actually owns.
///
/// Method: probe every non-destructive tweak (i.e. the eligible-for-one-click set), count
/// how many report Applied, divide by the total. PartiallyApplied counts as half. Unknown
/// is excluded from the denominator so a tweak whose probe failed doesn't punish the score.
///
/// Why non-destructive only: destructive tweaks (driver-store cleanup, registry cleanup,
/// per-app GPU prefs) are user-initiated, not "should always be on". Including them would
/// either always-cost points or require manual opt-in to score 100.
/// </summary>
public static class BoostScoreCalculator
{
    public sealed record Result(int Score, int Applied, int Total, string Tier);

    public static async Task<Result> ComputeAsync(IEnumerable<ITweak> tweaks, CancellationToken ct = default)
    {
        var eligible = tweaks.Where(t => !t.IsDestructive).ToList();
        double points = 0;
        int counted = 0;

        foreach (var t in eligible)
        {
            ct.ThrowIfCancellationRequested();
            TweakState state;
            try { state = await t.ProbeAsync(ct); }
            catch { state = TweakState.Unknown; }

            switch (state)
            {
                case TweakState.Applied:           points += 1.0; counted++; break;
                case TweakState.PartiallyApplied:  points += 0.5; counted++; break;
                case TweakState.NotApplied:                       counted++; break;
                case TweakState.Unknown:                                    break;
            }
        }

        if (counted == 0) return new Result(0, 0, 0, TierFor(0));

        var score = (int)Math.Round(points / counted * 100);
        return new Result(score, (int)Math.Round(points), counted, TierFor(score));
    }

    public static string TierFor(int score) => score switch
    {
        >= 90 => "EXCELLENT",
        >= 75 => "GREAT",
        >= 55 => "GOOD",
        >= 35 => "FAIR",
        _     => "POOR",
    };
}
