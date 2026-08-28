using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Obcred.Converters;

/// <summary>
/// True (IsCurrent) -> the brand accent border; false -> the standard light border.
/// Colors are hardcoded here (matching App.axaml's BrandAccent/BorderLight) since a
/// plain IValueConverter doesn't have convenient access to StaticResource lookups —
/// keep these in sync if those brand colors ever change.
/// </summary>
public class BoolToBrandAccentConverter : IValueConverter
{
    public static readonly BoolToBrandAccentConverter Instance = new();

    private static readonly IBrush Accent = new SolidColorBrush(Color.Parse("#2E86FF"));
    private static readonly IBrush Light = new SolidColorBrush(Color.Parse("#E2E8F0"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Accent : Light;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}