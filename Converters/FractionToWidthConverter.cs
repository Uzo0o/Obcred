using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Obcred.Converters;

/// <summary>
/// Turns a 0..1 fraction (e.g. UsageProgressFraction) into a pixel width for a
/// manually-drawn progress bar fill, given the track's full width as the
/// ConverterParameter. Avalonia has no built-in "percentage of parent" binding
/// for a Border's Width, so this does the arithmetic in code instead.
/// </summary>
public class FractionToWidthConverter : IValueConverter
{
    public static readonly FractionToWidthConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double fraction) return 0.0;
        if (parameter is not string paramStr || !double.TryParse(paramStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double fullWidth))
            return 0.0;

        return Math.Clamp(fraction, 0, 1) * fullWidth;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}