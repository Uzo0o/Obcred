using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Obcred.Models;
using Obcred.Services;

namespace Obcred.ViewModels;

/// <summary>
/// Backs the Received Invoices screen: invoices other companies have sent TO us,
/// pulled from UJP's purchase-invoice status endpoint and cached locally. UJP itself
/// has no concept of "paid" — that flag lives only in our local DB, toggled by hand.
/// </summary>
public partial class PurchaseInvoicesViewModel : ViewModelBase
{
    private readonly IDatabaseService _databaseService;
    private readonly IUjpService _ujpService;

    // Cache of seller TIN -> resolved company name/address, so a sync with many
    // invoices from the same sender only looks that company up once.
    private readonly Dictionary<string, CompanyDto> _sellerCache = new();

    public ObservableCollection<PurchaseInvoiceRecord> Invoices { get; } = new();

    [ObservableProperty] private PurchaseInvoiceRecord? _selectedInvoice;

    // DatePicker.SelectedDate is DateTimeOffset? in Avalonia — match it to avoid a cast error.
    [ObservableProperty] private DateTimeOffset _dateFrom = DateTimeOffset.Now.AddDays(-30);
    [ObservableProperty] private DateTimeOffset _dateTo = DateTimeOffset.Now;

    [ObservableProperty] private bool _isBusy;
    public bool IsNotBusy => !IsBusy;
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));

    [ObservableProperty] private string? _statusMessage;

    public bool HasInvoices => Invoices.Count > 0;

    public PurchaseInvoicesViewModel(IDatabaseService databaseService, IUjpService ujpService)
    {
        _databaseService = databaseService;
        _ujpService = ujpService;
        _ = LoadFromCacheAsync();
    }

    /// <summary>Fast path: shows whatever was already synced locally, no network call.</summary>
    [RelayCommand]
    public async Task LoadFromCacheAsync()
    {
        var items = await _databaseService.GetAllPurchaseInvoicesAsync();
        Invoices.Clear();
        foreach (var item in items)
            Invoices.Add(item);

        SelectedInvoice = Invoices.FirstOrDefault();
        OnPropertyChanged(nameof(HasInvoices));
    }

    /// <summary>Hits UJP for the selected date range, merges into the local cache.</summary>
    [RelayCommand]
    private async Task SyncFromUjpAsync()
    {
        if (DateTo < DateFrom)
        {
            StatusMessage = "The 'To' date can't be before the 'From' date.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Contacting UJP...";
        try
        {
            var results = await _ujpService.GetPurchaseInvoicesStatusAsync(DateFrom.Date, DateTo.Date);

            foreach (var dto in results)
            {
                // Preserve whatever paid flag we already had locally for this invoice —
                // a re-sync should never silently un-mark something as paid.
                var existing = await _databaseService.GetPurchaseInvoiceByEuidAsync(dto.Euid);

                var seller = await ResolveSellerAsync(dto.SellerTin);

                var record = new PurchaseInvoiceRecord
                {
                    Euid = dto.Euid,
                    DocNumber = dto.DocNumber,
                    DocDate = dto.DocDate,
                    DocDeliveryDate = dto.DocDeliveryDate,
                    StatusCode = dto.StatusCode,
                    StatusName = dto.StatusName,
                    NetAmount = dto.DocNetAmount,
                    VatAmount = dto.DocVatAmount,
                    GrossAmount = dto.DocGrossAmountR,
                    AvansAmount = dto.DocAvansAmount,
                    FinalAmount = dto.DocFinalAmount,
                    SellerTin = dto.SellerTin,
                    SellerName = seller?.Name ?? string.Empty,
                    SellerCity = seller?.Address?.City ?? string.Empty,
                    SellerStreet = seller?.Address?.Street ?? string.Empty,
                    SellerNumber = seller?.Address?.Number ?? string.Empty,
                    BuyerTin = dto.BuyerTin,
                    IsPaid = existing?.IsPaid ?? false,
                    LastSyncedUtc = DateTime.UtcNow
                };

                await _databaseService.SavePurchaseInvoiceAsync(record);
            }

            await LoadFromCacheAsync();
            StatusMessage = $"Synced {results.Count} invoice(s) from UJP.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Sync failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task TogglePaidAsync(PurchaseInvoiceRecord? invoice)
    {
        if (invoice is null) return;

        invoice.IsPaid = !invoice.IsPaid;
        await _databaseService.SetPurchaseInvoicePaidAsync(invoice.Euid, invoice.IsPaid);
    }

    private async Task<CompanyDto?> ResolveSellerAsync(string sellerTin)
    {
        if (string.IsNullOrWhiteSpace(sellerTin))
            return null;

        if (_sellerCache.TryGetValue(sellerTin, out var cached))
            return cached;

        try
        {
            var company = await _ujpService.GetCompanyDetailsAsync(sellerTin);
            _sellerCache[sellerTin] = company;
            return company;
        }
        catch
        {
            // A failed lookup for one seller shouldn't block the whole sync —
            // the invoice is still shown, just without a resolved company name.
            return null;
        }
    }
}