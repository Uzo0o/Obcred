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
/// Backs the Client List screen: a searchable list of every client we've ever saved,
/// and — for whichever one is selected — their full contact details plus every
/// invoice we've sent them, pulled straight out of the same local invoice audit trail
/// InvoiceHistoryView reads from (matched by EDB).
/// </summary>
public partial class ClientsViewModel : ViewModelBase
{
    private readonly IDatabaseService _databaseService;

    // Unfiltered caches; Clients is what SearchQuery actually filters into.
    private List<ClientRecord> _allClients = new();
    private List<InvoiceRecord> _allInvoices = new();

    public ObservableCollection<ClientRecord> Clients { get; } = new();
    public ObservableCollection<InvoiceRecord> ClientInvoices { get; } = new();

    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private ClientRecord? _selectedClient;

    [ObservableProperty] private int _totalInvoiceCount;
    [ObservableProperty] private decimal _totalInvoicedAmount;

    [ObservableProperty] private bool _isBusy;
    public bool IsNotBusy => !IsBusy;
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));

    public bool HasClients => Clients.Count > 0;
    public bool HasClientInvoices => ClientInvoices.Count > 0;

    public ClientsViewModel(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            _allClients = await _databaseService.GetAllClientsAsync();
            _allInvoices = await _databaseService.GetAllInvoicesAsync();

            ApplyFilter();

            // Keep the current selection if it still exists after a refresh; otherwise
            // fall back to the first client in the (filtered) list.
            SelectedClient = Clients.FirstOrDefault(c => c.Edb == SelectedClient?.Edb) ?? Clients.FirstOrDefault();
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var query = SearchQuery?.Trim();
        var matches = string.IsNullOrEmpty(query)
            ? _allClients
            : _allClients.Where(c =>
                    c.Name.Contains(query, System.StringComparison.OrdinalIgnoreCase) ||
                    c.Edb.Contains(query, System.StringComparison.OrdinalIgnoreCase))
                .ToList();

        Clients.Clear();
        foreach (var client in matches)
            Clients.Add(client);
        OnPropertyChanged(nameof(HasClients));
    }

    partial void OnSelectedClientChanged(ClientRecord? value)
    {
        ClientInvoices.Clear();
        TotalInvoiceCount = 0;
        TotalInvoicedAmount = 0;

        if (value is null)
        {
            OnPropertyChanged(nameof(HasClientInvoices));
            return;
        }

        var invoicesForClient = _allInvoices
            .Where(i => i.BuyerEdb == value.Edb)
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToList();

        foreach (var invoice in invoicesForClient)
            ClientInvoices.Add(invoice);

        TotalInvoiceCount = invoicesForClient.Count;
        TotalInvoicedAmount = invoicesForClient.Sum(i => i.GrossAmount);
        OnPropertyChanged(nameof(HasClientInvoices));
    }
}