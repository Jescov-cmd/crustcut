using System.Text.Json;
using System.Text.RegularExpressions;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class UltimatePerformanceTweak : ITweak, ICategorizedTweak
{
    private const string UltimateGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";
    private static readonly Regex GuidRx = new("(?<guid>[0-9a-f-]{36})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IPowerPlanClient _power;

    public UltimatePerformanceTweak(IPowerPlanClient power) { _power = power; }

    public string Id => "core.ultimate-performance";
    public string DisplayName => "Enable Ultimate Performance power plan";
    public string Description => "Adds Microsoft's hidden Ultimate Performance power plan (does not switch to it).";
    public string Category => "power";
    public string? RiskNote => null;
    public bool RequiresElevation => true;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        try
        {
            var list = _power.RunPowercfg("/list");
            return Task.FromResult(list.Contains("Ultimate Performance", StringComparison.OrdinalIgnoreCase)
                ? TweakState.Applied : TweakState.NotApplied);
        }
        catch
        {
            return Task.FromResult(TweakState.Unknown);
        }
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        // Idempotent: if an Ultimate Performance scheme already exists, reuse it instead
        // of duplicating. The old code ran /duplicatescheme on every apply, so repeated
        // applies spawned a pile of identical "Ultimate Performance" schemes.
        var existing = _power.ListPlans()
            .FirstOrDefault(p => p.Name.Equals("Ultimate Performance", StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return Task.FromResult(TweakResult.Success(JsonSerializer.Serialize(existing.Guid.ToString("D"))));

        var output = _power.RunPowercfg($"/duplicatescheme {UltimateGuid}");
        var match = GuidRx.Match(output);
        if (!match.Success)
            return Task.FromResult(TweakResult.Failure("powercfg did not return a new GUID."));
        return Task.FromResult(TweakResult.Success(JsonSerializer.Serialize(match.Groups["guid"].Value)));
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
    {
        // Remove every Ultimate Performance scheme present (older builds could create
        // several). Skip the active scheme — powercfg can't delete it — and treat a
        // "doesn't exist" failure as success, since the goal is simply "none present".
        Guid? active = null;
        try { active = _power.GetActivePlan().Guid; } catch { /* best effort */ }

        foreach (var plan in _power.ListPlans()
                     .Where(p => p.Name.Equals("Ultimate Performance", StringComparison.OrdinalIgnoreCase)))
        {
            if (active == plan.Guid) continue;
            try { _power.RunPowercfg($"/delete {plan.Guid:D}"); }
            catch { /* already gone, or in use — ignore; revert is best-effort cleanup */ }
        }
        return Task.FromResult(TweakResult.Success());
    }

    public Task<string> PreviewAsync(CancellationToken ct = default)
        => Task.FromResult("Will run 'powercfg /duplicatescheme' to add Ultimate Performance.");
}
