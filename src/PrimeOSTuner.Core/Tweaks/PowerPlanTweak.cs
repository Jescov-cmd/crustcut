using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class PowerPlanTweak : ITweak
{
    private readonly IPowerPlanClient _client;

    public string Id => "core.power-plan";
    public string DisplayName => "Use the fastest power plan";
    public string Description => "Switches to the Ultimate Performance power plan. CPU stays near base clock instead of throttling down at idle; on laptops this also means warmer and louder.";
    public bool RequiresElevation => true;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    public PowerPlanTweak(IPowerPlanClient client) { _client = client; }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        // Apply activates a DUPLICATED Ultimate Performance scheme, which gets its OWN GUID
        // (not the template GUID below). Matching on the template GUID meant the tile always
        // read "not applied" even with Ultimate Performance active — so match by name.
        var active = _client.GetActivePlan();
        return Task.FromResult(
            active.Name.Equals("Ultimate Performance", StringComparison.OrdinalIgnoreCase)
                ? TweakState.Applied : TweakState.NotApplied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var previous = _client.GetActivePlan();
        var ultimate = _client.EnsureUltimatePerformancePlan();
        _client.SetActivePlan(ultimate);
        return Task.FromResult(TweakResult.Success(previous.Guid.ToString("D")));
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
    {
        if (!Guid.TryParse(undoData, out var previous))
            return Task.FromResult(TweakResult.Failure("Invalid undo data"));

        // The saved "previous" plan can be gone — the user deleted it, or a Windows update
        // removed an OEM plan. powercfg /setactive on a missing GUID fails with "Invalid
        // Parameters" and the whole revert throws, making it impossible to turn the tweak
        // off (error popup, toggle snaps back on). Fall back to a real, existing plan
        // (prefer Balanced) so turning it off always works.
        var plans = _client.ListPlans();
        var target = plans.Any(p => p.Guid == previous) ? previous : (PickFallback(plans) ?? previous);

        _client.SetActivePlan(target);
        return Task.FromResult(TweakResult.Success());
    }

    // A sane plan to return to when the originally-saved one no longer exists: Balanced if
    // present, else any non-Ultimate plan, else whatever's available.
    private static Guid? PickFallback(IReadOnlyList<PowerPlan> plans) =>
        (plans.FirstOrDefault(p => p.Name.Equals("Balanced", StringComparison.OrdinalIgnoreCase))
         ?? plans.FirstOrDefault(p => !p.Name.Equals("Ultimate Performance", StringComparison.OrdinalIgnoreCase))
         ?? plans.FirstOrDefault())?.Guid;

    public Task<string> PreviewAsync(CancellationToken ct = default)
    {
        var active = _client.GetActivePlan();
        return Task.FromResult($"Will switch active power plan from '{active.Name}' to Ultimate Performance.");
    }
}
