using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace Crustcut.App;

/// <summary>
/// Attached property that loads an <see cref="Image"/>'s bitmap off the UI thread,
/// decoded to a fixed width instead of full size. A plain Source binding through a
/// converter decodes synchronously on the UI thread — with a page of cover art that
/// stalls every frame until all covers are decoded.
/// </summary>
public static class AsyncImage
{
    /// <summary>Decode width — 2× the 168px tile so covers stay crisp at 200% DPI.</summary>
    private const int DecodeWidth = 336;

    public static readonly AttachedProperty<string?> SourcePathProperty =
        AvaloniaProperty.RegisterAttached<Image, string?>("SourcePath", typeof(AsyncImage));

    public static string? GetSourcePath(Image image) => image.GetValue(SourcePathProperty);
    public static void SetSourcePath(Image image, string? value) => image.SetValue(SourcePathProperty, value);

    static AsyncImage()
    {
        SourcePathProperty.Changed.AddClassHandler<Image>((image, e) => _ = LoadAsync(image, e.NewValue as string));
    }

    private static async Task LoadAsync(Image image, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            image.Source = null;
            return;
        }

        Bitmap? bmp = null;
        try
        {
            bmp = await Task.Run(() =>
            {
                using var fs = File.OpenRead(path);
                return Bitmap.DecodeToWidth(fs, DecodeWidth);
            });
        }
        catch
        {
            // Corrupt or partially-downloaded file — leave the text fallback visible.
        }

        // The path may have changed while we decoded (recycled row) — only the latest wins.
        if (GetSourcePath(image) == path) image.Source = bmp;
    }
}
