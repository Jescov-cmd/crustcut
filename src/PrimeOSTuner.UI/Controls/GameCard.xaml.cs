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
        DataContextChanged += (_, _) => SyncCombo();
    }

    private void SyncCombo()
    {
        if (DataContext is GameTileViewModel vm)
        {
            for (int i = 0; i < ProfileCombo.Items.Count; i++)
            {
                if (((ComboBoxItem)ProfileCombo.Items[i]).Content?.ToString() == vm.AssignedMode)
                {
                    ProfileCombo.SelectedIndex = i;
                    return;
                }
            }
            ProfileCombo.SelectedIndex = 0;
        }
    }

    private void ProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not GameTileViewModel vm) return;
        if (ProfileCombo.SelectedItem is not ComboBoxItem item) return;
        var mode = item.Content?.ToString() ?? "(none)";
        if (vm.AssignedMode == mode) return;
        vm.AssignedMode = mode;
        ProfileChanged?.Invoke(this, (vm.Id, mode));
    }
}
