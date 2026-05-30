using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class CpuCoreParkingTweak : ITweak
{
    private const string Subgroup = "SUB_PROCESSOR";
    private const string Setting = "CPMINCORES";
    private const int TargetValue = 100;

    // GUIDs for the same subgroup/setting, used to read the value from the registry.
    // CPMINCORES is a HIDDEN power setting, so `powercfg /query` returns nothing for it —
    // the probe has to read the persisted value directly.
    private const string ProcessorSubgroupGuid = "54533251-82be-4824-96c1-47b60b740d00";
    private const string CoreParkingMinCoresGuid = "0cc5b647-c1df-4637-891a-dec35c318583";

    private readonly IPowerPlanClient _client;

    public string Id => "game.cpu-core-parking";
    public string DisplayName => "Disable CPU core parking";
    public string Description => "Stops Windows from parking idle CPU cores. Marginal on modern systems under load (Windows already unparks for games); occasionally helps with sub-1ms wake latency on bursty workloads.";
    public bool RequiresElevation => true;
    public bool IsDestructive => false;
    public bool RequiresReboot => true;

    public CpuCoreParkingTweak(IPowerPlanClient client) { _client = client; }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        // Read from the registry, not powercfg /query — CPMINCORES is hidden and the query
        // returns nothing, which made the tile always read "off" even after a successful apply.
        var current = _client.GetActiveSchemeSettingIndexFromRegistry(ProcessorSubgroupGuid, CoreParkingMinCoresGuid);
        if (current is null) return Task.FromResult(TweakState.NotApplied);
        return Task.FromResult(current.Value == TargetValue ? TweakState.Applied : TweakState.NotApplied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var previous = _client.GetActiveSchemeSettingIndexFromRegistry(ProcessorSubgroupGuid, CoreParkingMinCoresGuid) ?? 0;
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
        var current = _client.GetActiveSchemeSettingIndexFromRegistry(ProcessorSubgroupGuid, CoreParkingMinCoresGuid);
        return Task.FromResult($"Will run powercfg /setacvalueindex SCHEME_CURRENT SUB_PROCESSOR CPMINCORES 100. Current value: {current?.ToString() ?? "default"}.");
    }
}
