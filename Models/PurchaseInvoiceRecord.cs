using CommunityToolkit.Mvvm.ComponentModel;
using SQLite;

namespace Obcred.Models;

/// <summary>
/// A locally cached snapshot of one incoming invoice from UJP, plus a paid/unpaid flag
/// that only exists in this app — UJP's API has no concept of payment status, only
/// document lifecycle status (sent/accepted/rejected/cancelled etc.), so tracking who's
/// actually been paid is entirely our own bookkeeping on top of their data.
/// </summary>
public partial class PurchaseInvoiceRecord : ObservableObject
{
    [PrimaryKey]
    public string Euid { get; set; } = string.Empty;

    public string DocNumber { get; set; } = string.Empty;
    public string DocDate { get; set; } = string.Empty;
    public string? DocDeliveryDate { get; set; }

    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;

    public decimal NetAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal AvansAmount { get; set; }
    public decimal FinalAmount { get; set; }

    public string SellerTin { get; set; } = string.Empty;
    public string SellerName { get; set; } = string.Empty;
    public string SellerCity { get; set; } = string.Empty;
    public string SellerStreet { get; set; } = string.Empty;
    public string SellerNumber { get; set; } = string.Empty;

    public string BuyerTin { get; set; } = string.Empty;

    // Local-only bookkeeping; never comes from UJP.
    [ObservableProperty]
    private bool _isPaid;

    public System.DateTime LastSyncedUtc { get; set; } = System.DateTime.UtcNow;
}