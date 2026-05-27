using System.Windows;
using System.Windows.Controls;
using PrimeOSTuner.UI.ViewModels;

namespace PrimeOSTuner.UI.Controls;

public partial class GameCard : UserControl
{
    public event EventHandler<(string GameId, string ModeName)>? ProfileChanged;

    public GameCard()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => SyncPills();
    }

    private void SyncPills()
    {
        if (DataContext is not GameTileViewModel vm) return;
        for (int i = 0; i < ProfilePills.Items.Count; i++)
        {
            if (ProfilePills.Items[i] is string mode && mode == vm.AssignedMode)
            {
                ProfilePills.SelectedIndex = i;
                return;
            }
        }
        ProfilePills.SelectedIndex = 0;
    }

    private void ProfilePills_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not GameTileViewModel vm) return;
        if (ProfilePills.SelectedItem is not string mode) return;
        if (vm.AssignedMode == mode) return;
        vm.AssignedMode = mode;
        ProfileChanged?.Invoke(this, (vm.Id, mode));
    }
}
