using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PrimeOSTuner.Core.History;
using PrimeOSTuner.Core.Monitoring;
using PrimeOSTuner.Core.Pipeline;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.UI.ViewModels;
using PrimeOSTuner.Win;
using Serilog;
using System.IO;
using System.Windows;

namespace PrimeOSTuner.UI;

public partial class App : Application
{
    public IHost Host { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        var logsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrimeOSTuner", "logs");
        Directory.CreateDirectory(logsDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(logsDir, "primeos-.log"), rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(s =>
            {
                // Win layer
                s.AddSingleton<IRegistryClient, RegistryClient>();
                s.AddSingleton<IProcessClient, ProcessClient>();
                s.AddSingleton<IPowerPlanClient, PowerPlanClient>();
                s.AddSingleton<IRestorePointClient, RestorePointClient>();
                s.AddSingleton<IHardwareClient, HardwareClient>();

                // Core
                s.AddSingleton(_ => new TweakHistory(TweakHistory.DefaultPath()));
                s.AddSingleton<SystemSampler>();
                s.AddSingleton<JunkFileTweak>();
                s.AddSingleton<PowerPlanTweak>();
                s.AddSingleton<RamCleanerTweak>();
                s.AddSingleton<VisualEffectsTweak>();
                s.AddSingleton<IEnumerable<ITweak>>(sp => new ITweak[]
                {
                    sp.GetRequiredService<JunkFileTweak>(),
                    sp.GetRequiredService<PowerPlanTweak>(),
                    sp.GetRequiredService<RamCleanerTweak>(),
                    sp.GetRequiredService<VisualEffectsTweak>()
                });
                s.AddSingleton<OneClickOptimizer>();

                // ViewModels & MainWindow
                s.AddSingleton<ShellViewModel>();
                s.AddSingleton<DashboardViewModel>();
                s.AddSingleton<MainWindow>();
            })
            .Build();

        Host.Start();
        var window = Host.Services.GetRequiredService<MainWindow>();
        window.Show();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        Host?.StopAsync().Wait(TimeSpan.FromSeconds(5));
        Host?.Dispose();
        base.OnExit(e);
    }
}
