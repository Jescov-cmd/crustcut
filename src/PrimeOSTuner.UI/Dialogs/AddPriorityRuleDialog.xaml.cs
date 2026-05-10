using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Core.Memory;

namespace PrimeOSTuner.UI.Dialogs;

public sealed record RunningProcessInfo(int Pid, string ProcessName, string ExePath);

public partial class AddPriorityRuleDialog : Window
{
    private readonly GameRegistry _games;

    public PriorityRule? Result { get; private set; }
    private string? _chosenPath;
    private string? _chosenName;

    public AddPriorityRuleDialog(GameRegistry games)
    {
        InitializeComponent();
        _games = games;
        RefreshProcesses();
    }

    private void RefreshProcessesClick(object sender, RoutedEventArgs e) => RefreshProcesses();

    private void RefreshProcesses()
    {
        var procs = new List<RunningProcessInfo>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                var path = p.MainModule?.FileName;
                if (string.IsNullOrEmpty(path)) continue;
                procs.Add(new RunningProcessInfo(p.Id, p.ProcessName, path));
            }
            catch { /* access denied — system process */ }
            finally { p.Dispose(); }
        }
        ProcessList.ItemsSource = procs.OrderBy(p => p.ProcessName).ToList();
    }

    private void ProcessSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProcessList.SelectedItem is RunningProcessInfo p)
        {
            _chosenPath = p.ExePath;
            _chosenName = p.ProcessName;
            AddButton.IsEnabled = true;
        }
    }

    private void BrowseClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Executable files (*.exe)|*.exe",
            CheckFileExists = true
        };
        if (dlg.ShowDialog(this) == true)
        {
            _chosenPath = dlg.FileName;
            _chosenName = Path.GetFileNameWithoutExtension(dlg.FileName);
            BrowsedPath.Text = dlg.FileName;
            AddButton.IsEnabled = true;
        }
    }

    private async void AddClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_chosenPath) || string.IsNullOrEmpty(_chosenName)) return;

        // Auto-tag as Game if any registered game's InstallPath is a parent of the chosen exe.
        var games = await _games.GetAllAsync();
        var isGame = games.Any(g =>
            !string.IsNullOrEmpty(g.InstallPath) &&
            _chosenPath.StartsWith(g.InstallPath!, StringComparison.OrdinalIgnoreCase));

        Result = new PriorityRule(
            ExePath: _chosenPath,
            DisplayName: _chosenName,
            Priority: PriorityLevel.Normal,
            ProtectFromRamCleanup: false,
            GameBooster: false,
            IsGame: isGame);
        Close();
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        Result = null;
        Close();
    }
}
