using System.Threading;
using System.Threading.Tasks;

namespace PrimeOSTuner.Core.Tweaks;

public interface ITweak
{
    string Id { get; }                 // stable identifier, e.g. "core.junk-files"
    string DisplayName { get; }        // shown in UI
    string Description { get; }        // plain-language explanation
    bool RequiresElevation { get; }    // does Apply need admin?
    bool IsDestructive { get; }        // requires manual opt-in (never auto-run)

    Task<TweakState> ProbeAsync(CancellationToken ct = default);
    Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default);
    Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default);
    Task<string> PreviewAsync(CancellationToken ct = default);
}
