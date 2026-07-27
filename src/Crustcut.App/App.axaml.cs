using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Crustcut.Presentation;

namespace Crustcut.App;

public partial class App : Application
{
    private Composition? _composition;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _composition = new Composition();

            desktop.MainWindow = new MainWindow(
                new ShellViewModel(),
                _composition.Overview,
                _composition.Optimize,
                _composition.Cleanup,
                _composition.Memory);

            desktop.ShutdownRequested += (_, _) => _composition?.Overview.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
