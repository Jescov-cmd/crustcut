using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PrimeOSTuner.UI.Controls;

public partial class BoostScoreRing : UserControl
{
    public static readonly DependencyProperty ScoreProperty =
        DependencyProperty.Register(nameof(Score), typeof(int), typeof(BoostScoreRing),
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
        if (d is BoostScoreRing r && e.NewValue is int v)
        {
            r.ScoreText.Text = v.ToString();
            r.UpdateArc(v);
        }
    }

    private void UpdateArc(int score)
    {
        score = Math.Clamp(score, 0, 100);
        const double cx = 60, cy = 60, r = 55;
        var angle = score / 100.0 * 360.0;
        var rad = (angle - 90) * Math.PI / 180.0;
        var endX = cx + r * Math.Cos(rad);
        var endY = cy + r * Math.Sin(rad);
        var largeArc = angle > 180 ? 1 : 0;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(cx, cy - r), isFilled: false, isClosed: false);
            ctx.ArcTo(new Point(endX, endY),
                new Size(r, r), 0, largeArc == 1, SweepDirection.Clockwise, true, false);
        }
        geometry.Freeze();
        Arc.Data = geometry;
    }
}
