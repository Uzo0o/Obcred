using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Obcred.Converters;

/// <summary>
/// Lets a RadioButton's IsChecked be driven by "does this card's Id match the
/// currently selected template Id" — the same pattern as WPF's classic
/// enum-to-bool-by-parameter converter, just keyed by string instead.
/// </summary>
public class StringEqualsConverter : IValueConverter
{
    public static readonly StringEqualsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? parameter : BindingOperations.DoNothing;
}