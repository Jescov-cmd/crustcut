using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Tweaks;

namespace Crustcut.Presentation;

public partial class TweakRowVm : ObservableObject
{
    public ITweak Tweak { get; }
    public string CategoryKey { get; }
    public string Category { get; }
    public string? RiskNote { get; }

    [ObservableProperty] private bool _isApplied;
    [ObservableProperty] private bool _isBusy;

    public string? UndoData { get; set; }

    public string DisplayName => Tweak.DisplayName;
    public string Description => Tweak.Description;
    public bool RequiresReboot => Tweak.RequiresReboot;

    public TweakRowVm(ITweak tweak)
    {
        Tweak = tweak;
        var (key, label) = CategoryFor(tweak);
        CategoryKey = key;
        Category = label;
        RiskNote = (tweak as ICategorizedTweak)?.RiskNote;
    }

    public bool HasRisk => !string.IsNullOrEmpty(RiskNote);

    // A heat-causing tweak is one whose risk note warns it runs the system hotter (the power
    // plan tweaks). Detected from the note text so any future "runs hotter" tweak is covered
    // without a per-tweak flag.
    public bool CausesHeat =>
        RiskNote is not null &&
        (RiskNote.Contains("hotter", StringComparison.OrdinalIgnoreCase) ||
         RiskNote.Contains("heat", StringComparison.OrdinalIgnoreCase) ||
         RiskNote.Contains("thermal", StringComparison.OrdinalIgnoreCase));

    // Generic caution pill shows only for non-heat risks — heat gets its own badge.
    public bool HasOtherRisk => HasRisk && !CausesHeat;

    public static (string Key, string Label) CategoryFor(ITweak tweak)
    {
        if (tweak is ICategorizedTweak cat)
        {
            return cat.Category switch
            {
                "fps" => ("fps", "FPS & Latency"),
                "network" => ("network", "Network"),
                "system" => ("system", "System"),
                "privacy" => ("privacy", "Privacy"),
                "power" => ("power", "Power"),
                _ => ("fps", "FPS & Latency")
            };
        }

        // Fallback for legacy tweaks (no Category property).
        if (tweak.Id.StartsWith("game.nagle") || tweak.Id.StartsWith("game.network"))
            return ("network", "Network");
        return ("fps", "FPS & Latency");
    }
}
