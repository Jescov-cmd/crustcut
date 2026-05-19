using FluentAssertions;
using PrimeOSTuner.Core.Education;
using Xunit;

namespace PrimeOSTuner.Tests.Education;

public class GuideCatalogTests
{
    private static string GuideText(string id, string title) =>
        $"""
        ---
        id: {id}
        title: {title}
        category: Windows
        difficulty: Beginner
        risk: Low
        time: 5 minutes
        ---
        Body for {title}.
        """;

    private static string TempDir(params (string File, string Content)[] files)
    {
        var dir = Path.Combine(Path.GetTempPath(), "guidecat-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        foreach (var (file, content) in files)
            File.WriteAllText(Path.Combine(dir, file), content);
        return dir;
    }

    [Fact]
    public void LoadFromDirectory_parses_every_markdown_file()
    {
        var dir = TempDir(
            ("rebar.md", GuideText("enable-rebar", "Enable ReBAR")),
            ("xmp.md", GuideText("enable-xmp", "Enable XMP")));
        try
        {
            var guides = GuideCatalog.LoadFromDirectory(dir);

            guides.Should().HaveCount(2);
            guides.Select(g => g.Id).Should().BeEquivalentTo("enable-rebar", "enable-xmp");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void LoadFromDirectory_ignores_non_markdown_files()
    {
        var dir = TempDir(
            ("rebar.md", GuideText("enable-rebar", "Enable ReBAR")),
            ("notes.txt", "not a guide"));
        try
        {
            GuideCatalog.LoadFromDirectory(dir).Should().ContainSingle();
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void LoadFromDirectory_returns_empty_for_a_directory_with_no_guides()
    {
        var dir = TempDir();
        try
        {
            GuideCatalog.LoadFromDirectory(dir).Should().BeEmpty();
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void LoadFromDirectory_throws_on_a_duplicate_guide_id()
    {
        var dir = TempDir(
            ("a.md", GuideText("dup", "First")),
            ("b.md", GuideText("dup", "Second")));
        try
        {
            var act = () => GuideCatalog.LoadFromDirectory(dir);
            act.Should().Throw<InvalidOperationException>().WithMessage("*dup*");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void LoadFromDirectory_names_the_file_when_a_guide_is_malformed()
    {
        var dir = TempDir(("broken.md", "no frontmatter at all"));
        try
        {
            var act = () => GuideCatalog.LoadFromDirectory(dir);
            act.Should().Throw<FormatException>().WithMessage("*broken.md*");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Every_guide_that_ships_with_the_app_parses_cleanly()
    {
        var guides = GuideCatalog.LoadFromDirectory(GuideCatalog.DefaultDirectory());

        guides.Should().NotBeEmpty();
        guides.Select(g => g.Id).Should().Contain(new[]
        {
            "enable-resizable-bar", "enable-xmp-expo", "nvidia-control-panel",
            "disable-nic-power-management", "clean-gpu-driver-install",
            "update-motherboard-bios", "disable-visual-effects",
            "low-latency-audio", "clean-pc-dust",
        });
    }
}
