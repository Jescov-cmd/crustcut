using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class VisualEffectsTweak : ITweak
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string WindowMetricsKey = @"Control Panel\Desktop\WindowMetrics";
    private const string ExplorerAdvancedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

    private readonly IRegistryClient _registry;

    public string Id => "core.visual-effects";
    public string DisplayName => "Disable animations & transparency";
    public string Description => "Turns off window animations, taskbar animations, and acrylic transparency. Snappier UI, real GPU/CPU savings on weak hardware.";
    public bool RequiresElevation => false;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    public VisualEffectsTweak(IRegistryClient registry) { _registry = registry; }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        var transparency = _registry.ReadDword(RegistryHive.CurrentUser, PersonalizeKey, "EnableTransparency");
        var taskbarAnim = _registry.ReadDword(RegistryHive.CurrentUser, ExplorerAdvancedKey, "TaskbarAnimations");
        var minAnimate = _registry.ReadString(RegistryHive.CurrentUser, WindowMetricsKey, "MinAnimate");

        var applied = transparency == 0 && taskbarAnim == 0 && minAnimate == "0";
        return Task.FromResult(applied ? TweakState.Applied : TweakState.NotApplied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var backups = new List<RegistryBackup>
        {
            _registry.WriteDword(RegistryHive.CurrentUser, PersonalizeKey, "EnableTransparency", 0),
            _registry.WriteDword(RegistryHive.CurrentUser, ExplorerAdvancedKey, "TaskbarAnimations", 0),
            _registry.WriteDword(RegistryHive.CurrentUser, ExplorerAdvancedKey, "ListviewAlphaSelect", 0),
            _registry.WriteDword(RegistryHive.CurrentUser, ExplorerAdvancedKey, "ListviewShadow", 0),
            _registry.WriteString(RegistryHive.CurrentUser, WindowMetricsKey, "MinAnimate", "0"),
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
            "Will set HKCU EnableTransparency=0, TaskbarAnimations=0, ListviewAlphaSelect=0, ListviewShadow=0, MinAnimate=\"0\".");
    }
}
