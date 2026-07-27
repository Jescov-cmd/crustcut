using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

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
        });

        Environment.Exit(0);
    }
}
