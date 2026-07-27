using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Crustcut.App.Controls;

/// <summary>A filled line chart of recent values. Scales to its own min/max.</summary>
public sealed class Sparkline : Control
{
    public static readonly StyledProperty<IEnumerable?> ValuesProperty =
        AvaloniaProperty.Register<Sparkline, IEnumerable?>(nameof(Values));

    /// <summary>
    /// Fixed upper bound for the Y axis. Percentages should set 100 so a 19% reading draws
    /// near the floor instead of filling the box — auto-scaling to the series' own min/max
    /// makes a calm 19% look identical to a pegged 95%.
    /// </summary>
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<Sparkline, double>(nameof(Maximum), double.NaN);

    public IEnumerable? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    static Sparkline()
    {
        AffectsRender<Sparkline>(ValuesProperty, MaximumProperty);
    }

    public override void Render(DrawingContext ctx)
    {
        if (Values is null) return;

        var points = new List<double>();
        foreach (var v in Values)
            if (v is IConvertible c) points.Add(c.ToDouble(null));

        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;

        // A single sample still deserves a line — duplicate it so there's something to draw
        // while history builds up, rather than an empty card for the first few seconds.
        if (points.Count == 1) points.Add(points[0]);
        if (points.Count < 2) return;

        double min, range;
        if (!double.IsNaN(Maximum))
        {
            min = 0;
            range = Maximum;
        }
        else
        {
            min = points.Min();
            range = points.Max() - min;
            if (range <= double.Epsilon) range = 1;   // flat series — draw a level line
        }

        var w = Bounds.Width;
        var h = Bounds.Height;
        var step = w / (points.Count - 1);

        Point At(int i) => new(i * step, h - ((points[i] - min) / range) * h);

        var line = new StreamGeometry();
        using (var g = line.Open())
        {
            g.BeginFigure(At(0), false);
            for (var i = 1; i < points.Count; i++) g.LineTo(At(i));
            g.EndFigure(false);
        }

        var fill = new StreamGeometry();
        using (var g = fill.Open())
        {
            g.BeginFigure(new Point(0, h), true);
            for (var i = 0; i < points.Count; i++) g.LineTo(At(i));
            g.LineTo(new Point(w, h));
            g.EndFigure(true);
        }

        var area = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#57E8C088"), 0),
                new GradientStop(Color.Parse("#00E8C088"), 1),
            }
        };

        ctx.DrawGeometry(area, null, fill);
        ctx.DrawGeometry(null, new Pen(new SolidColorBrush(Color.Parse("#E8C088")), 1.4), line);
    }
}
