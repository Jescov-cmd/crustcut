using System.Windows;
using System.Windows.Controls;
using PrimeOSTuner.UI.ViewModels;

namespace PrimeOSTuner.UI.Views;

public partial class GameBoostView : UserControl
{
    private readonly GameBoostViewModel _vm;

    public GameBoostView(GameBoostViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    private async void ApplyBasicClick(object sender, RoutedEventArgs e) => await _vm.ApplyBasicAsync();
    private async void ApplyPerformanceClick(object sender, RoutedEventArgs e) => await _vm.ApplyPerformanceAsync();
    private async void ApplyCustomClick(object sender, RoutedEventArgs e) => await _vm.ApplyCustomAsync();
}
