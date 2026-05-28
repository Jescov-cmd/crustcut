using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Extensions.DependencyInjection;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.UI.Dialogs;
using PrimeOSTuner.UI.ViewModels;

namespace PrimeOSTuner.UI.Views;

public partial class GameLibraryView : UserControl
{
    private readonly GameLibraryViewModel _vm;
    private readonly GameProfileStore _profiles;
    private readonly AddedGamesStore _added;

    /// <summary>Exposed so the XAML can `{Binding GameBoostVm, ElementName=Root}` it.</summary>
    public GameBoostViewModel GameBoostVm { get; }

    public GameLibraryView(
        GameLibraryViewModel vm,
        GameProfileStore profiles,
        AddedGamesStore added,
        GameBoostViewModel gameBoostVm)
    {
        InitializeComponent();
        _vm = vm;
        _profiles = profiles;
        _added = added;
        GameBoostVm = gameBoostVm;
        DataContext = vm;
        Loaded += async (_, _) => await vm.LoadAsync();
    }

    private async void GameCard_ProfileChanged(object? sender, (string GameId, string ModeName) e)
    {
        if (e.ModeName == "(none)")
            await _profiles.ClearProfileForAsync(e.GameId);
        else
            await _profiles.SetProfileForAsync(e.GameId, e.ModeName);
    }

    private async void AddGameClick(object sender, RoutedEventArgs e)
    {
        var sp = ((App)Application.Current).Host.Services;
        var dialog = sp.GetRequiredService<AddGameDialog>();
        if (dialog.ShowDialog() == true && dialog.Result is not null)
        {
            await _added.AddAsync(dialog.Result);
            await _vm.LoadAsync();
        }
    }

    // ---- Boost mode toggles (folded in from the old Game Boost tab) ----

    private async void BasicToggleClick(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { IsChecked: true }) await GameBoostVm.ApplyBasicAsync();
    }

    private async void PerformanceToggleClick(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { IsChecked: true }) await GameBoostVm.ApplyPerformanceAsync();
    }

    private async void AggressiveToggleClick(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { IsChecked: true }) await GameBoostVm.ApplyAggressiveAsync();
    }
}
