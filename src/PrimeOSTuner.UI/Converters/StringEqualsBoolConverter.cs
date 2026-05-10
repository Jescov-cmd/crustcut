using System;
using System.Globalization;
using System.Windows.Data;

namespace PrimeOSTuner.UI.Converters;

public sealed class StringEqualsBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var v = value as string ?? "";
        var p = parameter as string ?? "";
        return string.Equals(v, p, StringComparison.OrdinalIgnoreCase);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b) return parameter as string;
        return Binding.DoNothing;
    }
}
