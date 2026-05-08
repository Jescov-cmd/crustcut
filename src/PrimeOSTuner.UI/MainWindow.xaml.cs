using System.Windows;
using System.Windows.Controls;

namespace PrimeOSTuner.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ShowPlaceholder("Dashboard");
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tab }) ShowPlaceholder(tab);
    }

    private void ShowPlaceholder(string tab)
    {
        PageHost.Content = new TextBlock
        {
            Text = $"{tab} (placeholder)",
            FontSize = 22,
            Foreground = (System.Windows.Media.Brush)FindResource("Text0Brush")
        };
    }
}
