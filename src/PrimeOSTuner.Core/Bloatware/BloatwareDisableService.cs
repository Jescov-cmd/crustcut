using PrimeOSTuner.Core.Tweaks;

namespace PrimeOSTuner.Core.Bloatware;

public sealed class BloatwareDisableService
{
    private readonly IServiceClient _services;

    // Map appx package name → list of related Windows services to disable.
    // Curated; only includes services we know are tied to a specific package.
    private static readonly Dictionary<string, string[]> ServiceMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Microsoft.XboxGamingOverlay"] = new[] { "XblGameSave", "XboxGipSvc", "XblAuthManager", "XboxNetApiSvc" },
        ["Microsoft.XboxApp"] = new[] { "XblGameSave", "XblAuthManager" },
        ["Microsoft.XboxIdentityProvider"] = new[] { "XblAuthManager" },
        ["Microsoft.549981C3F5F10"] = Array.Empty<string>(),  // Cortana — handled via separate registry tweak
    };

    public BloatwareDisableService(IServiceClient services)
    {
        _services = services;
    }

    public Task DisableAsync(BloatwareItem item, CancellationToken ct = default)
    {
        if (!ServiceMap.TryGetValue(item.Entry.AppxName, out var services))
            return Task.CompletedTask;

        foreach (var name in services)
        {
            var state = _services.Read(name);
            if (!state.Exists) continue;
            _services.Stop(name);
            _services.SetStartTypeDisabled(name);
        }
        return Task.CompletedTask;
    }

    public Task EnableAsync(BloatwareItem item, CancellationToken ct = default)
    {
        if (!ServiceMap.TryGetValue(item.Entry.AppxName, out var services))
            return Task.CompletedTask;

        foreach (var name in services)
        {
            var state = _services.Read(name);
            if (!state.Exists) continue;
            // "Manual" is the safest default — service runs only when something explicitly starts it.
            _services.SetStartType(name, "Manual");
        }
        return Task.CompletedTask;
    }
}
