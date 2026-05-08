using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class PowerPlanTweak : ITweak
{
    private readonly IPowerPlanClient _client;
    private static readonly Guid UltimateGuid = new("e9a42b02-d5df-448d-aa00-03f14749eb61");

    public string Id => "core.power-plan";
    public string DisplayName => "Switch to Ultimate Performance power plan";
    public string Description => "Sets Windows to the Ultimate Performance power plan, prioritizing speed over efficiency.";
    public bool RequiresElevation => true;
    public bool IsDestructive => false;

    public PowerPlanTweak(IPowerPlanClient client) { _client = client; }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        var active = _client.GetActivePlan();
        return Task.FromResult(active.Guid == UltimateGuid ? TweakState.Applied : TweakState.NotApplied);
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
