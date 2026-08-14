using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class PowerPlanTweak : ITweak, ICategorizedTweak, ISelfRevertingTweak
{
    private readonly IPowerPlanClient _client;

    public string Category => "power";
    public string? RiskNote => "Runs hotter: keeps the CPU near full clock instead of idling down, so expect more heat and fan noise — especially on laptops.";

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
        Guid.TryParse(undoData, out var previous);

        // Two ways the saved "previous" plan betrays the revert:
        // - it's GONE (user deleted it, or a Windows update removed an OEM plan) —
        //   /setactive on a missing GUID throws and the toggle snaps back on;
        // - it IS an Ultimate Performance plan. That happens when apply ran while the
        //   machine was already on Ultimate (an OPTIMIZE NOW followed by a manual toggle
        //   does exactly this): the recorded "previous" is the plan itself, so revert
        //   "switched" Ultimate → Ultimate, reported success, and the tweak never turned
        //   off. Poisoned undo gets the fallback, same as missing undo.
        var plans = _client.ListPlans();
        var saved = plans.FirstOrDefault(p => p.Guid == previous);
        var target = saved is not null &&
                     !saved.Name.Equals("Ultimate Performance", StringComparison.OrdinalIgnoreCase)
            ? saved.Guid
            : PickFallback(plans) ?? previous;

        _client.SetActivePlan(target);
        return Task.FromResult(TweakResult.Success());
    }

    /// <summary>Undo lost or applied outside the app: step back to a sane everyday plan.</summary>
    public Task<TweakResult> RevertToDefaultAsync(CancellationToken ct = default)
    {
        var fallback = PickFallback(_client.ListPlans());
        if (fallback is null)
            return Task.FromResult(TweakResult.Failure("No other power plan exists to switch to."));
        _client.SetActivePlan(fallback.Value);
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
