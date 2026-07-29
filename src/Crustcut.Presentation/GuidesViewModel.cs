using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Education;

namespace Crustcut.Presentation;

public enum GuideBlockKind { Heading, SubHeading, Bullet, Paragraph }

/// <summary>One renderable chunk of a guide body.</summary>
public sealed record GuideBlock(GuideBlockKind Kind, string Text)
{
    public bool IsHeading => Kind == GuideBlockKind.Heading;
    public bool IsSubHeading => Kind == GuideBlockKind.SubHeading;
    public bool IsBullet => Kind == GuideBlockKind.Bullet;
    public bool IsParagraph => Kind == GuideBlockKind.Paragraph;
}

public sealed class GuideListItemVm
{
    public Guide Guide { get; }
    public GuideListItemVm(Guide guide) => Guide = guide;
    public string Title => Guide.Title;
    public string Meta => $"{Guide.Difficulty} · {Guide.Risk} risk · {Guide.EstimatedTime}";
}

/// <summary>
/// Guides page. Renders the markdown bodies with a deliberately small line-based parser —
/// headings, bullets, paragraphs — rather than porting the WPF FlowDocument control. The
/// guides only use that subset.
/// </summary>
public partial class GuidesViewModel : ObservableObject
{
    public ObservableCollection<GuideListItemVm> Guides { get; } = new();
    public ObservableCollection<GuideBlock> Blocks { get; } = new();

    [ObservableProperty] private string _status = "";
    [ObservableProperty] private GuideListItemVm? _selected;
    [ObservableProperty] private bool _hasSelection;

    public GuidesViewModel()
    {
        try
        {
            foreach (var g in GuideCatalog.LoadFromDirectory(GuideCatalog.DefaultDirectory())
                                          .OrderBy(g => g.Difficulty))
                Guides.Add(new GuideListItemVm(g));
            Status = $"{Guides.Count} guide(s) — manual tweaks worth doing that no app should automate.";
        }
        catch (Exception ex)
        {
            Status = $"Couldn't load guides: {ex.Message}";
        }
    }

    partial void OnSelectedChanged(GuideListItemVm? value)
    {
        Blocks.Clear();
        HasSelection = value is not null;
        if (value is null) return;
        foreach (var block in Parse(value.Guide.MarkdownBody)) Blocks.Add(block);
    }

    public static IEnumerable<GuideBlock> Parse(string markdown)
    {
        var paragraph = new List<string>();

        IEnumerable<GuideBlock> FlushParagraph()
        {
            if (paragraph.Count == 0) yield break;
            yield return new GuideBlock(GuideBlockKind.Paragraph, string.Join(" ", paragraph));
            paragraph.Clear();
        }

        foreach (var raw in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            var trimmed = line.TrimStart();

            if (trimmed.Length == 0)
            {
                foreach (var b in FlushParagraph()) yield return b;
            }
            else if (trimmed.StartsWith("## "))
            {
                foreach (var b in FlushParagraph()) yield return b;
                yield return new GuideBlock(GuideBlockKind.SubHeading, Clean(trimmed[3..]));
            }
            else if (trimmed.StartsWith("# "))
            {
                foreach (var b in FlushParagraph()) yield return b;
                yield return new GuideBlock(GuideBlockKind.Heading, Clean(trimmed[2..]));
            }
            else if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
            {
                foreach (var b in FlushParagraph()) yield return b;
                yield return new GuideBlock(GuideBlockKind.Bullet, Clean(trimmed[2..]));
            }
            else if (trimmed.Length > 2 && char.IsDigit(trimmed[0]) && trimmed[1] == '.')
            {
                foreach (var b in FlushParagraph()) yield return b;
                yield return new GuideBlock(GuideBlockKind.Bullet, Clean(trimmed));
            }
            else
            {
                paragraph.Add(Clean(trimmed));
            }
        }
        foreach (var b in FlushParagraph()) yield return b;
    }

    // Strip the inline markup the guides use (bold/italic/code) — plain text renders fine.
    private static string Clean(string s) =>
        s.Replace("**", "").Replace("*", "").Replace("`", "");
}
