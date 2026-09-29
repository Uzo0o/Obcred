using System.Collections.Generic;
using Obcred.Models;

namespace Obcred.Services;

public interface IInvoicePdfService
{
    /// <summary>Renders the invoice to PDF bytes, using the user's currently saved
    /// template + logo + accent color preference.</summary>
    byte[] Generate(InvoicePdfModel model);

    /// <summary>Renders the invoice and writes it to the given file path, using the
    /// user's currently saved template + logo + accent color preference.</summary>
    void Save(InvoicePdfModel model, string filePath);

    /// <summary>Renders with an explicit template/logo/accent color, bypassing saved
    /// settings — used by the PDF Template screen to preview a choice before it's saved.
    /// headerImagePath/footerImagePath only matter for the "Custom" template.</summary>
    byte[] Generate(InvoicePdfModel model, string templateId, string? logoPath, string? accentColorId = null,
        string? headerImagePath = null, string? footerImagePath = null);

    /// <summary>Renders just the first page as a PNG, for a live on-screen preview.</summary>
    byte[] GeneratePreviewImage(InvoicePdfModel model, string templateId, string? logoPath, string? accentColorId = null,
        string? headerImagePath = null, string? footerImagePath = null);
}
