using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class HibernationTweak : ITweak
{
    private readonly IRegistryClient _registry;
    private readonly IPowerPlanClient _power;

    public HibernationTweak(IRegistryClient registry, IPowerPlanClient power)
    {
        _registry = registry;
        _power = power;
    }

    public string Id => "core.hibernation-disable";
    public string DisplayName => "Disable Hibernation";
    public string Description => "Frees ~8 GB on disk. Sleep still works; only hibernation is removed.";
    public string Category => "power";
    public string? RiskNote => null;
    public bool RequiresElevation => true;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        var v = _registry.ReadDword(RegistryHive.LocalMachine,
            "SYSTEM\\CurrentControlSet\\Control\\Power", "HibernateEnabled");
        return Task.FromResult(v == 0 ? TweakState.Applied : TweakState.NotApplied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var prev = _registry.ReadDword(RegistryHive.LocalMachine,
            "SYSTEM\\CurrentControlSet\\Control\\Power", "HibernateEnabled") ?? 1;
        _power.RunPowercfg("/h off");
        return Task.FromResult(TweakResult.Success(prev.ToString()));
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
    {
        if (int.TryParse(undoData, out var prev) && prev == 1)
            _power.RunPowercfg("/h on");
        return Task.FromResult(TweakResult.Success());
    }

    public Task<string> PreviewAsync(CancellationToken ct = default)
        => Task.FromResult("Will run 'powercfg /h off' to disable hibernation and remove hiberfil.sys.");
}
