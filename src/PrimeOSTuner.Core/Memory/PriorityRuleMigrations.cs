namespace PrimeOSTuner.Core.Memory;

public static class PriorityRuleMigrations
{
    /// <summary>
    /// Pre-v0.8 builds bulk-assigned BelowNormal to every auto-populated non-game app, so
    /// editors and tools the user actively works in (VS Code, OBS) were being deprioritised
    /// on every launch. Resets exactly that legacy shape — non-game, non-booster rules
    /// sitting at BelowNormal — back to Normal. Rules the user shaped by hand (games,
    /// booster-enabled, or any other priority) are untouched. Returns the number changed.
    /// </summary>
    public static int NormalizeLegacyBelowNormal(IList<PriorityRule> rules)
    {
        var changed = 0;
        for (var i = 0; i < rules.Count; i++)
        {
            var r = rules[i];
            if (r.IsGame || r.GameBooster || r.Priority != PriorityLevel.BelowNormal) continue;
            rules[i] = r with { Priority = PriorityLevel.Normal };
            changed++;
        }
        return changed;
    }

    /// <summary>
    /// Runs the normalisation against the store exactly once, gated by a marker file next
    /// to the rules. Safe to call on every startup; best-effort by design — a failure here
    /// must never block the app.
    /// </summary>
    public static async Task RunOnceAsync(PriorityRuleStore store, string markerPath)
    {
        try
        {
            if (File.Exists(markerPath)) return;

            var rules = (await store.LoadAsync()).ToList();
            if (NormalizeLegacyBelowNormal(rules) > 0)
                await store.SaveAsync(rules);

            await File.WriteAllTextAsync(markerPath,
                "belownormal-default migration ran " + DateTime.UtcNow.ToString("o"));
        }
        catch
        {
            // No marker written on failure, so it retries next launch.
        }
    }
}
