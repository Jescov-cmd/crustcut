using System.Text.Json;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class CpuCoreParkingTweak : ITweak, ICategorizedTweak, ISelfRevertingTweak
{
    public string Category => "power";
    public string? RiskNote => "Runs hotter: keeps every CPU core awake instead of letting idle ones sleep, so the system stays warmer and the fans spin up more — especially at the desktop.";

    private const int TargetValue = 100;
    private const int WindowsDefault = 0;   // parking allowed — what every shipped plan uses

    // CPMINCORES is a HIDDEN power setting, so `powercfg /query` returns nothing for it —
    // probing reads the persisted value straight from the registry.
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

    private sealed record Undo(Dictionary<Guid, int?>? PerScheme);

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        var current = _client.GetActiveSchemeSettingIndexFromRegistry(ProcessorSubgroupGuid, CoreParkingMinCoresGuid);
        if (current is null) return Task.FromResult(TweakState.NotApplied);
        return Task.FromResult(current.Value == TargetValue ? TweakState.Applied : TweakState.NotApplied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        // Every scheme, like the boost tweak: power settings belong to a scheme, and
        // writing only the active one evaporates when the user changes plan.
        var previous = _client.SetValueIndexOnAllSchemes(
            ProcessorSubgroupGuid, CoreParkingMinCoresGuid, TargetValue);
        return Task.FromResult(TweakResult.Success(
            JsonSerializer.Serialize(new Undo(new Dictionary<Guid, int?>(previous)))));
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
    {
        // Legacy undo from older builds is a bare int ("0"). And whatever the format, an
        // undo that "restores" the ON value is poisoned — recorded by an apply that ran
        // while the tweak was already applied. Reverting to it makes the toggle snap
        // back on, so it gets the Windows default instead.
        if (int.TryParse(undoData, out var legacy))
        {
            return RestoreEverywhere(legacy == TargetValue ? WindowsDefault : legacy);
        }
        try
        {
            var undo = JsonSerializer.Deserialize<Undo>(undoData);
            if (undo?.PerScheme is { Count: > 0 } perScheme)
            {
                var sanitised = perScheme.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value == TargetValue ? WindowsDefault : kv.Value);
                _client.RestoreValueIndexPerScheme(
                    ProcessorSubgroupGuid, CoreParkingMinCoresGuid, sanitised, WindowsDefault);
                return Task.FromResult(TweakResult.Success());
            }
        }
        catch { /* unreadable → default below */ }
        return RestoreEverywhere(WindowsDefault);
    }

    /// <summary>Undo lost or applied outside the app: back to the Windows default.</summary>
    public Task<TweakResult> RevertToDefaultAsync(CancellationToken ct = default)
        => RestoreEverywhere(WindowsDefault);

    private Task<TweakResult> RestoreEverywhere(int value)
    {
        _client.RestoreValueIndexPerScheme(
            ProcessorSubgroupGuid, CoreParkingMinCoresGuid,
            new Dictionary<Guid, int?>(), value);
        return Task.FromResult(TweakResult.Success());
    }

    public Task<string> PreviewAsync(CancellationToken ct = default)
    {
        var current = _client.GetActiveSchemeSettingIndexFromRegistry(ProcessorSubgroupGuid, CoreParkingMinCoresGuid);
        return Task.FromResult($"Will set CPMINCORES to 100 on every power plan. Current value: {current?.ToString() ?? "default"}.");
    }
}
