using Crustcut.Presentation;
using PrimeOSTuner.Core.Updates;

namespace Crustcut.App.Services;

/// <summary>Bridges the view-model to the real downloader/swapper, and owns the shutdown
/// that lets the swap script replace files this process is holding open.</summary>
public sealed class AppUpdateInstaller : IUpdateInstaller
{
    private readonly UpdateInstaller _installer;

    public AppUpdateInstaller(UpdateInstaller installer) => _installer = installer;

    public Task<string?> ApplyAsync(AvailableUpdate update, IProgress<string>? progress = null)
        => _installer.DownloadAndApplyAsync(update, Shutdown, progress);

    private static void Shutdown()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // Must go through App.RequestExit: a bare desktop.Shutdown() is cancelled by the
            // minimise-to-tray Closing handler, which would leave the swap script waiting on
            // a process that never exits.
            if (Avalonia.Application.Current is App app)
                app.RequestExit();
            else
                Environment.Exit(0);
        });
    }
}
