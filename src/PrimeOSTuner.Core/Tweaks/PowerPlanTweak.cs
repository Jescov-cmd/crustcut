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
        _client.SetActivePlan(previous);
        return Task.FromResult(TweakResult.Success());
    }

    public Task<string> PreviewAsync(CancellationToken ct = default)
    {
        var active = _client.GetActivePlan();
        return Task.FromResult($"Will switch active power plan from '{active.Name}' to Ultimate Performance.");
    }
}
