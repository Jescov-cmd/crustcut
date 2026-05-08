using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class HwGpuSchedulingTweak : ITweak
{
    private const string SubKey = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";
    private const string ValueName = "HwSchMode";

    private readonly IRegistryClient _registry;

    public string Id => "game.hw-gpu-scheduling";
    public string DisplayName => "Enable Hardware-accelerated GPU Scheduling";
    public string Description => "Sets HwSchMode=2 so the GPU manages its own scheduling, reducing CPU overhead. Requires admin and a reboot to take effect.";
    public bool RequiresElevation => true;
    public bool IsDestructive => false;

    public HwGpuSchedulingTweak(IRegistryClient registry) { _registry = registry; }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        var v = _registry.ReadString(RegistryHive.LocalMachine, SubKey, ValueName);
        return Task.FromResult(v == "2" ? TweakState.Applied : TweakState.NotApplied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var backup = _registry.WriteString(RegistryHive.LocalMachine, SubKey, ValueName, "2");
        return Task.FromResult(TweakResult.Success(JsonSerializer.Serialize(backup)));
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
    {
        var backup = JsonSerializer.Deserialize<RegistryBackup>(undoData)
            ?? throw new InvalidOperationException("Invalid undo data");
        _registry.RestoreFromBackup(backup);
        return Task.FromResult(TweakResult.Success());
    }

    public Task<string> PreviewAsync(CancellationToken ct = default)
    {
        var current = _registry.ReadString(RegistryHive.LocalMachine, SubKey, ValueName) ?? "(unset)";
        return Task.FromResult($"Will set HKLM\\{SubKey}\\{ValueName} from '{current}' to '2'. Reboot required.");
    }
}
