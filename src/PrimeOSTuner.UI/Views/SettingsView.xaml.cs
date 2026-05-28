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
        if (sender is not Button btn) return;

        var originalContent = btn.Content;
        btn.IsEnabled = false;
        btn.Content = "Trimming…";
        try
        {
            await _vm.RunRamCleanupNowAsync();
            btn.Content = "✓ Trimmed";
            await Task.Delay(1500);
        }
        finally
        {
            btn.Content = originalContent;
            btn.IsEnabled = true;
        }
    }
}
