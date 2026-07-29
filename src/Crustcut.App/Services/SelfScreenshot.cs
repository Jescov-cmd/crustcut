using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Crustcut.App.Services;

/// <summary>
/// Renders a window to a PNG from inside the app. Win32 screen capture is unreliable for
/// GPU-composited Avalonia windows — PrintWindow returns black and a screen grab catches
/// whatever is in front — so design iteration goes through Avalonia's own renderer.
/// Debug aid, driven by --screenshot on the command line.
/// </summary>
public static class SelfScreenshot
{
    /// <summary>
    /// Parses --screenshot &lt;path&gt; [--screenshot-tab &lt;NavId&gt;] [--screenshot-size WxH].
    /// The size flag exists so layout can be checked at more than one window size —
    /// fixed widths only reveal themselves as bugs when the window is narrow.
    /// </summary>
    public static (string? Path, string? Tab, int Width, int Height) Parse(string[] args)
    {
        string? path = null, tab = null;
        int w = 0, h = 0;

        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--screenshot") path = args[i + 1];
            if (args[i] == "--screenshot-tab") tab = args[i + 1];
            if (args[i] == "--screenshot-size")
            {
                var parts = args[i + 1].Split('x', 'X');
                if (parts.Length == 2 &&
                    int.TryParse(parts[0], out var pw) && int.TryParse(parts[1], out var ph))
                {
                    w = pw; h = ph;
                }
            }
        }
        return (path, tab, w, h);
    }

    /// <summary>
    /// Waits for a frame or two so bindings and layout settle, renders, saves, then exits.
    /// </summary>
    public static async Task CaptureThenExitAsync(Window window, string path, int settleMs = 2500)
    {
        await Task.Delay(settleMs);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var size = new PixelSize(
                Math.Max(1, (int)window.Bounds.Width),
                Math.Max(1, (int)window.Bounds.Height));

            using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
            bitmap.Render(window);

            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);
            bitmap.Save(path);

            // Ground truth for layout debugging: pixel-measuring screenshots kept producing
            // wrong theories about WHY something overflowed. This writes what the layout
            // system actually decided.
            DumpLayout(window, path + ".layout.txt");
        });

        await ShutdownAsync();
        return;

        static void DumpLayout(Window window, string outPath)
        {
            try
            {
                var sb = new StringBuilder();
                void Walk(Visual v, int depth)
                {
                    var c = v as Control;
                    var name = (c?.Name is { } n ? "#" + n : "") +
                               (c is StyledElement se && se.Classes.Count > 0
                                   ? " ." + string.Join(".", se.Classes) : "");
                    var topLeft = v.TranslatePoint(new Point(0, 0), window) ?? default;
                    sb.AppendLine(
                        $"{new string(' ', depth * 2)}{v.GetType().Name}{name}  " +
                        $"x={topLeft.X:F0} y={topLeft.Y:F0} w={v.Bounds.Width:F0} h={v.Bounds.Height:F0}");
                    foreach (var child in v.GetVisualChildren()) Walk(child, depth + 1);
                }
                Walk(window, 0);
                System.IO.File.WriteAllText(outPath, sb.ToString());
            }
            catch { /* debug aid only */ }
        }
    }

    private static async Task ShutdownAsync()
    {
        // Graceful shutdown, NOT Environment.Exit — Exit(0) killed an in-flight settings
        // write mid-file and tore priority-rules.json. Writes are atomic now, but there's
        // no reason to hard-kill the process either.
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (Avalonia.Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown();
            else
                Environment.Exit(0);
        });
    }
}
