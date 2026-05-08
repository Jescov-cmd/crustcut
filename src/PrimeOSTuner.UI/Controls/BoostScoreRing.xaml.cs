using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PrimeOSTuner.UI.Controls;

public partial class BoostScoreRing : UserControl
{
    public static readonly DependencyProperty ScoreProperty = DependencyProperty.Register(
        nameof(Score), typeof(int), typeof(BoostScoreRing),
        new PropertyMetadata(0, OnScoreChanged));

    public int Score
    {
        get => (int)GetValue(ScoreProperty);
        set => SetValue(ScoreProperty, value);
    }

    public BoostScoreRing()
    {
        InitializeComponent();
        UpdateArc(0);
    }

    private static void OnScoreChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ring = (BoostScoreRing)d;
        var v = (int)e.NewValue;
        ring.ScoreText.TargetValue = v;
        ring.UpdateArc(v);
    }

    private void UpdateArc(int score)
    {
        const double radius = 55;
        const double cx = 60;
        const double cy = 60;
        var fraction = Math.Clamp(score, 0, 100) / 100.0;
        var angle = fraction * 360.0;
        if (angle <= 0)
        {
            Arc.Data = null;
            return;
        }
        var rad = (angle - 90) * Math.PI / 180.0;
        var endX = cx + radius * Math.Cos(rad);
        var endY = cy + radius * Math.Sin(rad);
        var isLargeArc = angle > 180;

        var fig = new PathFigure
        {
            StartPoint = new Point(cx, cy - radius),
            IsClosed = false
        };
        fig.Segments.Add(new ArcSegment
        {
            Point = new Point(endX, endY),
            Size = new Size(radius, radius),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = isLargeArc
        });

        var geom = new PathGeometry();
        geom.Figures.Add(fig);
        Arc.Data = geom;
    }
}
