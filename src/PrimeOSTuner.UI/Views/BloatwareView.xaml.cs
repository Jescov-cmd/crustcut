using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using PrimeOSTuner.Core.Bloatware;
using PrimeOSTuner.UI.Dialogs;
using PrimeOSTuner.UI.ViewModels;

namespace PrimeOSTuner.UI.Views;

public partial class BloatwareView : UserControl
{
    private readonly BloatwareViewModel _vm;
    private readonly BloatwareDisableService _disableSvc;
    private readonly BloatwareUninstallService _uninstallSvc;

    public BloatwareView(
        BloatwareViewModel vm,
        BloatwareDisableService disableSvc,
        BloatwareUninstallService uninstallSvc)
    {
        InitializeComponent();
        _vm = vm;
        _disableSvc = disableSvc;
        _uninstallSvc = uninstallSvc;
        DataContext = vm;
        // Auto-scan when the tab is first shown.
        Loaded += async (_, _) => { if (_vm.Items.Count == 0) await _vm.RefreshAsync(); };
    }

    private async void RefreshClick(object sender, RoutedEventArgs e)
    {
        await _vm.RefreshAsync();
    }

    private async void DisableClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not BloatwareItemRowVm row) return;
        btn.IsEnabled = false;
        try
        {
            await _disableSvc.DisableAsync(row.Item);
            _vm.Items.Remove(row);   // handled — drop from list (a rescan won't show it either)
            _vm.DetectedCount = _vm.Items.Count;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Disable failed: {ex.Message}", row.DisplayName,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            btn.IsEnabled = true;
        }
    }

    private void OpenUninstallerClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not DesktopBloatRowVm row) return;
        try
        {
            // SECURITY: never hand the registry UninstallString to a shell (cmd would interpret
            // & | etc., and HKCU uninstall entries are writable by unprivileged processes while
            // Crustcut runs elevated). Split exe/args ourselves and launch the exe directly.
            var (exe, args) = SplitCommandLine(row.Hit.Program.UninstallString);
            if (exe is null)
            {
                MessageBox.Show("This program's uninstall command looks invalid — uninstall it from " +
                                "Windows Settings → Apps instead.", row.ProgramName,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, args)
            { UseShellExecute = true });
            btn.Content = "Uninstaller opened";
            btn.IsEnabled = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Couldn't open the uninstaller: {ex.Message}", row.ProgramName,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Splits an UninstallString into (exe, args) without shell interpretation. Handles a
    /// quoted exe path, and for unquoted paths with spaces resolves progressively longer
    /// prefixes the way Windows does. Returns (null, _) when no real executable resolves.
    /// </summary>
    internal static (string? Exe, string Args) SplitCommandLine(string commandLine)
    {
        var s = commandLine.Trim();
        if (s.Length == 0) return (null, "");

        if (s[0] == '"')
        {
            var close = s.IndexOf('"', 1);
            if (close < 0) return (null, "");
            var exe = s[1..close];
            return File.Exists(exe) ? (exe, s[(close + 1)..].Trim()) : (null, "");
        }

        // Unquoted: try progressively longer space-delimited prefixes ("C:\Program Files\x\u.exe /S").
        var probe = -1;
        while (true)
        {
            probe = s.IndexOf(' ', probe + 1);
            var candidate = probe < 0 ? s : s[..probe];
            if (File.Exists(candidate)) return (candidate, probe < 0 ? "" : s[(probe + 1)..].Trim());
            if (probe < 0) break;
        }
        // Not a direct file path — allow only well-known system uninstall hosts (resolved via PATH).
        var first = s.Split(' ', 2);
        var name = Path.GetFileNameWithoutExtension(first[0]).ToLowerInvariant();
        if (name is "msiexec" or "rundll32")
            return (first[0], first.Length > 1 ? first[1].Trim() : "");
        return (null, "");
    }

    private async void UninstallClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not BloatwareItemRowVm row) return;

        var dlg = new BloatwareUninstallDialog { Owner = Window.GetWindow(this) };
        dlg.Configure(row.Item);
        dlg.ShowDialog();
        if (!dlg.Confirmed) return;

        btn.IsEnabled = false;
        try
        {
            await _uninstallSvc.UninstallAsync(row.Item);
            _vm.Items.Remove(row);
            _vm.DetectedCount = _vm.Items.Count;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Uninstall failed: {ex.Message}", row.DisplayName,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            btn.IsEnabled = true;
        }
    }
}
