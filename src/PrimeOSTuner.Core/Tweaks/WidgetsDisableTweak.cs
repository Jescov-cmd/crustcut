using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class WidgetsDisableTweak : ITweak
{
    private const string MachinePolicyKey = @"SOFTWARE\Policies\Microsoft\Dsh";
    private const string ExplorerAdvancedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

    private readonly IRegistryClient _registry;

    public string Id => "core.widgets-disable";
    public string DisplayName => "Disable News & Interests / Widgets";
    public string Description => "Removes the Win10 News & Interests bar and hides the Win11 Widgets taskbar button. Stops the widgets host service from running in the background.";
    public bool RequiresElevation => true;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    public WidgetsDisableTweak(IRegistryClient registry) { _registry = registry; }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        var newsAndInterests = _registry.ReadDword(RegistryHive.LocalMachine, MachinePolicyKey, "AllowNewsAndInterests");
        var taskbarWidgets = _registry.ReadDword(RegistryHive.CurrentUser, ExplorerAdvancedKey, "TaskbarDa");

        var applied = newsAndInterests == 0 && taskbarWidgets == 0;
        return Task.FromResult(applied ? TweakState.Applied : TweakState.NotApplied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var backups = new List<RegistryBackup>
        {
            _registry.WriteDword(RegistryHive.LocalMachine, MachinePolicyKey, "AllowNewsAndInterests", 0),
            _registry.WriteDword(RegistryHive.CurrentUser, ExplorerAdvancedKey, "TaskbarDa", 0),
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
            "Will set HKLM Dsh\\AllowNewsAndInterests=0 (Win10) and HKCU Explorer\\Advanced\\TaskbarDa=0 (Win11).");
    }
}
