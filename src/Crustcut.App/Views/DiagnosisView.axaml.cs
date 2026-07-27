using Avalonia.Controls;
using Avalonia.Interactivity;
using Crustcut.Presentation;

namespace Crustcut.App.Views;

public partial class DiagnosisView : UserControl
{
    public DiagnosisView() => InitializeComponent();

    private async void ScanClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DiagnosisViewModel vm) await vm.ScanAsync();
    }
}
