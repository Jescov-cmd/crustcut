using PrimeOSTuner.Core.History;
using PrimeOSTuner.Core.Tweaks;

namespace PrimeOSTuner.Core.Profiles;

public sealed class ProfileApplier
{
    private readonly Dictionary<string, ITweak> _tweaks;
    private readonly TweakHistory _history;

    public ProfileApplier(IEnumerable<ITweak> tweaks, TweakHistory history)
    {
        _tweaks = tweaks.ToDictionary(t => t.Id);
        _history = history;
    }

    public async Task<ProfileResult> ApplyAsync(
        ModeProfile profile,
        IProgress<(int Done, int Total, string CurrentName)>? progress = null,
        CancellationToken ct = default)
    {
        var outcomes = new List<ProfileTweakOutcome>();
        int success = 0, failure = 0;

        for (int i = 0; i < profile.TweakIds.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var id = profile.TweakIds[i];

            if (!_tweaks.TryGetValue(id, out var tweak))
            {
                failure++;
                outcomes.Add(new ProfileTweakOutcome(id, false, null, $"Tweak '{id}' not registered"));
                continue;
            }

            progress?.Report((i, profile.TweakIds.Count, tweak.DisplayName));

            try
            {
                var r = await tweak.ApplyAsync(null, ct);
                if (r.Succeeded)
                {
                    success++;
                    outcomes.Add(new ProfileTweakOutcome(id, true, r.UndoData, null));
                    await _history.AppendAsync(new HistoryEntry(
                        Guid.NewGuid(), id, tweak.DisplayName, DateTime.UtcNow, r.UndoData, false));
                }
                else
                {
                    failure++;
                    outcomes.Add(new ProfileTweakOutcome(id, false, null, r.Error));
                }
            }
            catch (Exception ex)
            {
                failure++;
                outcomes.Add(new ProfileTweakOutcome(id, false, null, ex.Message));
            }
        }

        progress?.Report((profile.TweakIds.Count, profile.TweakIds.Count, "Done"));
        return new ProfileResult(profile.Id, success, failure, outcomes);
    }

    public async Task RevertAsync(IReadOnlyList<ProfileTweakOutcome> outcomes, CancellationToken ct = default)
    {
        for (int i = outcomes.Count - 1; i >= 0; i--)
        {
            ct.ThrowIfCancellationRequested();
            var o = outcomes[i];
            if (!o.Succeeded || o.UndoData is null) continue;
            if (!_tweaks.TryGetValue(o.TweakId, out var tweak)) continue;

            try
            {
                await tweak.RevertAsync(o.UndoData, ct);
            }
            catch
            {
            }
        }
    }
}
