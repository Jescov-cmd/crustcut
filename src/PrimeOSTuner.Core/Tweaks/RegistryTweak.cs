using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class RegistryTweak : ITweak
{
    private readonly RegistryTweakDefinition _def;
    private readonly IRegistryClient _registry;

    public RegistryTweak(RegistryTweakDefinition def, IRegistryClient registry)
    {
        _def = def;
        _registry = registry;
    }

    public string Id => _def.Id;
    public string DisplayName => _def.DisplayName;
    public string Description => _def.Description;
    public bool RequiresElevation => _def.RequiresElevation;
    public bool IsDestructive => false;
    public bool RequiresReboot => _def.RequiresReboot;

    public string Category => _def.Category;
    public string? RiskNote => _def.RiskNote;

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        if (_def.ParsedKind == RegistryValueKind.DWord)
        {
            var current = _registry.ReadDword(_def.ParsedHive, _def.Key, _def.ValueName);
            if (current is null) return Task.FromResult(TweakState.NotApplied);
            var expected = ParseDword(_def.AppliedData);
            return Task.FromResult(current == expected ? TweakState.Applied : TweakState.NotApplied);
        }
        else
        {
            var current = _registry.ReadString(_def.ParsedHive, _def.Key, _def.ValueName);
            return Task.FromResult(string.Equals(current, _def.AppliedData, StringComparison.OrdinalIgnoreCase)
                ? TweakState.Applied : TweakState.NotApplied);
        }
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        RegistryBackup backup;
        if (_def.ParsedKind == RegistryValueKind.DWord)
        {
            var value = ParseDword(_def.AppliedData);
            backup = _registry.WriteDword(_def.ParsedHive, _def.Key, _def.ValueName, value);
        }
        else
        {
            backup = _registry.WriteString(_def.ParsedHive, _def.Key, _def.ValueName, _def.AppliedData);
        }
        return Task.FromResult(TweakResult.Success(JsonSerializer.Serialize(backup)));
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
    {
        var backup = JsonSerializer.Deserialize<RegistryBackup>(undoData)
            ?? throw new InvalidOperationException("Invalid undo data");
        _registry.RestoreFromBackup(backup);
        return Task.FromResult(TweakResult.Success());
    }

    public Task<string> PreviewAsync(CancellationToken ct = default)
        => Task.FromResult($"Will set {_def.Hive}\\{_def.Key}\\{_def.ValueName} to '{_def.AppliedData}' ({_def.ValueKind}).");

    private static int ParseDword(string raw)
    {
        if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return unchecked((int)Convert.ToUInt32(raw, 16));
        return int.Parse(raw);
    }
}
