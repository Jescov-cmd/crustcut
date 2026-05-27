using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Memory;

namespace PrimeOSTuner.UI.ViewModels;

public partial class PriorityRuleVm : ObservableObject
{
    [ObservableProperty] private string _displayName;
    [ObservableProperty] private string _exePath;
    [ObservableProperty] private PriorityLevel _priority;
    [ObservableProperty] private bool _protectFromRamCleanup;
    [ObservableProperty] private bool _gameBooster;
    [ObservableProperty] private bool _isGame;
    [ObservableProperty] private string _statusText = "Idle";
    [ObservableProperty] private string _statusColor = "#888";

    // UI-only — used by Memory Priority's multi-select mode.
    [ObservableProperty] private bool _isSelected;

    public string ExeName => Path.GetFileName(ExePath);

    public PriorityRuleVm(PriorityRule rule)
    {
        _displayName = rule.DisplayName;
        _exePath = rule.ExePath;
        _priority = rule.Priority;
        _protectFromRamCleanup = rule.ProtectFromRamCleanup;
        _gameBooster = rule.GameBooster;
        _isGame = rule.IsGame;
    }

    public PriorityRule ToRule() => new(
        ExePath, DisplayName, Priority, ProtectFromRamCleanup, GameBooster, IsGame);
}
