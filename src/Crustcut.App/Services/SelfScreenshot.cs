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
    /// <summary>Parses --screenshot &lt;path&gt; [--screenshot-tab &lt;NavId&gt;] out of the args.</summary>
    public static (string? Path, string? Tab) Parse(string[] args)
    {
        string? path = null, tab = null;
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--screenshot") path = args[i + 1];
            if (args[i] == "--screenshot-tab") tab = args[i + 1];
        }
        return (path, tab);
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
