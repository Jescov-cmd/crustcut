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
    public string DisplayName => "Hardware GPU scheduling";
    public string Description => "Moves GPU command scheduling off the CPU and onto the GPU. Helps on most modern NVIDIA / AMD cards; rarely hurts on older drivers — try it, reboot, and check Sentinel's CPU row. Reboot required.";
    public bool RequiresElevation => true;
    public bool IsDestructive => false;
    public bool RequiresReboot => true;

    public HwGpuSchedulingTweak(IRegistryClient registry) { _registry = registry; }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        // HwSchMode is a REG_DWORD (2 = on). Writing/reading it as a string left a
        // value the GPU driver ignores — so HAGS never actually turned on even though
        // the tile claimed "applied". Read it as a DWORD to match Windows.
        var v = _registry.ReadDword(RegistryHive.LocalMachine, SubKey, ValueName);
        return Task.FromResult(v == 2 ? TweakState.Applied : TweakState.NotApplied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var backup = _registry.WriteDword(RegistryHive.LocalMachine, SubKey, ValueName, 2);
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
        var current = _registry.ReadDword(RegistryHive.LocalMachine, SubKey, ValueName)?.ToString() ?? "(unset)";
        return Task.FromResult($"Will set HKLM\\{SubKey}\\{ValueName} from '{current}' to '2'. Reboot required.");
    }
}
