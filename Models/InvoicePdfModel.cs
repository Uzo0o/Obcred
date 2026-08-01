using System.Collections.Generic;

namespace Obcred.Models;

/// <summary>
/// A flat, self-contained snapshot of an invoice used to render a printable PDF.
/// It is also serialized into the invoice audit record so a past invoice can be
/// re-printed later without re-deriving anything.
/// </summary>
public class InvoicePdfModel
{
    public string DocNumber { get; set; } = string.Empty;
    public string DocTypeName { get; set; } = "Фактура";
    public string IssueDate { get; set; } = string.Empty;
    public string TurnoverDate { get; set; } = string.Empty;

    public string SellerName { get; set; } = string.Empty;
    public string SellerEdb { get; set; } = string.Empty;
    public string SellerVatNumber { get; set; } = string.Empty;
    public string SellerAddress { get; set; } = string.Empty;

    public string BuyerName { get; set; } = string.Empty;
    public string BuyerEdb { get; set; } = string.Empty;
    public string BuyerVatNumber { get; set; } = string.Empty;
    public string BuyerAddress { get; set; } = string.Empty;

    public List<InvoicePdfLine> Lines { get; set; } = new();

    public decimal NetAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public string Currency { get; set; } = "MKD";
}

public class InvoicePdfLine
{
    public int LineNo { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public string Unit { get; set; } = "pcs";
    public decimal UnitPrice { get; set; }
    public string VatLabel { get; set; } = string.Empty;
    public decimal LineNet { get; set; }
    public decimal LineVat { get; set; }
    public decimal LineGross { get; set; }
}
