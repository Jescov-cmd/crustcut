using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Core.History;
using PrimeOSTuner.Core.Lifecycle;
using PrimeOSTuner.Core.Profiles;
using PrimeOSTuner.Core.Tweaks;
using Xunit;

namespace PrimeOSTuner.Tests.Lifecycle;

public class ProfileLifecycleServiceTests : IDisposable
{
    private readonly string _activePath = Path.Combine(Path.GetTempPath(), $"active-{Guid.NewGuid()}.json");
    private readonly string _gameProfilesPath = Path.Combine(Path.GetTempPath(), $"gp-{Guid.NewGuid()}.json");
    private readonly string _historyPath = Path.Combine(Path.GetTempPath(), $"h-{Guid.NewGuid()}.json");

    public void Dispose()
    {
        foreach (var p in new[] { _activePath, _gameProfilesPath, _historyPath })
            if (File.Exists(p)) File.Delete(p);
    }

    private static Mock<ITweak> StubTweak(string id)
    {
        var m = new Mock<ITweak>();
        m.SetupGet(t => t.Id).Returns(id);
        m.SetupGet(t => t.DisplayName).Returns(id);
        m.Setup(t => t.ApplyAsync(It.IsAny<IProgress<int>?>(), default))
            .ReturnsAsync(TweakResult.Success("undo-" + id));
        m.Setup(t => t.RevertAsync(It.IsAny<string>(), default))
            .ReturnsAsync(TweakResult.Success());
        return m;
    }

    private static KnownGame Game = new(
        "steam.440", "Team Fortress 2", new[] { "hl2.exe" }, "440", null, KnownGameSource.Steam);

    [Fact]
    public async Task On_GameStarted_applies_assigned_profile_and_writes_active_tweaks_file()
    {
        var tweak = StubTweak("game.game-mode").Object;
        var applier = new ProfileApplier(new[] { tweak }, new TweakHistory(_historyPath));
        var profileStore = new GameProfileStore(_gameProfilesPath);
        await profileStore.SetProfileForAsync(Game.Id, "basic");
        var activeStore = new ActiveTweaksStore(_activePath);

        var watcher = new Mock<IGameProcessWatcher>();
        var service = new ProfileLifecycleService(
            watcher.Object,
            profileStore,
            activeStore,
            new Dictionary<string, ModeProfile>
            {
                ["basic"] = new ModeProfile("basic", "Basic", "", new[] { "game.game-mode" })
            },
            applier);

        await service.HandleGameStartedAsync(Game);

        var record = await activeStore.LoadAsync();
        record.Should().NotBeNull();
        record!.GameId.Should().Be(Game.Id);
        record.Outcomes.Should().HaveCount(1);
    }

    [Fact]
    public async Task On_GameStopped_reverts_outcomes_and_clears_active_tweaks_file()
    {
        var tweak = StubTweak("game.game-mode");
        var applier = new ProfileApplier(new[] { tweak.Object }, new TweakHistory(_historyPath));
        var profileStore = new GameProfileStore(_gameProfilesPath);
        await profileStore.SetProfileForAsync(Game.Id, "basic");
        var activeStore = new ActiveTweaksStore(_activePath);

        var watcher = new Mock<IGameProcessWatcher>();
        var service = new ProfileLifecycleService(
            watcher.Object,
            profileStore,
            activeStore,
            new Dictionary<string, ModeProfile>
            {
                ["basic"] = new ModeProfile("basic", "Basic", "", new[] { "game.game-mode" })
            },
            applier);

        await service.HandleGameStartedAsync(Game);
        (await activeStore.LoadAsync()).Should().NotBeNull("the profile should be applied + recorded");

        await service.HandleGameStoppedAsync(new GameStoppedArgs(Game, "exit"));

        tweak.Verify(t => t.RevertAsync("undo-game.game-mode", default), Times.Once);
        (await activeStore.LoadAsync()).Should().BeNull();
    }

    [Fact]
    public async Task RecoverFromCrashAsync_reverts_any_existing_active_tweaks_record()
    {
        var tweak = StubTweak("game.game-mode");
        var applier = new ProfileApplier(new[] { tweak.Object }, new TweakHistory(_historyPath));
        var activeStore = new ActiveTweaksStore(_activePath);
        await activeStore.SaveAsync(new ActiveTweaksRecord(
            Game.Id, "basic", DateTime.UtcNow,
            new[] { new ProfileTweakOutcome("game.game-mode", true, "undo-game.game-mode", null) }));

        var watcher = new Mock<IGameProcessWatcher>();
        var service = new ProfileLifecycleService(
            watcher.Object,
            new GameProfileStore(_gameProfilesPath),
            activeStore,
            new Dictionary<string, ModeProfile>(),
            applier);

        await service.RecoverFromCrashAsync();

        tweak.Verify(t => t.RevertAsync("undo-game.game-mode", default), Times.Once);
        (await activeStore.LoadAsync()).Should().BeNull();
    }
}
