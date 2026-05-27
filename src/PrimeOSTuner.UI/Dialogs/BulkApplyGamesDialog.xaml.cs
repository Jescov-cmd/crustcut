using System.Windows;

namespace PrimeOSTuner.UI.Dialogs;

public partial class BulkApplyGamesDialog : Window
{
    public bool Confirmed { get; private set; }

    public BulkApplyGamesDialog()
    {
        InitializeComponent();
    }

    public void Configure(int newCount, int updateCount)
    {
        var parts = new List<string>();
        if (newCount > 0)
            parts.Add(newCount == 1 ? "add 1 detected game" : $"add {newCount} detected games");
        if (updateCount > 0)
            parts.Add(updateCount == 1
                ? "reset 1 existing rule to recommended"
                : $"reset {updateCount} existing rules to recommended");

        GameCountText.Text = parts.Count == 0
            ? "Everything is already on the recommended settings."
            : "This will " + string.Join(" and ", parts) + " with these settings:";
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
