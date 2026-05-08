using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Games;

namespace PrimeOSTuner.UI.ViewModels;

public partial class GameTileViewModel : ObservableObject
{
    [ObservableProperty] private string? _coverImagePath;
    [ObservableProperty] private bool _isLoadingCover;
    [ObservableProperty] private string _assignedMode = "(none)";
    [ObservableProperty] private bool _isRunning;

    public KnownGame Game { get; }

    public string DisplayName => Game.DisplayName;
    public string Id => Game.Id;

    public GameTileViewModel(KnownGame game) { Game = game; IsLoadingCover = true; }
}
