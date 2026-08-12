using PrimeOSTuner.Core.History;
using PrimeOSTuner.Core.Tweaks;

namespace PrimeOSTuner.Core.Pipeline;

public sealed class OneClickOptimizer
{
    private readonly IReadOnlyList<ITweak> _tweaks;
    private readonly TweakHistory _history;

    public OneClickOptimizer(IEnumerable<ITweak> tweaks, TweakHistory history)
    {
        _tweaks = tweaks.ToList();
        _history = history;
    }

    public async Task<OptimizeReport> RunAsync(IProgress<(int Done, int Total, string CurrentName)>? progress = null, CancellationToken ct = default)
    {
        // One-shot actions are excluded too: "optimize now" means "turn on the good
        // settings", not "wipe caches you didn't ask to wipe" (a shader-cache wipe makes
        // the next game session STUTTER while it rebuilds — the opposite of a boost).
        // Opt-in tweaks are excluded too: settings whose effect depends on the specific
        // machine (MPO disable helps some systems and produces black rectangles on others)
        // must be a deliberate user choice, never a side effect of one click.
        var safe = _tweaks
            .Where(t => !t.IsDestructive && t is not IOneShotTweak && t is not IOptInTweak { OptIn: true })
            .ToList();
        var skippedDestructive = _tweaks.Count - safe.Count;
        var applied = new List<string>();
        var failures = new List<(string, string)>();
        int success = 0, failure = 0;

        for (int i = 0; i < safe.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var t = safe[i];
            progress?.Report((i, safe.Count, t.DisplayName));
            var result = await t.ApplyAsync(null, ct);
            if (result.Succeeded)
            {
                success++;
                applied.Add(t.Id);
                await _history.AppendAsync(new HistoryEntry(
                    Guid.NewGuid(), t.Id, t.DisplayName, DateTime.UtcNow, result.UndoData, false));
            }
            else
            {
                failure++;
                failures.Add((t.Id, result.Error ?? "unknown"));
            }
        }

        progress?.Report((safe.Count, safe.Count, "Done"));
        return new OptimizeReport(success, failure, skippedDestructive, applied, failures);
    }
}
