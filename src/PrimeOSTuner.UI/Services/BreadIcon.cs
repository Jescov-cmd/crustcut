using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PrimeOSTuner.UI.Services;

/// <summary>
/// Loads the Crustcut bread logo (Assets/Brand/bread.png, embedded as a WPF Resource)
/// and exposes it as a System.Drawing.Icon for the system tray and a BitmapSource for
/// the WPF window icon. Pixels come straight from the PNG — the warm brown tones and
/// score marks are part of the image, so no recoloring happens here.
/// </summary>
public static class BreadIcon
{
    private const string ResourceUri = "pack://application:,,,/Assets/Brand/bread.png";

    public static Icon BuildSystemIcon(int size = 32)
    {
        using var bmp = LoadPngScaled(size);
        var hIcon = bmp.GetHicon();
        var icon = (Icon)Icon.FromHandle(hIcon).Clone();
        DestroyIcon(hIcon);
        return icon;
    }

    public static BitmapSource BuildWpfImageSource(int size = 64)
    {
        // For WPF we can hand BitmapImage the pack URI directly. WPF rasterizes
        // at the requested decode size.
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = new Uri(ResourceUri, UriKind.Absolute);
        bmp.DecodePixelWidth = size;
        bmp.DecodePixelHeight = size;
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    /// <summary>
    /// Loads the embedded PNG into a System.Drawing.Bitmap at the exact pixel size we
    /// need — Win32 HICON expects square dimensions matching the system metric.
    /// </summary>
    private static Bitmap LoadPngScaled(int size)
    {
        var streamInfo = Application.GetResourceStream(new Uri(ResourceUri, UriKind.Absolute))
            ?? throw new InvalidOperationException("bread.png resource not found");
        using var src = new Bitmap(streamInfo.Stream);
        var dst = new Bitmap(size, size);
        using var g = Graphics.FromImage(dst);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(src, 0, 0, size, size);
        return dst;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}
