using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Core.Profiles;
using PrimeOSTuner.Win.Suspension;

namespace PrimeOSTuner.Core.Lifecycle;

public sealed class ProfileLifecycleService
{
    private readonly IGameProcessWatcher _watcher;
    private readonly GameProfileStore _profiles;
    private readonly ActiveTweaksStore _active;
    private readonly IReadOnlyDictionary<string, ModeProfile> _profileLookup;
    private readonly ProfileApplier _applier;
    private readonly IBackgroundSuspenderService? _suspender;

    public ProfileLifecycleService(
        IGameProcessWatcher watcher,
        GameProfileStore profiles,
        ActiveTweaksStore active,
        IReadOnlyDictionary<string, ModeProfile> profileLookup,
        ProfileApplier applier,
        IBackgroundSuspenderService? suspender = null)
    {
        _watcher = watcher;
        _profiles = profiles;
        _active = active;
        _profileLookup = profileLookup;
        _applier = applier;
        _suspender = suspender;
    }

    public void Start()
    {
        _watcher.GameStarted += OnGameStarted;
        _watcher.GameStopped += OnGameStopped;
        _watcher.Start();
    }

    public void Stop()
    {
        _watcher.GameStarted -= OnGameStarted;
        _watcher.GameStopped -= OnGameStopped;
        _watcher.Stop();
    }

    public async Task RecoverFromCrashAsync(CancellationToken ct = default)
    {
        var record = await _active.LoadAsync();
        if (record is null) return;

        await _applier.RevertAsync(record.Outcomes, ct);
        await _active.ClearAsync();
    }

    private async void OnGameStarted(object? sender, KnownGame game)
    {
        try
        {
            var modeName = await _profiles.GetProfileForAsync(game.Id);
            if (modeName is null) return;
            if (!_profileLookup.TryGetValue(modeName, out var profile)) return;

            var result = await _applier.ApplyAsync(profile);
            await _active.SaveAsync(new ActiveTweaksRecord(
                game.Id, profile.Id, DateTime.UtcNow, result.Outcomes));

            try { _suspender?.SuspendBackgroundApps(); }
            catch { /* freezing optional apps must never break a game launch */ }
        }
        catch
        {
        }
    }

    private async void OnGameStopped(object? sender, GameStoppedArgs e)
    {
        try
        {
            try { _suspender?.ResumeAll(); }
            catch { /* never trap a stopped game in a frozen-app state */ }

            var record = await _active.LoadAsync();
            if (record is null || record.GameId != e.Game.Id) return;

            await _applier.RevertAsync(record.Outcomes);
            await _active.ClearAsync();
        }
        catch
        {
        }
    }
}
