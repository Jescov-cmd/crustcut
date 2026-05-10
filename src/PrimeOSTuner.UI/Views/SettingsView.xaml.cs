using System.Windows;
using System.Windows.Controls;
using PrimeOSTuner.UI.ViewModels;

namespace PrimeOSTuner.UI.Views;

public partial class SettingsView : UserControl
{
    private readonly SettingsViewModel _vm;

    public SettingsView(SettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    private async void OptimizeNowClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            btn.IsEnabled = false;
            try { await _vm.RunRamCleanupNowAsync(); }
            finally { btn.IsEnabled = true; }
        }
    }
}
