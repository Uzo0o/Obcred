using System;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Obcred.Models;
using Obcred.Services;

namespace Obcred.ViewModels;

public partial class HistoryViewModel : ViewModelBase
{
    private readonly IDatabaseService _databaseService;
    private readonly IInvoicePdfService _pdfService;

    public ObservableCollection<InvoiceRecord> Invoices { get; } = new();

    [ObservableProperty] private InvoiceRecord? _selectedInvoice;
    [ObservableProperty] private string? _statusMessage;

    // Wired by the view: prompts for a save location, returns the chosen path (or null if cancelled).
    public Func<string, Task<string?>>? SavePdfFileAction { get; set; }

    public HistoryViewModel(IDatabaseService databaseService, IInvoicePdfService pdfService)
    {
        _databaseService = databaseService;
        _pdfService = pdfService;
        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var items = await _databaseService.GetAllInvoicesAsync();
        Invoices.Clear();
        foreach (var item in items)
        {
            Invoices.Add(item);
        }

        SelectedInvoice = Invoices.Count > 0 ? Invoices[0] : null;
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (SelectedInvoice == null)
            return;

        if (string.IsNullOrWhiteSpace(SelectedInvoice.PdfModelJson))
        {
            StatusMessage = "No PDF data stored for this invoice.";
            return;
        }

        if (SavePdfFileAction == null)
            return;

        try
        {
            var model = JsonSerializer.Deserialize<InvoicePdfModel>(SelectedInvoice.PdfModelJson);
            if (model == null)
            {
                StatusMessage = "Could not read stored PDF data.";
                return;
            }

            string? path = await SavePdfFileAction($"{SelectedInvoice.DocNumber}.pdf");
            if (string.IsNullOrEmpty(path))
                return; // cancelled

            _pdfService.Save(model, path);
            StatusMessage = $"PDF saved to {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"PDF export failed: {ex.Message}";
        }
    }
}
