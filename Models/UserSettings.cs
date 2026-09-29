namespace Obcred.Models;

public class UserSettings
{
    public string CertPath { get; set; } = string.Empty;
    public string CertPassword { get; set; } = string.Empty; 
    public string CertThumbprint { get; set; } = string.Empty;
    public string EujpId { get; set; } = string.Empty;
    public string SellerEdb { get; set; } = string.Empty;

    // Optional prefix for the sequential invoice number, e.g. "" => 2026-0001, "INV-" => INV-2026-0001.
    public string InvoiceNumberPrefix { get; set; } = string.Empty;

    // When false (default) the app talks to the UJP TEST sandbox; when true, to LIVE production.
    public bool UseProductionEnvironment { get; set; } = false;

    // PDF branding: which built-in layout to render with, and an optional logo file
    // (a local copy under our AppData folder, so it survives the original being moved/deleted).
    public string PdfTemplate { get; set; } = "Classic";
    public string PdfLogoPath { get; set; } = string.Empty;
    public string PdfAccentColorId { get; set; } = "Blue";
    
    // Cached UJP Data
    public string SellerName { get; set; } = string.Empty;
    public string SellerVatNumber { get; set; } = string.Empty; // NEW
    public string SellerStreet { get; set; } = string.Empty;
    public string SellerNumber { get; set; } = string.Empty;
    public string SellerCity { get; set; } = string.Empty;
    public string SellerZip { get; set; } = string.Empty;

    // Invoice defaults every user can fill in once and reuse on every invoice —
    // these map onto UJP's docHeader/docFooter/docPayment free-text fields
    // (see efakturawiki.ujp.gov.mk "100 Фактура": UJP01-12/13, UJP10-03/05/06)
    // and are what the printed PDF's footer is built from.
    public string BankName { get; set; } = string.Empty;
    public string BankAccount { get; set; } = string.Empty; // жиро сметка
    public string Iban { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;

    // Default payment due period in days; overridable per invoice since it can vary by client.
    public int DefaultPaymentDueDays { get; set; } = 0;

    // Free-text notes reused on every invoice (docPayment.docPaymentTerms / docPaymentInterest,
    // and docFooter — e.g. dispute jurisdiction, currency-devaluation clause, complaint deadline).
    public string PaymentTermsNote { get; set; } = string.Empty;
    public string PaymentInterestNote { get; set; } = string.Empty;
    public string InvoiceFooterNote { get; set; } = string.Empty;

    // Shown on the signature line ("Фактурирал / Овластено лице за потпис на фактура").
    public string AuthorizedSignerName { get; set; } = string.Empty;

    // "Custom" template only: rasterized crops of the letterhead/footer chrome from an
    // imported reference PDF (local copies under AppData, same pattern as PdfLogoPath).
    public string HeaderImagePath { get; set; } = string.Empty;
    public string FooterImagePath { get; set; } = string.Empty;
}