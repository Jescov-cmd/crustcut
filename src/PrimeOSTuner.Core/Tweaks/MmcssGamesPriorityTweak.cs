using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class MmcssGamesPriorityTweak : ITweak
{
    private const string SubKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";

    private readonly IRegistryClient _registry;

    public string Id => "game.mmcss-games-priority";
    public string DisplayName => "Raise MMCSS Games task priority";
    public string Description => "Pushes the multimedia 'Games' scheduler class to High priority. Pairs with maximised game CPU priority to reduce audio/input scheduling latency.";
    public bool RequiresElevation => true;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    public MmcssGamesPriorityTweak(IRegistryClient registry) { _registry = registry; }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        var priority = _registry.ReadDword(RegistryHive.LocalMachine, SubKey, "Priority");
        var schedCat = _registry.ReadString(RegistryHive.LocalMachine, SubKey, "Scheduling Category");
        var sfio = _registry.ReadString(RegistryHive.LocalMachine, SubKey, "SFIO Priority");

        var applied = priority == 6
            && string.Equals(schedCat, "High", StringComparison.OrdinalIgnoreCase)
            && string.Equals(sfio, "High", StringComparison.OrdinalIgnoreCase);
        return Task.FromResult(applied ? TweakState.Applied : TweakState.NotApplied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var backups = new List<RegistryBackup>
        {
            _registry.WriteDword(RegistryHive.LocalMachine, SubKey, "Priority", 6),
            _registry.WriteString(RegistryHive.LocalMachine, SubKey, "Scheduling Category", "High"),
            _registry.WriteString(RegistryHive.LocalMachine, SubKey, "SFIO Priority", "High"),
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
            $"Will set HKLM\\{SubKey}: Priority=6, Scheduling Category=High, SFIO Priority=High.");
    }
}
