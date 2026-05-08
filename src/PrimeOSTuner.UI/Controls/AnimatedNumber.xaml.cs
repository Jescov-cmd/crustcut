using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace PrimeOSTuner.UI.Controls;

public partial class AnimatedNumber : UserControl
{
    public static readonly DependencyProperty TargetValueProperty = DependencyProperty.Register(
        nameof(TargetValue), typeof(double), typeof(AnimatedNumber),
        new PropertyMetadata(0.0, OnTargetValueChanged));

    public static readonly DependencyProperty DisplayValueProperty = DependencyProperty.Register(
        nameof(DisplayValue), typeof(double), typeof(AnimatedNumber),
        new PropertyMetadata(0.0, OnDisplayValueChanged));

    public static readonly DependencyProperty FormatProperty = DependencyProperty.Register(
        nameof(Format), typeof(string), typeof(AnimatedNumber),
        new PropertyMetadata("0"));

    public double TargetValue
    {
        get => (double)GetValue(TargetValueProperty);
        set => SetValue(TargetValueProperty, value);
    }

    public double DisplayValue
    {
        get => (double)GetValue(DisplayValueProperty);
        set => SetValue(DisplayValueProperty, value);
    }

    public string Format
    {
        get => (string)GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    public AnimatedNumber()
    {
        InitializeComponent();
        Label.Text = "0";
    }

    private static void OnTargetValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (AnimatedNumber)d;
        var from = ctrl.DisplayValue;
        var to = (double)e.NewValue;
        if (from == to) return;

        var anim = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(800),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        anim.Completed += (_, _) => ctrl.DisplayValue = to;
        ctrl.BeginAnimation(DisplayValueProperty, anim);
    }

    private static void OnDisplayValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (AnimatedNumber)d;
        var v = (double)e.NewValue;
        ctrl.Label.Text = v.ToString(ctrl.Format);
    }
}
