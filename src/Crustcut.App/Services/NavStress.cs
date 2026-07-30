using System.Diagnostics;
using Avalonia.Threading;

namespace Crustcut.App.Services;

/// <summary>
/// Automated tab-switch responsiveness test: `Crustcut.exe --navstress out.txt` opens the
/// window, clicks through the tabs the way a user would, and records how long the UI
/// thread stays busy after each switch — i.e. the freeze the user actually feels. Exists
/// because a startup screenshot can look fine while interactive navigation is still
/// seconds-laggy (that exact gap shipped one "fixed" build that wasn't).
/// </summary>
public static class NavStress
{
    public static string? Parse(string[] args)
    {
        var i = Array.IndexOf(args, "--navstress");
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    public static async Task RunThenExitAsync(MainWindow window, string outPath, Action shutdown)
    {
        // Let startup work (engine, first probes) settle so it doesn't pollute the numbers.
        await Task.Delay(4000);

        // Each tab twice: first visit pays the one-time build+load, the revisit is what
        // day-to-day switching feels like.
        var sequence = new[]
        {
            "Memory", "Games", "Overview", "Optimize",
            "Memory", "Games", "Overview", "Memory", "Games",
        };

        var lines = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tab in sequence)
        {
            var kind = seen.Add(tab) ? "first" : "revisit";
            var sw = Stopwatch.StartNew();
            window.NavigateTo(tab);
            // Background priority runs only after all layout/render work drains, so the
            // elapsed time ≈ how long the UI thread was pinned by the switch.
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            sw.Stop();
            lines.Add($"{tab,-10} {kind,-8} {sw.ElapsedMilliseconds,5} ms");
            await Task.Delay(500);
        }

        try { File.WriteAllLines(outPath, lines); } catch { }
        shutdown();
    }
}
