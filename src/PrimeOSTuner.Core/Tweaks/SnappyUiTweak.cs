using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class SnappyUiTweak : ITweak
{
    private const string SubKey = @"Control Panel\Desktop";

    private static readonly (string Name, string Value)[] Targets =
    {
        ("MenuShowDelay", "0"),
        ("HungAppTimeout", "1000"),
        ("WaitToKillAppTimeout", "5000"),
        ("AutoEndTasks", "1"),
    };

    private readonly IRegistryClient _registry;

    public string Id => "core.snappy-ui";
    public string DisplayName => "Snappier UI timeouts";
    public string Description => "Drops menu show delay to 0 ms and shortens hung-app timeouts so frozen windows close in seconds instead of half a minute.";
    public bool RequiresElevation => false;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    public SnappyUiTweak(IRegistryClient registry) { _registry = registry; }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        foreach (var (name, expected) in Targets)
        {
            var current = _registry.ReadString(RegistryHive.CurrentUser, SubKey, name);
            if (current != expected)
                return Task.FromResult(TweakState.NotApplied);
        }
        return Task.FromResult(TweakState.Applied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var backups = new List<RegistryBackup>();
        foreach (var (name, value) in Targets)
        {
            backups.Add(_registry.WriteString(RegistryHive.CurrentUser, SubKey, name, value));
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
        return Task.FromResult(
            "Will set HKCU Control Panel\\Desktop: MenuShowDelay=0, HungAppTimeout=1000, WaitToKillAppTimeout=5000, AutoEndTasks=1.");
    }
}
