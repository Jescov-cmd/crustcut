namespace PrimeOSTuner.Core.Tweaks;

/// <summary>
/// On startup, re-applies any optimizer the user turned on that has since drifted OFF
/// (Windows reset it, a driver/GPU update reverted it, a power-scheme switch moved a
/// scheme-relative setting, or it was a volatile in-memory setting like timer resolution).
/// Tweaks still detected as applied are left alone, so this is cheap and idempotent.
/// </summary>
public static class DriftedTweakReapplier
{
    public sealed record Result(int Reapplied, int AlreadyApplied, int Failed);

    public static async Task<Result> ReapplyAsync(
        IEnumerable<ITweak> tweaks,
        IReadOnlyCollection<string> enforcedIds,
        CancellationToken ct = default)
    {
        // Build the lookup defensively — never throw on a duplicate id.
        var byId = new Dictionary<string, ITweak>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in tweaks) byId[t.Id] = t;

        int reapplied = 0, already = 0, failed = 0;
        foreach (var id in enforcedIds)
        {
            if (!byId.TryGetValue(id, out var tweak)) continue;
            try
            {
                if (await tweak.ProbeAsync(ct) == TweakState.Applied) { already++; continue; }
                var r = await tweak.ApplyAsync(null, ct);
                if (r.Succeeded) reapplied++; else failed++;
            }
            catch
            {
                failed++;
            }
        }
        return new Result(reapplied, already, failed);
    }
}
