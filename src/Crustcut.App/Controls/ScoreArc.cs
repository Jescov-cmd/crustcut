using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Crustcut.App.Controls;

/// <summary>
/// Gradient arc that fills to <see cref="Score"/>. Rendered with a deliberate margin
/// inside its bounds so the glow has room to fall off — clipping it produces a visible
/// square edge, which is exactly the defect the redesign was correcting.
/// </summary>
public sealed class ScoreArc : Control
{
    public static readonly StyledProperty<double> ScoreProperty =
        AvaloniaProperty.Register<ScoreArc, double>(nameof(Score));

    public double Score
    {
        get => GetValue(ScoreProperty);
        set => SetValue(ScoreProperty, value);
    }

    static ScoreArc()
    {
        AffectsRender<ScoreArc>(ScoreProperty);
    }

    public override void Render(DrawingContext ctx)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0) return;

        const double thickness = 7;
        // Inset by more than half the stroke so the round cap never touches the edge.
        var inset = thickness;
        var radius = (size - inset * 2) / 2;
        if (radius <= 0) return;

        var centre = new Point(Bounds.Width / 2, Bounds.Height / 2);

        var track = new Pen(new SolidColorBrush(Color.Parse("#12FFFFFF")), thickness);
        ctx.DrawEllipse(null, track, centre, radius, radius);

        var fraction = Math.Clamp(Score, 0, 100) / 100.0;
        if (fraction <= 0) return;

        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#F2D6A8"), 0),
                new GradientStop(Color.Parse("#E8C088"), 0.55),
                new GradientStop(Color.Parse("#B4763A"), 1),
            }
        };

        var pen = new Pen(brush, thickness, lineCap: PenLineCap.Round);

        var start = new Point(centre.X, centre.Y - radius);          // 12 o'clock
        var sweep = 2 * Math.PI * fraction;
        var end = new Point(
            centre.X + radius * Math.Sin(sweep),
            centre.Y - radius * Math.Cos(sweep));

        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(start, false);
            g.ArcTo(end, new Size(radius, radius), 0,
                    isLargeArc: fraction > 0.5, SweepDirection.Clockwise);
            g.EndFigure(false);
        }

        ctx.DrawGeometry(null, pen, geometry);
    }
}
