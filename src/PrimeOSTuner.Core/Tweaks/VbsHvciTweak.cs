using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

/// <summary>
/// Disables Virtualization-Based Security and HVCI (Memory Integrity).
/// Real 5–15 % FPS recovery on most Windows 11 installs where VBS shipped
/// enabled by default, but it IS a security feature — so this is marked
/// destructive (opt-in only, never part of a one-click profile).
/// </summary>
public sealed class VbsHvciTweak : ITweak
{
    private const string DeviceGuardKey = @"SYSTEM\CurrentControlSet\Control\DeviceGuard";
    private const string HvciKey = @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity";

    private readonly IRegistryClient _registry;

    public string Id => "core.vbs-hvci-disable";
    public string DisplayName => "Disable VBS / Memory Integrity";
    public string Description => "Turns off Virtualization-Based Security and HVCI. 5–15% FPS win on Win11 — but this is a security feature. Reboot required.";
    public bool RequiresElevation => true;
    public bool IsDestructive => true;   // security disable — opt-in only
    public bool RequiresReboot => true;

    public VbsHvciTweak(IRegistryClient registry) { _registry = registry; }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        var vbs = _registry.ReadDword(RegistryHive.LocalMachine, DeviceGuardKey, "EnableVirtualizationBasedSecurity");
        var hvci = _registry.ReadDword(RegistryHive.LocalMachine, HvciKey, "Enabled");
        var applied = vbs == 0 && hvci == 0;
        return Task.FromResult(applied ? TweakState.Applied : TweakState.NotApplied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var backups = new List<RegistryBackup>
        {
            _registry.WriteDword(RegistryHive.LocalMachine, DeviceGuardKey, "EnableVirtualizationBasedSecurity", 0),
            _registry.WriteDword(RegistryHive.LocalMachine, HvciKey, "Enabled", 0),
        };
        return Task.FromResult(TweakResult.Success(JsonSerializer.Serialize(backups)));
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
    {
        var backups = JsonSerializer.Deserialize<List<RegistryBackup>>(undoData)
            ?? throw new InvalidOperationException("Invalid undo data");
        foreach (var b in backups) _registry.RestoreFromBackup(b);
        return Task.FromResult(TweakResult.Success());
    }

    public Task<string> PreviewAsync(CancellationToken ct = default)
    {
        return Task.FromResult(
            "Will set HKLM DeviceGuard\\EnableVirtualizationBasedSecurity=0 and HVCI\\Enabled=0. Requires reboot.");
    }
}
