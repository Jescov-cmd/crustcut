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
    public string Description => "Adds Microsoft's hidden Ultimate Performance power plan to Windows. On its own this changes nothing — it just makes the plan available. Use \"Use the fastest power plan\" to actually switch to it.";
    public string Category => "power";
    public string? RiskNote => "Runs hotter: the Ultimate Performance plan keeps components at high power for max speed, which means more heat and fan noise.";
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

    /// <summary>The Balanced plan every Windows install ships with.</summary>
    private static readonly Guid BalancedGuid = new("381b4222-f694-41f0-9685-ff5bb260df2e");

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
    {
        // Windows refuses to delete the ACTIVE power plan. The old code "handled" that by
        // silently skipping it — so if the user was ON Ultimate Performance (the normal
        // case, since enabling it is pointless without switching to it), the toggle
        // reported success while removing nothing, and the user was told to go fix it in
        // Control Panel themselves. Step off the plan first, then delete.
        var ultimates = _power.ListPlans()
            .Where(p => p.Name.Equals("Ultimate Performance", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (ultimates.Count == 0) return Task.FromResult(TweakResult.Success());

        Guid? active = null;
        try { active = _power.GetActivePlan().Guid; } catch { /* best effort */ }

        var switched = false;
        if (active is Guid a && ultimates.Any(p => p.Guid == a))
        {
            try
            {
                _power.SetActivePlan(BalancedGuid);
                switched = true;
            }
            catch (Exception ex)
            {
                // No Balanced plan (heavily customised machine): fall back to any
                // non-Ultimate plan rather than telling the user to do it by hand.
                var fallback = _power.ListPlans().FirstOrDefault(p =>
                    !p.Name.Equals("Ultimate Performance", StringComparison.OrdinalIgnoreCase));
                if (fallback is null)
                    return Task.FromResult(TweakResult.Failure(
                        $"Couldn't switch off the Ultimate Performance plan: {ex.Message}"));
                _power.SetActivePlan(fallback.Guid);
                switched = true;
            }
        }

        foreach (var plan in ultimates)
        {
            try { _power.RunPowercfg($"/delete {plan.Guid:D}"); }
            catch { /* already gone — the goal is simply "none present" */ }
        }
        return Task.FromResult(TweakResult.Success(null,
            switched ? "Switched to the Balanced plan and removed Ultimate Performance." : null));
    }

    public Task<string> PreviewAsync(CancellationToken ct = default)
        => Task.FromResult("Will run 'powercfg /duplicatescheme' to add Ultimate Performance.");
}
