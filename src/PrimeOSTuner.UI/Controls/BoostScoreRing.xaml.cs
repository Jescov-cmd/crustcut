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
        ring.ScoreText.Text = v.ToString();
        ring.UpdateArc(v);

        var gradient = Application.Current?.Resources["AccentGradientBrush"] as Brush
                       ?? Application.Current?.Resources["AccentBrush"] as Brush
                       ?? Brushes.Coral;
        ring.Arc.Stroke = gradient;
        ring.ScoreText.Foreground = Application.Current?.Resources["AccentBrush"] as Brush ?? Brushes.Coral;
    }

    private void UpdateArc(int score)
    {
        const double radius = 46;   // 100/2 - half stroke
        const double cx = 50;
        const double cy = 50;
        var fraction = System.Math.Clamp(score, 0, 100) / 100.0;
        var angle = fraction * 360.0;
        if (angle <= 0)
        {
            Arc.Data = null;
            return;
        }
        var rad = (angle - 90) * System.Math.PI / 180.0;
        var endX = cx + radius * System.Math.Cos(rad);
        var endY = cy + radius * System.Math.Sin(rad);
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
