using System.Text.Json;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class ServiceDisableTweak : ITweak
{
    private readonly string _serviceName;
    private readonly IServiceClient _client;

    public ServiceDisableTweak(
        string id,
        string displayName,
        string description,
        string category,
        string serviceName,
        string? riskNote,
        IServiceClient client)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        Category = category;
        _serviceName = serviceName;
        RiskNote = riskNote;
        _client = client;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public string Category { get; }
    public string? RiskNote { get; }
    public bool RequiresElevation => true;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    private sealed record Backup(string PreviousStartType);

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        var s = _client.Read(_serviceName);
        if (!s.Exists) return Task.FromResult(TweakState.Unknown);
        return Task.FromResult(s.CurrentStartType == "Disabled" && !s.IsRunning
            ? TweakState.Applied : TweakState.NotApplied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var s = _client.Read(_serviceName);
        if (!s.Exists)
            return Task.FromResult(TweakResult.Failure($"Service '{_serviceName}' does not exist."));
        var backup = new Backup(s.CurrentStartType);
        _client.Stop(_serviceName);
        _client.SetStartTypeDisabled(_serviceName);
        return Task.FromResult(TweakResult.Success(JsonSerializer.Serialize(backup)));
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
    {
        var backup = JsonSerializer.Deserialize<Backup>(undoData)
            ?? throw new InvalidOperationException("Invalid undo data");
        _client.SetStartType(_serviceName, backup.PreviousStartType);
        return Task.FromResult(TweakResult.Success());
    }

    public Task<string> PreviewAsync(CancellationToken ct = default)
    {
        var s = _client.Read(_serviceName);
        return Task.FromResult(s.Exists
            ? $"Will stop the '{_serviceName}' service and set startup type to Disabled (currently {s.CurrentStartType})."
            : $"Service '{_serviceName}' does not exist on this system.");
    }
}
