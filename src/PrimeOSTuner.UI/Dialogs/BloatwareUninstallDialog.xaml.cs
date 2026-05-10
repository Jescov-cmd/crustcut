using System.Windows;
using PrimeOSTuner.Core.Bloatware;

namespace PrimeOSTuner.UI.Dialogs;

public partial class BloatwareUninstallDialog : Window
{
    public BloatwareUninstallDialog()
    {
        InitializeComponent();
    }

    public bool Confirmed { get; private set; }

    public void Configure(BloatwareItem item)
    {
        TitleText.Text = $"Uninstall {item.Entry.DisplayName}?";
        SubtitleText.Text = item.Entry.AppxName;
        if (item.Entry.Tier == SafetyTier.Risky && !string.IsNullOrEmpty(item.Entry.RiskNote))
        {
            RiskNoteText.Text = item.Entry.RiskNote;
            WarningBox.Visibility = Visibility.Visible;
        }
        else
        {
            // Safe tier: hide the warning box, show a simple confirmation only.
            WarningBox.Visibility = Visibility.Collapsed;
            RiskNoteText.Text = string.Empty;
        }
    }

    private void ConfirmClick(object sender, RoutedEventArgs e)
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
