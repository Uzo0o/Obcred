using System.Collections.Generic;
using System.Threading.Tasks;
using Obcred.Models;

namespace Obcred.Services;

public interface IDatabaseService
{
    Task SaveClientAsync(ClientRecord client);
    Task<List<ClientRecord>> SearchClientsByNameAsync(string searchQuery);
    Task<ClientRecord> GetClientByEdbAsync(string edb);

    Task<List<ClientRecord>> GetAllClientsAsync();

    // Invoice audit trail
    Task SaveInvoiceAsync(InvoiceRecord invoice);
    Task<List<InvoiceRecord>> GetAllInvoicesAsync();
    Task<InvoiceRecord> GetInvoiceByIdAsync(int id);

    // Incoming ("влезни") invoices — cached UJP data plus a local paid/unpaid flag
    Task SavePurchaseInvoiceAsync(PurchaseInvoiceRecord invoice);
    Task<List<PurchaseInvoiceRecord>> GetAllPurchaseInvoicesAsync();
    Task<PurchaseInvoiceRecord?> GetPurchaseInvoiceByEuidAsync(string euid);
    Task SetPurchaseInvoicePaidAsync(string euid, bool isPaid);

    // Gap-free sequential numbering (per year)
    Task<int> PeekNextInvoiceSeqAsync(int year);
    Task CommitInvoiceSeqAsync(int year);
}