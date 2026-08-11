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
    
    // Cached UJP Data
    public string SellerName { get; set; } = string.Empty;
    public string SellerVatNumber { get; set; } = string.Empty; // NEW
    public string SellerStreet { get; set; } = string.Empty;
    public string SellerNumber { get; set; } = string.Empty; 
    public string SellerCity { get; set; } = string.Empty;
    public string SellerZip { get; set; } = string.Empty;    
}