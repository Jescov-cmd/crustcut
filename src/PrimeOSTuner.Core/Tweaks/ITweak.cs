using System.Threading;
using System.Threading.Tasks;

namespace PrimeOSTuner.Core.Tweaks;

public interface ITweak
{
    string Id { get; }                 // stable identifier, e.g. "core.power-plan"
    string DisplayName { get; }        // shown in UI
    string Description { get; }        // plain-language explanation
    bool RequiresElevation { get; }    // does Apply need admin?
    bool IsDestructive { get; }        // requires manual opt-in (never auto-run)
    // Every concrete tweak must declare this. Default interface members don't bind
    // reliably in WPF — when a class skips this, the REBOOT pill shows on every tile.
    bool RequiresReboot { get; }

    Task<TweakState> ProbeAsync(CancellationToken ct = default);
    Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default);
    Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default);
    Task<string> PreviewAsync(CancellationToken ct = default);
}
