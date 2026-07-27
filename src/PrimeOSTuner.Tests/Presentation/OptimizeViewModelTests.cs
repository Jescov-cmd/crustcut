using Crustcut.Presentation;
using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.History;
using PrimeOSTuner.Core.Profiles;
using PrimeOSTuner.Core.Tweaks;
using Xunit;

namespace PrimeOSTuner.Tests.Presentation;

public sealed class RecordingDialogService : IDialogService
{
    public List<(string Title, string Message, DialogKind Kind)> Shown { get; } = new();

    public Task ShowAsync(string title, string message, DialogKind kind = DialogKind.Info)
    {
        Shown.Add((title, message, kind));
        return Task.CompletedTask;
    }
}

public class OptimizeViewModelTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "crustcut-optimize-" + Guid.NewGuid().ToString("N"));

    private OptimizeViewModel Build(IEnumerable<ITweak> tweaks, RecordingDialogService dialogs)
    {
        Directory.CreateDirectory(_dir);
        return new OptimizeViewModel(
            tweaks,
            new TweakHistory(Path.Combine(_dir, "history.json")),
            new SessionTweakStore(Path.Combine(_dir, "session.json")),
            new PendingUndoStore(Path.Combine(_dir, "undo.json")),
            dialogs);
    }

    private static Mock<ITweak> Tweak(string id, string name = "Test tweak", bool destructive = false)
    {
        var t = new Mock<ITweak>();
        t.SetupGet(x => x.Id).Returns(id);
        t.SetupGet(x => x.DisplayName).Returns(name);
        t.SetupGet(x => x.Description).Returns("does a thing");
        t.SetupGet(x => x.IsDestructive).Returns(destructive);
        t.SetupGet(x => x.RequiresReboot).Returns(false);
        t.Setup(x => x.ProbeAsync(It.IsAny<CancellationToken>())).ReturnsAsync(TweakState.NotApplied);
        return t;
    }

    [Fact]
    public void Destructive_tweaks_never_become_toggle_tiles()
    {
        var vm = Build(new[] { Tweak("safe.one").Object, Tweak("danger.one", destructive: true).Object },
                       new RecordingDialogService());

        vm.AllRows.Should().ContainSingle().Which.Tweak.Id.Should().Be("safe.one");
    }

    [Fact]
    public async Task Failed_apply_reports_the_error_and_leaves_the_tile_off()
    {
        var t = Tweak("t.fail");
        t.Setup(x => x.ApplyAsync(It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(TweakResult.Failure("nope"));

        var dialogs = new RecordingDialogService();
        var vm = Build(new[] { t.Object }, dialogs);

        await vm.ToggleAsync(vm.AllRows[0], wantApplied: true);

        dialogs.Shown.Should().ContainSingle();
        dialogs.Shown[0].Message.Should().Contain("nope");
        vm.AllRows[0].IsApplied.Should().BeFalse();
    }

    [Fact]
    public async Task Tile_reflects_the_probe_not_the_switch_the_user_flicked()
    {
        // Apply "succeeds" but the probe says it never took. The tile must tell the truth.
        var t = Tweak("t.lies");
        t.Setup(x => x.ApplyAsync(It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(TweakResult.Success("undo"));
        t.Setup(x => x.ProbeAsync(It.IsAny<CancellationToken>())).ReturnsAsync(TweakState.NotApplied);

        var vm = Build(new[] { t.Object }, new RecordingDialogService());

        await vm.ToggleAsync(vm.AllRows[0], wantApplied: true);

        vm.AllRows[0].IsApplied.Should().BeFalse();
    }

    [Fact]
    public async Task Revert_without_undo_data_explains_rather_than_silently_failing()
    {
        var t = Tweak("t.noundo");
        var dialogs = new RecordingDialogService();
        var vm = Build(new[] { t.Object }, dialogs);

        await vm.ToggleAsync(vm.AllRows[0], wantApplied: false);

        dialogs.Shown.Should().ContainSingle();
        dialogs.Shown[0].Message.Should().Contain("undo information");
        t.Verify(x => x.RevertAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Access_denied_does_not_tell_an_elevated_user_to_run_as_admin()
    {
        // Crustcut runs elevated, so "run as administrator" would be wrong and useless advice.
        var t = Tweak("t.protected");
        t.Setup(x => x.ApplyAsync(It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
         .ThrowsAsync(new UnauthorizedAccessException("Attempted to perform an unauthorized operation"));

        var dialogs = new RecordingDialogService();
        var vm = Build(new[] { t.Object }, dialogs);

        await vm.ToggleAsync(vm.AllRows[0], wantApplied: true);

        dialogs.Shown.Should().ContainSingle();
        dialogs.Shown[0].Message.Should().NotContain("Run as administrator");
        dialogs.Shown[0].Message.Should().Contain("protected or managed by a policy");
        dialogs.Shown[0].Kind.Should().Be(DialogKind.Warning);
    }

    [Fact]
    public async Task Busy_flag_clears_even_when_the_tweak_throws()
    {
        var t = Tweak("t.throws");
        t.Setup(x => x.ApplyAsync(It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
         .ThrowsAsync(new InvalidOperationException("boom"));

        var vm = Build(new[] { t.Object }, new RecordingDialogService());

        await vm.ToggleAsync(vm.AllRows[0], wantApplied: true);

        vm.AllRows[0].IsBusy.Should().BeFalse();
    }

    [Fact]
    public void Search_filters_by_name_and_description()
    {
        var a = Tweak("t.a", "Disable Game DVR");
        var b = Tweak("t.b", "Flush DNS");
        var vm = Build(new[] { a.Object, b.Object }, new RecordingDialogService());

        vm.SearchText = "dvr";

        vm.VisibleRows.Should().ContainSingle().Which.DisplayName.Should().Be("Disable Game DVR");
    }

    [Fact]
    public void Clearing_search_restores_every_row()
    {
        var vm = Build(new[] { Tweak("t.a", "Alpha").Object, Tweak("t.b", "Beta").Object },
                       new RecordingDialogService());

        vm.SearchText = "alpha";
        vm.SearchText = "";

        vm.VisibleRows.Should().HaveCount(2);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
    }
}
