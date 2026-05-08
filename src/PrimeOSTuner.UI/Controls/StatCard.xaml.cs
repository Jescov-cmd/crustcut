using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace PrimeOSTuner.UI.Controls;

public partial class StatCard : UserControl
{
    public static readonly DependencyProperty StatNameProperty =
        DependencyProperty.Register(nameof(StatName), typeof(string), typeof(StatCard),
            new PropertyMetadata("", (d, e) => ((StatCard)d).NameText.Text = (string)e.NewValue));

    public static readonly DependencyProperty ValueTextProperty =
        DependencyProperty.Register(nameof(ValueText), typeof(string), typeof(StatCard),
            new PropertyMetadata("", (d, e) => ApplyValue((StatCard)d, (string)e.NewValue)));

    public static readonly DependencyProperty SubTextProperty =
        DependencyProperty.Register(nameof(SubText), typeof(string), typeof(StatCard),
            new PropertyMetadata("", (d, e) => ((StatCard)d).SubTextBlock.Text = (string)e.NewValue));

    public static readonly DependencyProperty HistoryProperty =
        DependencyProperty.Register(nameof(History), typeof(ObservableCollection<double>), typeof(StatCard),
            new PropertyMetadata(null, OnHistoryChanged));

    public string StatName { get => (string)GetValue(StatNameProperty); set => SetValue(StatNameProperty, value); }
    public string ValueText { get => (string)GetValue(ValueTextProperty); set => SetValue(ValueTextProperty, value); }
    public string SubText { get => (string)GetValue(SubTextProperty); set => SetValue(SubTextProperty, value); }
    public ObservableCollection<double> History { get => (ObservableCollection<double>)GetValue(HistoryProperty); set => SetValue(HistoryProperty, value); }

    public StatCard()
    {
        InitializeComponent();
        Spark.XAxes = new[] { new Axis { IsVisible = false } };
        Spark.YAxes = new[] { new Axis { IsVisible = false, MinLimit = 0, MaxLimit = 100 } };
    }

    private static void ApplyValue(StatCard card, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            card.ValueAnimated.TargetValue = 0;
            return;
        }
        var digits = new string(text.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
        if (double.TryParse(digits,
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out var num))
        {
            card.ValueAnimated.Format = digits.Contains('.') ? "0.0" : "0";
            card.ValueAnimated.TargetValue = num;
        }
    }

    private static void OnHistoryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (StatCard)d;
        if (e.NewValue is ObservableCollection<double> coll)
        {
            card.Spark.Series = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = coll,
                    GeometrySize = 0,
                    Stroke = new SolidColorPaint(new SKColor(0, 229, 197)) { StrokeThickness = 2 },
                    Fill = new SolidColorPaint(new SKColor(0, 229, 197, 80)),
                    LineSmoothness = 0.4
                }
            };
        }
    }
}
