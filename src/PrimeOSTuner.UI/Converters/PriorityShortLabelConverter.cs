using System;
using System.Globalization;
using System.Windows.Data;
using PrimeOSTuner.Core.Memory;

namespace PrimeOSTuner.UI.Converters;

public sealed class PriorityShortLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value switch
        {
            PriorityLevel.BelowNormal => "Below",
            PriorityLevel.Normal      => "Normal",
            PriorityLevel.AboveNormal => "Above",
            PriorityLevel.High        => "High",
            _ => value?.ToString() ?? ""
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
