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
            ? new SolidColorBrush(Color.Parse("#DCFCE7"))
            : new SolidColorBrush(Color.Parse("#FEE2E2")));

    public static readonly IValueConverter ForegroundConverter =
        new FuncValueConverter<bool, IBrush>(paid => paid
            ? new SolidColorBrush(Color.Parse("#059669"))
            : new SolidColorBrush(Color.Parse("#DC2626")));

    public static readonly IValueConverter DotColorConverter =
        new FuncValueConverter<bool, Color>(paid => paid
            ? Color.Parse("#00B894")
            : Color.Parse("#FF4757"));
}