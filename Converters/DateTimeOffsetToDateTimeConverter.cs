using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Obcred.Converters;

/// <summary>
/// Bridges the view models' DateTimeOffset date properties to CalendarDatePicker,
/// whose SelectedDate is a DateTime?. Purely a view-layer adapter so the date
/// fields can use the compact text-plus-calendar control instead of the stock
/// three-segment DatePicker spinner.
/// </summary>
public class DateTimeOffsetToDateTimeConverter : IValueConverter
{
    public static readonly DateTimeOffsetToDateTimeConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateTimeOffset dto ? dto.LocalDateTime : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Clearing the picker leaves the view model's date untouched rather than
        // pushing a null into a non-nullable property.
        if (value is not DateTime dt) return BindingOperations.DoNothing;
        return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Local));
    }
}
