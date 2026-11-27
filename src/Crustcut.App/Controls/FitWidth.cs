using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Crustcut.App.Controls;

/// <summary>
/// Put directly inside a vertical ScrollViewer. Measures its content at exactly the
/// width you can see, so wrapping text gets its real height.
///
/// Without it, a page inside a ScrollViewer was measured wider than the window: rows ran
/// past the right edge. The earlier workaround, binding each row's Width to a parent's
/// Bounds, fixed the width but measured the text height at the wrong width first. The
/// list then came out shorter than it really was, and the last rows ("Deep clean RAM")
/// could never be scrolled into view.
/// </summary>
public sealed class FitWidth : Decorator
{
    private ScrollViewer? _scroller;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _scroller = this.FindAncestorOfType<ScrollViewer>();
        if (_scroller is not null) _scroller.PropertyChanged += OnScrollerChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_scroller is not null) _scroller.PropertyChanged -= OnScrollerChanged;
        _scroller = null;
    }

    private void OnScrollerChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty || e.Property == ScrollViewer.ViewportProperty)
            InvalidateMeasure();
    }

    // Page gutters live HERE (Padding), never on the ScrollViewer: Avalonia's ScrollViewer
    // offsets its content by its own Padding but leaves the padding out of the scroll
    // extent, so the bottom padding's worth of every page could never be scrolled to, and
    // the side padding pushed content past the right edge.
    private double VisibleWidth(double available)
    {
        var width = available;
        if (_scroller is { } sv)
        {
            // Viewport is the visible area. Before the first layout pass it's still zero.
            var viewport = sv.Viewport.Width > 0 ? sv.Viewport.Width : sv.Bounds.Width;
            if (viewport > 0) width = double.IsInfinity(width) ? viewport : Math.Min(width, viewport);
        }
        return double.IsInfinity(width) ? 0 : Math.Max(0, width);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = VisibleWidth(availableSize.Width);
        var p = Padding;
        if (Child is null) return new Size(width, p.Top + p.Bottom);
        Child.Measure(new Size(Math.Max(0, width - p.Left - p.Right), double.PositiveInfinity));
        return new Size(width, Child.DesiredSize.Height + p.Top + p.Bottom);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = VisibleWidth(finalSize.Width);
        var p = Padding;
        // Full width up to the child's MaxWidth, kept on the left (Stretch + MaxWidth would
        // centre it).
        var inner = Math.Max(0, width - p.Left - p.Right);
        if (Child is { } c && !double.IsInfinity(c.MaxWidth)) inner = Math.Min(inner, c.MaxWidth + c.Margin.Left + c.Margin.Right);
        Child?.Arrange(new Rect(p.Left, p.Top, inner,
            Math.Max(0, Math.Max(finalSize.Height - p.Top - p.Bottom, Child.DesiredSize.Height))));
        return new Size(width, finalSize.Height);
    }
}
