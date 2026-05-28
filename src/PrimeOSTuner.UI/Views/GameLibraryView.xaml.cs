using System.Windows;
using System.Windows.Controls;
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

    public GameLibraryView(GameLibraryViewModel vm, GameProfileStore profiles, AddedGamesStore added)
    {
        InitializeComponent();
        _vm = vm;
        _profiles = profiles;
        _added = added;
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
}
