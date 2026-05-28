using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace PrimeOSTuner.UI.Services;

/// <summary>
/// Renders the Crustcut bread-loaf logo (pink-orange gradient + three diagonal score marks)
/// to a System.Drawing.Bitmap, then exposes both a Win32 <see cref="Icon"/> (for the
/// system tray) and a WPF <see cref="BitmapSource"/> (for the window icon / taskbar / Alt-Tab).
///
/// Drawing programmatically — instead of shipping a .ico file — keeps the brand consistent
/// across every surface and means a future logo tweak is a one-line code change.
/// </summary>
public static class BreadIcon
{
    public static Icon BuildSystemIcon(int size = 32)
    {
        using var bmp = Render(size);
        var hIcon = bmp.GetHicon();
        var icon = (Icon)Icon.FromHandle(hIcon).Clone();
        DestroyIcon(hIcon);
        return icon;
    }

    public static BitmapSource BuildWpfImageSource(int size = 64)
    {
        using var bmp = Render(size);
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        ms.Position = 0;
        var decoder = new PngBitmapDecoder(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }

    private static Bitmap Render(int size)
    {
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var brush = new LinearGradientBrush(
            new PointF(0, 0), new PointF(size, size),
            Color.FromArgb(0xF7, 0x41, 0x6B),  // pink
            Color.FromArgb(0xFF, 0xA0, 0x1F)); // orange

        // Loaf shape: rounded rectangle centered, with a margin proportional to size.
        float margin = size * 0.0625f;
        float top = size * 0.156f;
        float bottom = size - size * 0.156f;
        float radius = (bottom - top) / 2f;

        using var loaf = new GraphicsPath();
        loaf.AddArc(margin, top, radius * 2, radius * 2, 180, 90);
        loaf.AddArc(size - margin - radius * 2, top, radius * 2, radius * 2, 270, 90);
        loaf.AddArc(size - margin - radius * 2, bottom - radius * 2, radius * 2, radius * 2, 0, 90);
        loaf.AddArc(margin, bottom - radius * 2, radius * 2, radius * 2, 90, 90);
        loaf.CloseFigure();
        g.FillPath(brush, loaf);

        // Three diagonal score marks across the top of the loaf
        using var scorePen = new Pen(Color.FromArgb(200, 255, 255, 255), size * 0.06f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        float s = size;
        g.DrawLine(scorePen, s * 0.28f, s * 0.34f, s * 0.40f, s * 0.50f);
        g.DrawLine(scorePen, s * 0.44f, s * 0.28f, s * 0.56f, s * 0.44f);
        g.DrawLine(scorePen, s * 0.60f, s * 0.34f, s * 0.72f, s * 0.50f);

        return bmp;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(System.IntPtr handle);
}
