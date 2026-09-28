using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Obcred.Converters;

/// <summary>
/// True when a count is zero. Used to show an empty-state message over a list
/// or grid that has nothing in it yet (e.g. the line-items table on a fresh invoice).
/// </summary>
public class IsZeroConverter : IValueConverter
{
    public static readonly IsZeroConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int i && i == 0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
