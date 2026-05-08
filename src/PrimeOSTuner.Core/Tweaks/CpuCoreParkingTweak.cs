using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class CpuCoreParkingTweak : ITweak
{
    private const string Subgroup = "SUB_PROCESSOR";
    private const string Setting = "CPMINCORES";
    private const int TargetValue = 100;

    private readonly IPowerPlanClient _client;

    public string Id => "game.cpu-core-parking";
    public string DisplayName => "Disable CPU core parking";
    public string Description => "Forces all CPU cores to remain unparked (Min Cores = 100%) so games can use every core under load instantly.";
    public bool RequiresElevation => true;
    public bool IsDestructive => false;

    public CpuCoreParkingTweak(IPowerPlanClient client) { _client = client; }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        var current = _client.GetActiveAcValueIndex(Subgroup, Setting);
        if (current is null) return Task.FromResult(TweakState.Unknown);
        return Task.FromResult(current.Value == TargetValue ? TweakState.Applied : TweakState.NotApplied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var previous = _client.GetActiveAcValueIndex(Subgroup, Setting) ?? 0;
        _client.SetActiveAcValueIndex(Subgroup, Setting, TargetValue);
        return Task.FromResult(TweakResult.Success(previous.ToString()));
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
    {
        if (!int.TryParse(undoData, out var prev))
            return Task.FromResult(TweakResult.Failure("Invalid undo data"));
        _client.SetActiveAcValueIndex(Subgroup, Setting, prev);
        return Task.FromResult(TweakResult.Success());
    }

    public Task<string> PreviewAsync(CancellationToken ct = default)
    {
        var current = _client.GetActiveAcValueIndex(Subgroup, Setting);
        return Task.FromResult($"Will run powercfg /setacvalueindex SCHEME_CURRENT SUB_PROCESSOR CPMINCORES 100. Current value: {current?.ToString() ?? "unknown"}.");
    }
}
