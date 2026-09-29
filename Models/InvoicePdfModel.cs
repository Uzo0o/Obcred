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

    // Reference document (e.g. "По испратница бр. 29/26 од 18.08.2026") — left blank when unused.
    public string DeliveryNoteNumber { get; set; } = string.Empty;
    public string DeliveryNoteDate { get; set; } = string.Empty;

    // Payment terms — mirrors UJP's docPayment.docPaymentTypeDueDays/docPaymentTerms/docPaymentInterest.
    public int? PaymentDueDays { get; set; }
    public string PaymentTermsNote { get; set; } = string.Empty;
    public string PaymentInterestNote { get; set; } = string.Empty;

    // Free-text footer note (dispute jurisdiction, devaluation clause, complaint deadline, etc.).
    public string FooterNote { get; set; } = string.Empty;

    // Fixed footer: bank + contact details, shown on every invoice when filled in.
    public string BankName { get; set; } = string.Empty;
    public string BankAccount { get; set; } = string.Empty;
    public string Iban { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;

    // "Custom" template only: shown on the signature line under the header/footer chrome images.
    public string AuthorizedSignerName { get; set; } = string.Empty;
}

public class InvoicePdfLine
{
    public int LineNo { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public string Unit { get; set; } = "pcs";
    public decimal UnitPrice { get; set; }
    public string VatLabel { get; set; } = string.Empty;
    public decimal LineNet { get; set; }
    public decimal LineVat { get; set; }
    public decimal LineGross { get; set; }
}
