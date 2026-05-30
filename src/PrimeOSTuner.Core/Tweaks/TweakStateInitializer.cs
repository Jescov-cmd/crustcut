using PrimeOSTuner.Core.History;

namespace PrimeOSTuner.Core.Tweaks;

/// <summary>The probed initial state of a tweak when a view loads.</summary>
public sealed record TweakInitialState(string TweakId, bool IsApplied, string? UndoData);

/// <summary>
/// Computes the real, on-disk applied state of each tweak by probing the live system,
/// so UI toggles reflect what's actually applied instead of resetting to "off" on every
/// launch. Also recovers the most recent (not-yet-reverted) undo data from history so a
/// tweak that was applied in a PREVIOUS session can still be toggled off now.
/// </summary>
public static class TweakStateInitializer
{
    public static async Task<IReadOnlyList<TweakInitialState>> ComputeAsync(
        IEnumerable<ITweak> tweaks,
        TweakHistory history,
        CancellationToken ct = default)
    {
        // Recover the EARLIEST apply's undo data per tweak (resetting at each revert).
        // This matters because re-applying an already-applied tweak captures the applied
        // value as its "previous" — so the LATEST undo is often poisoned (reverting to it
        // restores the value right back to ON). The first apply in a streak holds the
        // pristine pre-Crustcut value, which is what a real "turn off" must restore.
        var pristineUndo = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var e in await history.LoadAsync())
            {
                if (e.Reverted) pristineUndo.Remove(e.TweakId);
                else if (!pristineUndo.ContainsKey(e.TweakId)) pristineUndo[e.TweakId] = e.UndoData;
            }
        }
        catch
        {
            // History is best-effort. A read failure just means no recovered undo data —
            // probing still drives the applied state correctly.
        }

        var result = new List<TweakInitialState>();
        foreach (var t in tweaks)
        {
            TweakState state;
            try { state = await t.ProbeAsync(ct); }
            catch { state = TweakState.Unknown; }

            var applied = state == TweakState.Applied;
            var undo = applied && pristineUndo.TryGetValue(t.Id, out var u) ? u : null;
            result.Add(new TweakInitialState(t.Id, applied, undo));
        }
        return result;
    }
}
