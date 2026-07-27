using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Crustcut.App.Services;
using Crustcut.Presentation;
using PrimeOSTuner.Core.Monitoring;
using PrimeOSTuner.Core.Performance;
using PrimeOSTuner.Core.Profiles;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;

namespace Crustcut.App;

public partial class App : Application
{
    private OverviewViewModel? _overview;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _overview = BuildOverviewViewModel();

            desktop.MainWindow = new MainWindow(new ShellViewModel(), _overview);
            desktop.ShutdownRequested += (_, _) => _overview?.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Composed by hand rather than through a container. Phase 1 only needs the live
    /// sampler and the registry-driven tweak catalog; the hand-written tweaks pull in
    /// GameRegistry and a large dependency graph, and get wired in Phase 2 along with the
    /// rest of the pages.
    /// </summary>
    private static OverviewViewModel BuildOverviewViewModel()
    {
        var registry = new RegistryClient();
        var defs = RegistryTweakCatalog.LoadFromFile(RegistryTweakCatalog.DefaultPath());
        var tweaks = defs.Select(d => (ITweak)new RegistryTweak(d, registry)).ToList();

        var sampler = new SystemSampler(new HardwareClient());
        var activeStore = new ActiveTweaksStore(ActiveTweaksStore.DefaultPath());
        var frameStore = new FrameSessionStore(FrameSessionStore.DefaultPath());

        return new OverviewViewModel(sampler, activeStore, tweaks, frameStore, new AvaloniaDispatcher());
    }
}
