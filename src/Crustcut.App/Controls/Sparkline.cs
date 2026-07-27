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

    public IEnumerable? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    static Sparkline()
    {
        AffectsRender<Sparkline>(ValuesProperty);
    }

    public override void Render(DrawingContext ctx)
    {
        if (Values is null) return;

        var points = new List<double>();
        foreach (var v in Values)
            if (v is IConvertible c) points.Add(c.ToDouble(null));

        if (points.Count < 2 || Bounds.Width <= 0 || Bounds.Height <= 0) return;

        var min = points.Min();
        var max = points.Max();
        var range = max - min;
        if (range <= double.Epsilon) range = 1;   // flat series — draw a level line

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
