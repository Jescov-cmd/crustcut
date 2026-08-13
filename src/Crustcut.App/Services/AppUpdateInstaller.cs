using Avalonia.Controls.ApplicationLifetimes;
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
            if (Avalonia.Application.Current?.ApplicationLifetime
                is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown();
            else
                Environment.Exit(0);
        });
    }
}
