using System.IO;
using System.Windows;
using Microsoft.Win32;
using PrimeOSTuner.Core.Games;

namespace PrimeOSTuner.UI.Dialogs;

public partial class AddGameDialog : Window
{
    public KnownGame? Result { get; private set; }

    public AddGameDialog()
    {
        InitializeComponent();
    }

    private void BrowseClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Executable (*.exe)|*.exe",
            Title = "Select the game's executable"
        };
        if (dlg.ShowDialog() == true)
        {
            ExeBox.Text = dlg.FileName;
            if (string.IsNullOrWhiteSpace(NameBox.Text))
                NameBox.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
        }
    }

    private void OkClick(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text?.Trim();
        var exe = ExeBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(exe))
        {
            MessageBox.Show("Please enter both a name and an executable path.", "Missing info");
            return;
        }
        var exeName = Path.GetFileName(exe);
        var id = "user." + name.ToLowerInvariant().Replace(" ", "-");
        var appId = string.IsNullOrWhiteSpace(AppIdBox.Text) ? null : AppIdBox.Text.Trim();
        Result = new KnownGame(id, name, new[] { exeName }, appId, exe, KnownGameSource.UserAdded);
        DialogResult = true;
        Close();
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
