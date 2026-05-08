using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PrimeOSTuner.Core.History;
using PrimeOSTuner.Core.Tweaks;

namespace PrimeOSTuner.UI.Views;

public partial class OptimizeView : UserControl
{
    private readonly TweakHistory _history;

    public OptimizeView(IEnumerable<ITweak> tweaks, TweakHistory history)
    {
        InitializeComponent();
        TweakList.ItemsSource = tweaks.Where(t => !t.IsDestructive).ToList();
        _history = history;
    }

    private async void PreviewClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ITweak t })
        {
            var preview = await t.PreviewAsync();
            MessageBox.Show(preview, $"Preview — {t.DisplayName}");
        }
    }

    private async void ApplyClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ITweak t } btn)
        {
            btn.IsEnabled = false;
            try
            {
                var result = await t.ApplyAsync();
                if (result.Succeeded)
                {
                    await _history.AppendAsync(new HistoryEntry(
                        Guid.NewGuid(), t.Id, t.DisplayName, DateTime.UtcNow, result.UndoData, false));
                    MessageBox.Show("Applied successfully.", t.DisplayName);
                }
                else
                {
                    MessageBox.Show($"Failed: {result.Error}", t.DisplayName);
                }
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }
    }
}
