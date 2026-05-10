using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class NicPowerManagementTweak : ITweak, ICategorizedTweak
{
    private const string ClassRoot = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";
    private const int DisablePowerDownBit = 0x100;

    private readonly IRegistryClient _registry;

    public NicPowerManagementTweak(IRegistryClient registry) { _registry = registry; }

    public string Id => "core.nic-power-mgmt";
    public string DisplayName => "Disable NIC power management";
    public string Description => "Stops Windows from turning the network adapter off to save power.";
    public string Category => "network";
    public string? RiskNote => "Slightly higher idle power on laptops.";
    public bool RequiresElevation => true;
    public bool IsDestructive => false;
    public bool RequiresReboot => true;

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        foreach (var subkey in EnumNicSubkeys())
        {
            var v = _registry.ReadDword(RegistryHive.LocalMachine, subkey, "PnPCapabilities") ?? 0;
            if ((v & DisablePowerDownBit) == 0) return Task.FromResult(TweakState.NotApplied);
        }
        return Task.FromResult(TweakState.Applied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var backups = new List<RegistryBackup>();
        foreach (var subkey in EnumNicSubkeys())
        {
            var current = _registry.ReadDword(RegistryHive.LocalMachine, subkey, "PnPCapabilities") ?? 0;
            var newVal = current | DisablePowerDownBit;
            backups.Add(_registry.WriteDword(RegistryHive.LocalMachine, subkey, "PnPCapabilities", newVal));
        }
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
        var nics = EnumNicSubkeys().Count();
        return Task.FromResult($"Will set PnPCapabilities |= 0x100 on {nics} network adapter subkey(s).");
    }

    private static IEnumerable<string> EnumNicSubkeys()
    {
        // Enumerate the four-digit numeric subkeys under the Network Adapter class.
        using var classKey = Registry.LocalMachine.OpenSubKey(ClassRoot);
        if (classKey is null) yield break;
        foreach (var name in classKey.GetSubKeyNames())
        {
            if (name.Length == 4 && int.TryParse(name, out _))
                yield return $"{ClassRoot}\\{name}";
        }
    }
}
