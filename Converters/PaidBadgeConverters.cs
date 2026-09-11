using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Obcred.Converters;

/// <summary>
/// Small set of bool-&gt;visual converters for the local-only "paid" flag on received
/// invoices. UJP has no such concept; this is purely our own badge styling.
/// </summary>
public static class PaidBadgeConverters
{
    public static readonly IValueConverter LabelConverter =
        new FuncValueConverter<bool, string>(paid => paid ? "PAID" : "UNPAID");

    public static readonly IValueConverter ToggleLabelConverter =
        new FuncValueConverter<bool, string>(paid => paid ? "Paid" : "Mark as Paid");

    public static readonly IValueConverter BackgroundConverter =
        new FuncValueConverter<bool, IBrush>(paid => paid
            ? new SolidColorBrush(Color.Parse("#DDF5EA"))
            : new SolidColorBrush(Color.Parse("#FDE8E7")));

    public static readonly IValueConverter ForegroundConverter =
        new FuncValueConverter<bool, IBrush>(paid => paid
            ? new SolidColorBrush(Color.Parse("#0F9D6A"))
            : new SolidColorBrush(Color.Parse("#D93025")));

    public static readonly IValueConverter DotColorConverter =
        new FuncValueConverter<bool, Color>(paid => paid
            ? Color.Parse("#0F9D6A")
            : Color.Parse("#D93025"));
}