using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using PrimeOSTuner.Core.Education;

namespace PrimeOSTuner.UI.Controls;

/// <summary>
/// Renders a guide's markdown body (headings, paragraphs, bullet/numbered lists,
/// inline bold) into a stack of themed TextBlocks. Bind the <see cref="Markdown"/>
/// property; the visual tree rebuilds whenever it changes.
/// </summary>
public sealed class MarkdownView : ContentControl
{
    private static readonly Brush HeadingBrush = Frozen(0xF4, 0xF4, 0xF6);
    private static readonly Brush BodyBrush = Frozen(0xC9, 0xC9, 0xD2);
    private static readonly Brush MarkerBrush = Frozen(0x8A, 0x8A, 0x96);

    public static readonly DependencyProperty MarkdownProperty =
        DependencyProperty.Register(
            nameof(Markdown), typeof(string), typeof(MarkdownView),
            new PropertyMetadata(null, OnMarkdownChanged));

    public string? Markdown
    {
        get => (string?)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((MarkdownView)d).Rebuild();

    private void Rebuild()
    {
        if (string.IsNullOrWhiteSpace(Markdown))
        {
            Content = null;
            return;
        }

        var panel = new StackPanel();
        foreach (var block in MarkdownDocument.Parse(Markdown))
            panel.Children.Add(RenderBlock(block));
        Content = panel;
    }

    private static UIElement RenderBlock(MarkdownBlock block) => block.Kind switch
    {
        MarkdownBlockKind.Heading => Heading(block),
        MarkdownBlockKind.BulletList => List(block, numbered: false),
        MarkdownBlockKind.NumberedList => List(block, numbered: true),
        _ => Paragraph(block.Items.Count > 0 ? block.Items[0] : string.Empty),
    };

    private static UIElement Heading(MarkdownBlock block) => new TextBlock
    {
        Text = block.Items.Count > 0 ? block.Items[0] : string.Empty,
        Foreground = HeadingBrush,
        FontWeight = FontWeights.SemiBold,
        FontSize = block.HeadingLevel <= 2 ? 16 : 14,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 18, 0, 6),
    };

    private static UIElement Paragraph(string text)
    {
        var tb = new TextBlock
        {
            Foreground = BodyBrush,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 21,
            Margin = new Thickness(0, 0, 0, 8),
        };
        AddInlines(tb, text);
        return tb;
    }

    private static UIElement List(MarkdownBlock block, bool numbered)
    {
        var panel = new StackPanel { Margin = new Thickness(2, 0, 0, 8) };
        for (var i = 0; i < block.Items.Count; i++)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(numbered ? 26 : 16) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var marker = new TextBlock
            {
                Text = numbered ? $"{i + 1}." : "•",
                Foreground = MarkerBrush,
                FontSize = 13,
                Margin = new Thickness(0, 0, 6, 0),
            };
            Grid.SetColumn(marker, 0);

            var content = new TextBlock
            {
                Foreground = BodyBrush,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 21,
            };
            AddInlines(content, block.Items[i]);
            Grid.SetColumn(content, 1);

            grid.Children.Add(marker);
            grid.Children.Add(content);
            panel.Children.Add(grid);
        }
        return panel;
    }

    private static void AddInlines(TextBlock target, string text)
    {
        foreach (var span in MarkdownDocument.ParseInline(text))
        {
            target.Inlines.Add(new Run(span.Text)
            {
                FontWeight = span.Bold ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = span.Bold ? HeadingBrush : target.Foreground,
            });
        }
    }

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
