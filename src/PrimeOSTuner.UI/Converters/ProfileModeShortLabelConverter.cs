using System;
using System.Globalization;
using System.Windows.Data;

namespace PrimeOSTuner.UI.Converters;

public sealed class ProfileModeShortLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value as string) switch
        {
            "(none)"      => "Off",
            "basic"       => "Basic",
            "performance" => "Perf",
            "custom"      => "Custom",
            _             => value?.ToString() ?? ""
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
