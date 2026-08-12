using System.Text.Json;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

/// <summary>
/// Disables CPU turbo boost (powercfg PERFBOOSTMODE = 0). Modern Ryzen/Intel chips
/// deliberately boost until they hit their thermal limit — the CPU running at 75-80°C at
/// light load is boost working as designed, and it's why fans spin up. Turning boost off
/// drops temperatures 10-15°C and lets quiet fan curves actually be quiet, at the cost of
/// a few percent peak burst speed. Sustained (base-clock) performance is unaffected.
/// PERFBOOSTMODE is a HIDDEN power setting: probe reads the registry directly because
/// powercfg /query refuses to show it (same story as CPU core parking).
/// </summary>
public sealed class CpuBoostLimitTweak : ITweak, ICategorizedTweak, ISelfRevertingTweak
{
    private const string SubProcessor = "54533251-82be-4824-96c1-47b60b740d00";
    private const string PerfBoostMode = "be337238-0d82-4146-a960-4f3749d470c7";
    private const int Disabled = 0;
    private const int WindowsDefault = 2;   // "Aggressive" — what shipped power plans use

    private readonly IPowerPlanClient _power;

    public string Id => "core.cpu-boost-limit";
    public string DisplayName => "Quiet CPU (limit turbo boost)";
    public string Description =>
        "Stops the CPU boosting into its thermal limit. Boost is why a modern CPU sits at " +
        "75-80° doing light work — and why fans ramp. Off: 10-15° cooler, quieter fans, " +
        "a few percent slower in short bursts. Sustained speed is unaffected.";
    public string Category => "power";
    public string? RiskNote => "Slightly lower peak CPU speed";
    public bool RequiresElevation => true;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    public CpuBoostLimitTweak(IPowerPlanClient power) => _power = power;

    private sealed record Undo(int? Ac, int? Dc);

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        try
        {
            var ac = _power.GetActiveSchemeSettingIndexFromRegistry(SubProcessor, PerfBoostMode);
            return Task.FromResult(ac == Disabled ? TweakState.Applied : TweakState.NotApplied);
        }
        catch
        {
            return Task.FromResult(TweakState.Unknown);
        }
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        try
        {
            var undo = new Undo(
                _power.GetActiveSchemeSettingIndexFromRegistry(SubProcessor, PerfBoostMode),
                null);
            _power.RunPowercfg($"/setacvalueindex SCHEME_CURRENT {SubProcessor} {PerfBoostMode} {Disabled}");
            _power.RunPowercfg($"/setdcvalueindex SCHEME_CURRENT {SubProcessor} {PerfBoostMode} {Disabled}");
            _power.RunPowercfg("/setactive SCHEME_CURRENT");
            return Task.FromResult(TweakResult.Success(JsonSerializer.Serialize(undo),
                "Turbo boost off — temperatures start dropping within a minute."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(TweakResult.Failure(ex.Message));
        }
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
    {
        try
        {
            var undo = JsonSerializer.Deserialize<Undo>(undoData);
            return Restore(undo?.Ac ?? WindowsDefault);
        }
        catch (Exception ex)
        {
            return Task.FromResult(TweakResult.Failure(ex.Message));
        }
    }

    /// <summary>Applied outside the app or undo lost: back to the Windows default.</summary>
    public Task<TweakResult> RevertToDefaultAsync(CancellationToken ct = default)
        => Restore(WindowsDefault);

    private Task<TweakResult> Restore(int value)
    {
        _power.RunPowercfg($"/setacvalueindex SCHEME_CURRENT {SubProcessor} {PerfBoostMode} {value}");
        _power.RunPowercfg($"/setdcvalueindex SCHEME_CURRENT {SubProcessor} {PerfBoostMode} {value}");
        _power.RunPowercfg("/setactive SCHEME_CURRENT");
        return Task.FromResult(TweakResult.Success());
    }

    public Task<string> PreviewAsync(CancellationToken ct = default)
        => Task.FromResult("Will set processor boost mode (PERFBOOSTMODE) to Disabled on the active power plan (undo restores the previous mode).");
}
