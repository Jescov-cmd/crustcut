using Avalonia.Controls;

namespace Crustcut.App.Views;

public partial class HistoryView : UserControl
{
    public HistoryView()
    {
        InitializeComponent();
        PageActivity.Track(this, active =>
        {
            if (!active) return;
            ActivityList.ItemsSource = ReadRecentActivity();
            // Reloaded on every visit: loading once per run hid anything applied after
            // the first look until Crustcut restarted.
            if (DataContext is Crustcut.Presentation.HistoryViewModel vm)
                _ = vm.LoadAsync().ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        });
    }

    /// <summary>
    /// Last dozen engine.log lines, newest first. Read fresh on every visit — the file is
    /// tiny and a stale diary is worse than none.
    /// </summary>
    private static IReadOnlyList<string> ReadRecentActivity()
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PrimeOSTuner", "engine.log");
            if (!File.Exists(path)) return new[] { "Nothing yet — automatic actions will show up here." };

            // Skip the engine's internal chatter; keep lines a person would care about.
            string[] noise = { "engine: StartAsync", "tick: skipping", "adaptive: idle",
                               "fans: auto →", "memory: " };
            return File.ReadLines(path)
                .Where(l => l.Length > 0 && !noise.Any(l.Contains))
                .TakeLast(60)
                .Reverse()
                .Take(12)
                .ToList();
        }
        catch
        {
            return new[] { "Couldn't read the activity log." };
        }
    }
}
