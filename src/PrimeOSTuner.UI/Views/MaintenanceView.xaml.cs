using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PrimeOSTuner.Core.History;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.UI.Services;
using PrimeOSTuner.UI.ViewModels;

namespace PrimeOSTuner.UI.Views;

public partial class MaintenanceView : UserControl
{
    // Disk-space / housekeeping actions. Deliberately NOT performance tweaks — they are
    // not part of the optimization flow or the boost score.
    private static readonly string[] CleanupTweakIds =
    {
        "core.dns-flush",
        "core.windows-update-cache",
        "core.driver-health",
        "core.driver-store-cleanup",
    };

    private readonly TweakHistory _history;
    private readonly TrayIconService _tray;
    private readonly SettingsViewModel _settings;

    public MaintenanceView(
        IEnumerable<ITweak> tweaks,
        TweakHistory history,
        TrayIconService tray,
        SettingsViewModel settings)
    {
        InitializeComponent();
        _history = history;
        _tray = tray;
        _settings = settings;

        var cleanups = tweaks
            .Where(t => CleanupTweakIds.Contains(t.Id))
            .OrderBy(t => Array.IndexOf(CleanupTweakIds, t.Id))
            .ToList();
        CleanupActions.ItemsSource = cleanups;
    }

    private void Notify(string title, string message)
    {
        if (_settings.NotificationsEnabled) _tray.ShowNotification(title, message);
    }

    private async void CleanupClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ITweak tweak } btn) return;

        var originalContent = btn.Content;
        btn.IsEnabled = false;
        btn.Content = "Working…";
        try
        {
            var result = await tweak.ApplyAsync();
            if (result.Succeeded)
            {
                await _history.AppendAsync(new HistoryEntry(
                    Guid.NewGuid(), tweak.Id, tweak.DisplayName,
                    DateTime.UtcNow, result.UndoData, false));
                CleanupStatus.Text = $"{tweak.DisplayName} — {result.Message ?? "done."}";
                Notify(tweak.DisplayName, result.Message ?? "Done.");
            }
            else
            {
                CleanupStatus.Text = $"{tweak.DisplayName} — failed: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            CleanupStatus.Text = $"{tweak.DisplayName} — error: {ex.Message}";
        }
        finally
        {
            btn.IsEnabled = true;
            btn.Content = originalContent;
        }
    }
}
