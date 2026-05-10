using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PrimeOSTuner.UI.Controls;

/// <summary>
/// Stat tile with a hand-rolled sparkline.
///
/// History was previously a LiveCharts CartesianChart, but that combo (LiveCharts2 rc5.4 +
/// transitive SkiaSharp 3.116) wasn't reliably picking up ObservableCollection mutations and
/// the line silently never drew. A Polyline + Polygon fed from CollectionChanged is ~30 lines,
/// renders natively through WPF, and has no third-party dependency.
/// </summary>
public partial class StatCard : UserControl
{
    public static readonly DependencyProperty StatNameProperty =
        DependencyProperty.Register(nameof(StatName), typeof(string), typeof(StatCard),
            new PropertyMetadata("", (d, e) => ((StatCard)d).NameText.Text = (string)e.NewValue));

    public static readonly DependencyProperty ValueTextProperty =
        DependencyProperty.Register(nameof(ValueText), typeof(string), typeof(StatCard),
            new PropertyMetadata("", (d, e) => ((StatCard)d).ValueTextBlock.Text = (string)e.NewValue));

    public static readonly DependencyProperty SubTextProperty =
        DependencyProperty.Register(nameof(SubText), typeof(string), typeof(StatCard),
            new PropertyMetadata("", (d, e) => ((StatCard)d).SubTextBlock.Text = (string)e.NewValue));

    public static readonly DependencyProperty HistoryProperty =
        DependencyProperty.Register(nameof(History), typeof(ObservableCollection<double>), typeof(StatCard),
            new PropertyMetadata(null, OnHistoryChanged));

    public string StatName { get => (string)GetValue(StatNameProperty); set => SetValue(StatNameProperty, value); }
    public string ValueText { get => (string)GetValue(ValueTextProperty); set => SetValue(ValueTextProperty, value); }
    public string SubText { get => (string)GetValue(SubTextProperty); set => SetValue(SubTextProperty, value); }
    public ObservableCollection<double> History
    {
        get => (ObservableCollection<double>)GetValue(HistoryProperty);
        set => SetValue(HistoryProperty, value);
    }

    private ObservableCollection<double>? _subscribed;

    private static readonly string DiagPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PrimeOSTuner", "statcard-diag.log");
    private static void Diag(string msg)
    {
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(DiagPath)!);
            System.IO.File.AppendAllText(DiagPath, $"{DateTime.Now:HH:mm:ss.fff} {msg}\n");
        } catch { }
    }

    public StatCard()
    {
        InitializeComponent();
        SparkHost.SizeChanged += (_, _) => Redraw();
    }

    private static void OnHistoryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (StatCard)d;
        if (card._subscribed is not null)
            card._subscribed.CollectionChanged -= card.OnCollectionChanged;

        card._subscribed = e.NewValue as ObservableCollection<double>;
        if (card._subscribed is not null)
            card._subscribed.CollectionChanged += card.OnCollectionChanged;

        card.Redraw();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Redraw();

    private void Redraw()
    {
        var values = _subscribed;
        var w = SparkHost.ActualWidth;
        var h = SparkHost.ActualHeight;
        if (values is null || values.Count < 2 || w <= 0 || h <= 0)
        {
            SparkLine.Data = null;
            SparkFill.Data = null;
            return;
        }

        // Auto-scale Y to the visible range so small fluctuations are actually visible.
        // Without this, a CPU sitting at 27-29% would look like a perfectly flat line.
        double minV = values[0], maxV = values[0];
        for (int i = 1; i < values.Count; i++)
        {
            var v = values[i];
            if (v < minV) minV = v;
            if (v > maxV) maxV = v;
        }
        var center = (minV + maxV) * 0.5;
        // Floor the visible range so a totally steady value doesn't get amplified into
        // a huge jagged line. Keep at least 2 units of range, or 5% of the magnitude.
        var minRange = Math.Max(2.0, Math.Abs(center) * 0.05);
        var range = Math.Max(maxV - minV, minRange);
        var pad = range * 0.18;
        var yLo = minV - pad;
        var yHi = yLo + range + pad * 2;

        var n = values.Count;
        var stepX = w / (n - 1);

        var pts = new Point[n];
        for (int i = 0; i < n; i++)
        {
            var v = values[i];
            var x = i * stepX;
            var y = h - ((v - yLo) / (yHi - yLo)) * h;
            pts[i] = new Point(x, y);
        }

        // Stroked line: a single PathFigure with all points.
        var lineFigure = new PathFigure { StartPoint = pts[0], IsClosed = false };
        for (int i = 1; i < n; i++) lineFigure.Segments.Add(new LineSegment(pts[i], isStroked: true));
        SparkLine.Data = new PathGeometry(new[] { lineFigure });

        // Filled area: same path closed along the bottom edge.
        var fillFigure = new PathFigure { StartPoint = pts[0], IsClosed = true };
        for (int i = 1; i < n; i++) fillFigure.Segments.Add(new LineSegment(pts[i], isStroked: true));
        fillFigure.Segments.Add(new LineSegment(new Point(w, h), isStroked: false));
        fillFigure.Segments.Add(new LineSegment(new Point(0, h), isStroked: false));
        SparkFill.Data = new PathGeometry(new[] { fillFigure });
    }
}
