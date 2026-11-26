using Avalonia;

namespace Crustcut.App;

internal static class Program
{
    /// <summary>Command-line args, exposed so App can read the --screenshot debug flags.</summary>
    public static string[] Args { get; private set; } = Array.Empty<string>();

    [STAThread]
    public static void Main(string[] args)
    {
        Args = args;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
        // CPU rendering by default. The GPU path (ANGLE on Direct3D 11) creates a D3D
        // device on first window, and the graphics driver brings ~70 worker threads and
        // ~125 MB of native memory with it — measured, and none of it is released when the
        // window closes, so a tray utility paid it all day. Screenshots of both paths are
        // pixel-identical and tab switching measured no slower; this UI is text, lines and
        // flat fills, which is exactly what CPU rendering does well.
        // CRUSTCUT_RENDER=gpu restores the GPU path for diagnosis.
        if (Environment.GetEnvironmentVariable("CRUSTCUT_RENDER") != "gpu")
            builder = builder.With(new Win32PlatformOptions
            {
                RenderingMode = new[] { Win32RenderingMode.Software },
            });
        return builder;
    }
}
