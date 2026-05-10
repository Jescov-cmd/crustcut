using System.Windows;

namespace PrimeOSTuner.UI.Dialogs;

public partial class BulkApplyGamesDialog : Window
{
    public bool Confirmed { get; private set; }

    public BulkApplyGamesDialog()
    {
        InitializeComponent();
    }

    public void Configure(int gameCount)
    {
        GameCountText.Text = gameCount == 1
            ? "This will add 1 detected game to Memory Priority with these settings:"
            : $"This will add {gameCount} detected games to Memory Priority with these settings:";
    }

    private void ApplyClick(object sender, RoutedEventArgs e)
    {
        Confirmed = true;
        Close();
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }
}
