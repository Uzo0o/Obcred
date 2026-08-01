using Obcred.Models;

namespace Obcred.Services;

public interface IInvoicePdfService
{
    /// <summary>Renders the invoice to PDF bytes.</summary>
    byte[] Generate(InvoicePdfModel model);

    /// <summary>Renders the invoice and writes it to the given file path.</summary>
    void Save(InvoicePdfModel model, string filePath);
}
