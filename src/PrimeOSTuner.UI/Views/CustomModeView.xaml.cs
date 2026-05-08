using System.Windows;
using System.Windows.Controls;
using PrimeOSTuner.UI.ViewModels;

namespace PrimeOSTuner.UI.Views;

public partial class CustomModeView : UserControl
{
    private readonly CustomModeViewModel _vm;

    public CustomModeView(CustomModeViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        Loaded += async (_, _) => await vm.LoadAsync();
    }

    private async void SaveClick(object sender, RoutedEventArgs e)
    {
        await _vm.SaveAsync();
        MessageBox.Show("Custom Mode saved. Apply it from Game Boost or assign to a game in Game Library.", "Saved");
    }
}
