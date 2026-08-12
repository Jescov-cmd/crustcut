using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Memory;

namespace Crustcut.Presentation;

/// <summary>One choice in the per-app memory-limit dropdown.</summary>
public sealed record MemoryLimitOption(string Label, int? Mb)
{
    public override string ToString() => Label;
}

public partial class PriorityRuleVm : ObservableObject
{
    /// <summary>Preset caps. 256 MB is the floor — below that most modern apps thrash.</summary>
    public static IReadOnlyList<MemoryLimitOption> MemoryLimitOptions { get; } = new MemoryLimitOption[]
    {
        new("No limit", null),
        new("256 MB", 256),
        new("512 MB", 512),
        new("1 GB", 1024),
        new("2 GB", 2048),
        new("4 GB", 4096),
    };

    [ObservableProperty] private string _displayName;
    [ObservableProperty] private string _exePath;
    [ObservableProperty] private PriorityLevel _priority;
    [ObservableProperty] private bool _protectFromRamCleanup;
    [ObservableProperty] private bool _gameBooster;
    [ObservableProperty] private bool _isGame;
    // Nullable: a re-binding ComboBox transiently writes null through the TwoWay binding
    // before restoring the real selection. Null means "No limit" everywhere it's read.
    [ObservableProperty] private MemoryLimitOption? _memoryLimit;
    [ObservableProperty] private string _statusText = "Idle";
    [ObservableProperty] private string _statusColor = "#888";

    // UI-only â€” used by Memory Priority's multi-select mode.
    [ObservableProperty] private bool _isSelected;

    public string ExeName => Path.GetFileName(ExePath);

    /// <summary>
    /// The rule as last persisted. Lets UpdateRuleAsync tell a REAL user edit apart from
    /// the phantom SelectionChanged every ComboBox fires when its ItemsSource re-binds on
    /// tab attach — those phantoms used to trigger a full process-table scan per row,
    /// which is what froze the Memory tab on every visit.
    /// </summary>
    public PriorityRule LastSaved { get; set; }

    public PriorityRuleVm(PriorityRule rule)
    {
        _displayName = rule.DisplayName;
        _exePath = rule.ExePath;
        _priority = rule.Priority;
        _protectFromRamCleanup = rule.ProtectFromRamCleanup;
        _gameBooster = rule.GameBooster;
        _isGame = rule.IsGame;
        // A stored value that isn't one of the presets falls back to "No limit" (and drops
        // the odd value on next save) — presets are the whole contract of the dropdown.
        _memoryLimit = MemoryLimitOptions.FirstOrDefault(o => o.Mb == rule.MemoryLimitMb)
            ?? MemoryLimitOptions[0];
        LimitAutoAssigned = rule.LimitAutoAssigned;
        LastSaved = rule;
    }

    public PriorityRule ToRule() => new(
        ExePath, DisplayName, Priority, ProtectFromRamCleanup, GameBooster, IsGame,
        MemoryLimit?.Mb, LimitAutoAssigned);

    /// <summary>Carried through edits so the engine can tell a cap it chose from one the
    /// user picked; only its own are revised automatically.</summary>
    public bool LimitAutoAssigned { get; set; }
}

