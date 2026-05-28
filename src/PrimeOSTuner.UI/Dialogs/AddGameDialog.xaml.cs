using System.IO;
using System.Windows;
using Microsoft.Win32;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Win.Steam;

namespace PrimeOSTuner.UI.Dialogs;

public partial class AddGameDialog : Window
{
    private readonly ISteamAppLookup _lookup;

    public KnownGame? Result { get; private set; }

    public AddGameDialog(ISteamAppLookup lookup)
    {
        _lookup = lookup;
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

    private async void OkClick(object sender, RoutedEventArgs e)
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

        // Auto-link: if the user didn't paste a Steam AppID, look it up by name so
        // Sentinel can pull the recommended-spec data. Best-effort; null on failure.
        if (appId is null)
        {
            try
            {
                StatusText.Text = "Looking up on Steam…";
                OkBtn.IsEnabled = false;
                var match = await _lookup.ResolveAsync(name);
                if (match is not null)
                {
                    appId = match.AppId;
                    StatusText.Text = $"Linked to Steam: {match.OfficialName} (AppID {match.AppId})";
                }
                else
                {
                    StatusText.Text = "No Steam match — saving without app ID.";
                }
            }
            catch { StatusText.Text = "Lookup failed — saving without app ID."; }
            finally { OkBtn.IsEnabled = true; }
        }

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
