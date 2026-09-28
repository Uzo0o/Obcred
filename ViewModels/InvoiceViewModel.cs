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

public partial class InvoiceViewModel : ObservableObject
{
    private readonly IUjpService _ujpService;
    private readonly IDatabaseService _databaseService;
    private readonly IUserSettingsService _settingsService;
    private readonly IInvoicePdfService _pdfService;
    private readonly IUsageService _usageService;

    // Wired by the view: prompts for a save location, returns the chosen path (or null if cancelled).
    public Func<string, Task<string?>>? SavePdfFileAction { get; set; }

    // Wired by the view: shown when the person crosses the Free plan's limit
    // without an acknowledgment on file for this month yet.
    public Action<UsageStatus>? OverageWarningRequested { get; set; }

    // Wired by App.axaml.cs: signs the current account out and returns to the
    // login screen, so a different account's invoices/clients/plan can load.
    public Action? LogoutRequested { get; set; }

    [RelayCommand]
    private void Logout() => LogoutRequested?.Invoke();

    public IReadOnlyList<string> TaxOptions { get; } = VatRates.DisplayNames;
    public IReadOnlyList<DocumentType> DocumentTypeOptions { get; } = DocumentTypes.All;
    public ObservableCollection<ClientRecord> SearchResults { get; } = new();
    public ObservableCollection<ClientRecord> AllClients { get; } = new();
    
    [ObservableProperty] private string _searchQuery;
    [ObservableProperty] private ClientRecord _selectedClient;

    // These properties map directly to your XAML
    [ObservableProperty] private string _buyerEdb;

    [ObservableProperty] private string _buyerName;

    [ObservableProperty] private string _statusMessage; 
    
    [ObservableProperty] private string _invoiceNumber;

    // DatePicker.SelectedDate is DateTimeOffset? in Avalonia — match it to avoid a cast error.
    [ObservableProperty] private DateTimeOffset _invoiceDate = DateTimeOffset.Now;
    [ObservableProperty] private DateTimeOffset _turnoverDate = DateTimeOffset.Now;

    [ObservableProperty] private DocumentType _selectedDocumentType = DocumentTypes.Invoice;
    
    [ObservableProperty] private decimal _netAmount;
    [ObservableProperty] private decimal _vatAmount;
    [ObservableProperty] private decimal _grossAmount;

    // True while a network operation (submit/lookup) is in progress; drives button enable state + spinner.
    [ObservableProperty] private bool _isBusy;
    public bool IsNotBusy => !IsBusy;
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));

    // Plan/usage badge for the sidebar (bound in MainWindow.axaml).
    [ObservableProperty] private string _usagePlanLabel = "Бесплатен план";
    [ObservableProperty] private string _usageCountLabel = string.Empty;
    [ObservableProperty] private double _usageProgressFraction;
    [ObservableProperty] private bool _usageIsUnlimited;

    // Environment banner for the main window (bound in MainWindow.axaml).
    public bool IsProduction => _settingsService.CurrentSettings.UseProductionEnvironment;
    public bool IsTest => !IsProduction;
    public string EnvironmentBanner => IsProduction ? "● ПРОДУКЦИЈА (ВО ЖИВО)" : "● ТЕСТ РЕЖИМ";

    // Called after Settings is edited so the banner updates without a restart.
    public void RefreshEnvironment()
    {
        OnPropertyChanged(nameof(IsProduction));
        OnPropertyChanged(nameof(IsTest));
        OnPropertyChanged(nameof(EnvironmentBanner));
    }
    
    // The calendar year the currently-displayed InvoiceNumber was generated for,
    // so we commit the counter for the correct year on success.
    private int _currentSequenceYear = DateTime.Now.Year;

    // The DI container automatically hands this ViewModel the UjpService!
    public InvoiceViewModel(IUjpService ujpService, IDatabaseService databaseService,
        IUserSettingsService settingsService, IInvoicePdfService pdfService, IUsageService usageService)
    {
        _ujpService = ujpService;
        _databaseService = databaseService;
        _settingsService = settingsService;
        _pdfService = pdfService;
        _usageService = usageService;

        // Deliberately NOT loaded here: this ViewModel is a long-lived singleton
        // constructed the first time it's needed, which could be before we know
        // which account is signed in. App.axaml.cs calls RefreshForUserAsync
        // explicitly, once right after every login — first-ever and any
        // logout -> different-account login within the same run.
    }

    /// <summary>
    /// Loads this signed-in account's clients, next invoice number, and plan/usage
    /// badge. Safe to call again after switching accounts.
    /// </summary>
    public async Task RefreshForUserAsync()
    {
        // Clear whatever draft the previous account was mid-typing — it belongs
        // to their session, not the one now signed in.
        InvoiceItems.Clear();
        SearchResults.Clear();
        SearchQuery = string.Empty;
        SelectedClient = null;
        BuyerEdb = string.Empty;
        BuyerName = string.Empty;
        StatusMessage = string.Empty;

        await LoadAllClientsAsync();
        await GenerateNextInvoiceNumberAsync();
        await RefreshUsageAsync();
    }

    // Pulls the current month's plan/usage snapshot for the sidebar badge.
    // Best-effort: if the Worker is unreachable this just leaves the last
    // known badge state in place rather than failing anything. Public so the
    // plan picker can force an immediate badge refresh after a switch.
    public async Task RefreshUsageAsync()
    {
        var status = await _usageService.GetStatusAsync();
        if (status is null)
            return;

        UsagePlanLabel = (char.ToUpper(status.Plan[0]) + status.Plan[1..]) + " план";
        UsageIsUnlimited = status.Limit is null;
        UsageCountLabel = UsageIsUnlimited
            ? $"{status.Used} испратени овој месец"
            : $"{status.Used}/{status.Limit} овој месец";
        UsageProgressFraction = (!UsageIsUnlimited && status.Limit > 0)
            ? Math.Clamp((double)status.Used / status.Limit!.Value, 0, 1)
            : 0;

        // Every capped plan gets this notice the moment it's crossed — not just
        // Free. Business has no limit (Limit is null) so it never qualifies.
        bool overLimit = status.Limit is > 0 && status.Used > status.Limit.Value;
        if (overLimit && !_usageService.HasAcknowledgedOverageThisMonth())
        {
            OverageWarningRequested?.Invoke(status);
        }
    }

    // Peeks the gap-free counter and shows the next number (without consuming it).
    private async Task GenerateNextInvoiceNumberAsync()
    {
        _currentSequenceYear = DateTime.Now.Year;
        int seq = await _databaseService.PeekNextInvoiceSeqAsync(_currentSequenceYear);
        InvoiceNumber = FormatInvoiceNumber(_currentSequenceYear, seq);
    }

    private string FormatInvoiceNumber(int year, int seq)
    {
        string prefix = _settingsService.CurrentSettings.InvoiceNumberPrefix ?? string.Empty;
        return $"{prefix}{year}-{seq:D4}";
    }

    [RelayCommand]
    public async Task LoadAllClients()
    {
        var clients = await _databaseService.GetAllClientsAsync();
        AllClients.Clear();
        foreach(var c in clients) AllClients.Add(c);
    }
    private async Task LoadAllClientsAsync()
    {
        var clients = await _databaseService.GetAllClientsAsync();
        AllClients.Clear();
        foreach (var c in clients)
        {
            AllClients.Add(c);
        }
    }
    [RelayCommand]
    private async Task SearchLocalClientsAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            SearchResults.Clear();
            return;
        }

        var results = await _databaseService.SearchClientsByNameAsync(SearchQuery);
        
        SearchResults.Clear();
        foreach (var client in results)
        {
            SearchResults.Add(client);
        }
    }

    // 2. The Auto-Fill Magic
    // CommunityToolkit automatically calls this method when _selectedClient changes!
    partial void OnSelectedClientChanged(ClientRecord value)
    {
        if (value != null)
        {
            BuyerEdb = value.Edb;
            BuyerName = value.Name;
            StatusMessage = "Вчитано од локалниот адресар.";
        }
    }

    // 3. Update your EXISTING LookupCompanyAsync to SAVE to the database
    [RelayCommand]
    private async Task LookupCompanyAsync()
    {
        if (string.IsNullOrWhiteSpace(BuyerEdb)) return;

        try
        {
            StatusMessage = "Се бара компанијата во базата на УЈП...";
            var company = await _ujpService.GetCompanyDetailsAsync(BuyerEdb);

            if (company != null)
            {
                BuyerName = company.Name;
                StatusMessage = "Компанијата е пронајдена и зачувана во адресарот!";

                // SILENTLY SAVE TO SQLITE!
                await _databaseService.SaveClientAsync(new ClientRecord 
                {
                    Edb = BuyerEdb,
                    VatNumber = company.VatNumber ?? "", // SAVED
                    Name = company.Name,
                    Street = company.Address?.Street ?? "",
                    Number = company.Address?.Number ?? "-", 
                    City = company.Address?.City ?? "",
                    Zip = company.Address?.Zip ?? "1000",
                    CountryCode = company.CountryCode ?? "MK"
                });
                await  LoadAllClientsAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Грешка: {ex.Message}";
        }
    }

    public ObservableCollection<DocItem> InvoiceItems { get; } = new();

    [RelayCommand]
    private void AddBlankItem()
    {
        var newItem = new DocItem 
        { 
            LineNo = InvoiceItems.Count + 1,
            Desc = "Нова ставка",
            Qty = 1.0m,
            UnitPrice = 0.0m
        };

        // THE FIX: Listen to the Qty and UnitPrice so the ViewModel actually does the math!
        newItem.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(DocItem.TaxIndicator) ||
                args.PropertyName == nameof(DocItem.UnitPrice) ||
                args.PropertyName == nameof(DocItem.Qty))
            {
                RecalculateTotals();
            }
        };

        InvoiceItems.Add(newItem);
        RenumberItems();
        RecalculateTotals();
    }

    [RelayCommand]
    private void RemoveItem(DocItem? item)
    {
        if (item == null)
            return;

        InvoiceItems.Remove(item);
        RenumberItems();
        RecalculateTotals();
    }

    private void RenumberItems()
    {
        for (int i = 0; i < InvoiceItems.Count; i++)
            InvoiceItems[i].LineNo = i + 1;
    }


    private void RecalculateTotals()
    {
        decimal currentNet = 0;
        decimal currentVat = 0;
        
        foreach (var item in InvoiceItems)
        {
            currentNet += item.RowNetTotal;
            currentVat += item.RowVatAmount;
        }

        NetAmount = currentNet;
        VatAmount = currentVat;
        GrossAmount = NetAmount + VatAmount;
    }

    // Basic pre-submit checks. Returns a user-friendly message, or null if the invoice is valid.
    private string? ValidateInvoice()
    {
        if (InvoiceItems.Count == 0)
            return "Додадете барем една ставка пред да поднесете.";

        if (string.IsNullOrWhiteSpace(BuyerEdb))
            return "Прво изберете или внесете купувач.";

        string edbDigits = new string(BuyerEdb.Where(char.IsDigit).ToArray());
        if (edbDigits.Length != 13)
            return "ЕДБ на купувачот (даночен број) мора да содржи 13 цифри.";

        foreach (var item in InvoiceItems)
        {
            if (string.IsNullOrWhiteSpace(item.Desc))
                return $"Ред {item.LineNo}: описот е задолжителен.";
            if (item.Qty <= 0)
                return $"Ред {item.LineNo}: количината мора да биде поголема од 0.";
            if (item.UnitPrice < 0)
                return $"Ред {item.LineNo}: единечната цена не може да биде негативна.";
        }

        RecalculateTotals();
        if (GrossAmount <= 0)
            return "Вкупниот износ на фактурата мора да биде поголем од 0.";

        return null;
    }

    private static string FormatAddress(string? street, string? number, string? zip, string? city)
    {
        var lines = new List<string>();
        string line1 = string.Join(" ", new[] { street, number }.Where(s => !string.IsNullOrWhiteSpace(s)));
        string line2 = string.Join(" ", new[] { zip, city }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (!string.IsNullOrWhiteSpace(line1)) lines.Add(line1);
        if (!string.IsNullOrWhiteSpace(line2)) lines.Add(line2);
        return string.Join(", ", lines);
    }

    private InvoicePdfModel BuildPdfModel(ClientRecord? buyer)
    {
        var seller = _settingsService.CurrentSettings;
        RecalculateTotals();

        return new InvoicePdfModel
        {
            DocNumber = InvoiceNumber,
            DocTypeName = SelectedDocumentType.Name,
            IssueDate = InvoiceDate.ToString("yyyy-MM-dd"),
            TurnoverDate = TurnoverDate.ToString("yyyy-MM-dd"),

            SellerName = seller.SellerName,
            SellerEdb = seller.SellerEdb,
            SellerVatNumber = seller.SellerVatNumber,
            SellerAddress = FormatAddress(seller.SellerStreet, seller.SellerNumber, seller.SellerZip, seller.SellerCity),

            BuyerName = buyer?.Name ?? BuyerName ?? string.Empty,
            BuyerEdb = buyer?.Edb ?? BuyerEdb ?? string.Empty,
            BuyerVatNumber = buyer?.VatNumber ?? string.Empty,
            BuyerAddress = buyer != null ? FormatAddress(buyer.Street, buyer.Number, buyer.Zip, buyer.City) : string.Empty,

            Lines = InvoiceItems.Select(i => new InvoicePdfLine
            {
                LineNo = i.LineNo,
                Description = i.Desc,
                Qty = i.Qty,
                Unit = "ком.",
                UnitPrice = i.UnitPrice,
                VatLabel = i.TaxIndicator,
                LineNet = Math.Round(i.RowNetTotal, 2),
                LineVat = Math.Round(i.RowVatAmount, 2),
                LineGross = Math.Round(i.RowGrossTotal, 2)
            }).ToList(),

            NetAmount = Math.Round(NetAmount, 2),
            VatAmount = Math.Round(VatAmount, 2),
            GrossAmount = Math.Round(GrossAmount, 2),
            Currency = "MKD"
        };
    }

    [RelayCommand]
    private async Task SavePdfAsync()
    {
        if (InvoiceItems.Count == 0)
        {
            StatusMessage = "Додадете барем една ставка пред да извезете PDF.";
            return;
        }
        if (SavePdfFileAction == null)
        {
            StatusMessage = "Извезувањето во PDF не е достапно во овој контекст.";
            return;
        }

        try
        {
            var buyer = await _databaseService.GetClientByEdbAsync(BuyerEdb ?? string.Empty);
            var model = BuildPdfModel(buyer);

            string? path = await SavePdfFileAction($"{InvoiceNumber}.pdf");
            if (string.IsNullOrEmpty(path)) return; // user cancelled

            _pdfService.Save(model, path);
            StatusMessage = $"PDF зачуван во {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Извезувањето во PDF не успеа: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SubmitInvoiceAsync()
    {
        string? validationError = ValidateInvoice();
        if (validationError != null)
        {
            StatusMessage = validationError;
            return;
        }

        IsBusy = true;
        try
        {
            StatusMessage = "Се подготвува испраќањето...";
            var buyerRecord = await _databaseService.GetClientByEdbAsync(BuyerEdb);

            if (buyerRecord == null)
            {
                StatusMessage = "Адресата на купувачот не е пронајдена. Прво кликнете „Побарај во УЈП“ за да ги зачувате неговите податоци.";
                return;
            }

            string officialTimestamp = await _ujpService.GetServerTimestampAsync();
            var seller = _settingsService.CurrentSettings;

            // Build the VAT Totals block by grouping items by their (centralized) VAT rate.
            var calculatedVatTotals = InvoiceItems
                .GroupBy(i => VatRates.FromDisplayName(i.TaxIndicator))
                .Select(group => new
                {
                    vatTaxIndicator = group.Key.ApiCode,
                    vatCode = group.Key.ApiCode,
                    vatPercent = group.Key.Percent,
                    vatTaxableAmount = Math.Round(group.Sum(i => i.RowNetTotal), 2),
                    vatAmount = Math.Round(group.Sum(i => i.RowVatAmount), 2),
                    vatTotalAmount = Math.Round(group.Sum(i => i.RowGrossTotal), 2)
                }).ToArray();
            
            RecalculateTotals();

            // Generate the UJP document id up front so we can both send it and store it.
            string docId = Guid.NewGuid().ToString("N").Substring(0, 10);

            // Per the UJP wiki's documented formula, docGrossAmountR is the gross amount
            // rounded to a whole denar, and docFinalAmount = docGrossAmountR - docAvansAmount
            // — both always whole numbers in every UJP example. We don't support advance
            // payments yet, so docAvansAmount is 0 and docFinalAmount == docGrossAmountR.
            int docAvansAmount = 0;
            int docGrossAmountR = (int)Math.Round(GrossAmount, MidpointRounding.AwayFromZero);
            int docFinalAmount = docGrossAmountR - docAvansAmount;

            // 3. Build the Payload using the translated codes
            var payload = new
            {
                requestTimestamp = officialTimestamp,
                document = new
                {
                    header = new
                    {
                        docStorno = 0,
                        docType = SelectedDocumentType.Code,
                        docTypeName = SelectedDocumentType.Name,
                        docDate = InvoiceDate.ToString("yyyy-MM-dd"),
                        docTurnoverDate = TurnoverDate.ToString("yyyy-MM-dd"),
                        docNumber = InvoiceNumber,
                        docId = docId
                    },
                    seller = new
                    {
                        sellerCCode = "MK",
                        sellerCName = "Северна Македонија",
                        sellerTin = seller.SellerEdb,
                        
                        sellerVatNumber = seller.SellerVatNumber, // INJECTED
                        
                        sellerName = seller.SellerName,
                        sellerAddress = new { streetAddress = seller.SellerStreet, streetNumber = seller.SellerNumber, postalCode = seller.SellerZip, city = seller.SellerCity }
                    },
                    buyer = new
                    {
                        buyerCCode = "MK",
                        buyerCName = "Северна Македонија",
                        buyerTin = buyerRecord.Edb,
                        
                        buyerVatNumber = buyerRecord.VatNumber, // INJECTED
                        
                        buyerName = buyerRecord.Name,
                        buyerAddress = new 
                        { 
                            streetAddress = string.IsNullOrWhiteSpace(buyerRecord.Street) ? "Б.Б." : buyerRecord.Street, 
                            streetNumber = string.IsNullOrWhiteSpace(buyerRecord.Number) ? "-" : buyerRecord.Number, 
                            postalCode = string.IsNullOrWhiteSpace(buyerRecord.Zip) ? "1000" : buyerRecord.Zip, 
                            city = buyerRecord.City 
                        }
                    },
                    docPayment = new
                    {
                        docPaymentTypeCode = "P11",
                        docPaymentTypeDesc = "Плаќање со картичка",
                        docCurrency = "MKD",
                        docCurrencyCode = "MKD",
                        docCurrencyDate = InvoiceDate.ToString("yyyy-MM-dd"),
                        docCurrencyExchRate = 1
                    },
                    docItems = InvoiceItems.Select(item =>
                    {
                        var rate = VatRates.FromDisplayName(item.TaxIndicator);
                        return new
                        {
                            docItemLineNo = item.LineNo,
                            docItemSku = "SKU-" + item.LineNo,
                            docItemDesc = item.Desc,
                            docItemMUnit = "pcs",
                            docItemQty = Math.Round(item.Qty, 3),

                            docItemUnitOriginalPriceWoVat = Math.Round(item.UnitPrice, 2),
                            docItemUnitDiscountAmount = 0m,
                            docItemUnitPriceWoVat = Math.Round(item.UnitPrice, 2),

                            // docItemUnitVat = VAT AMOUNT (currency) for ONE unit = unitPriceWoVat * rate.
                            // docItemVat = the VAT rate as a percentage (18.0, 10.0, 5.0, 0.0).
                            // These are NOT the same value — see UJP sample payloads (e.g. unitVat 4.7619 vs vat 5).
                            // Derived from the SAME rounded unit price we actually send, so UJP's own
                            // recheck (roundedPrice * rate) lands inside the allowed threshold.
                            docItemUnitVat = Math.Round(Math.Round(item.UnitPrice, 2) * rate.Fraction, 4),
                            docItemVat = rate.Percent,

                            docItemVatGroup = rate.ApiCode,

                            docItemTotalOriginalPriceWoVat = Math.Round(item.RowNetTotal, 2),
                            docItemTotalPriceWoVat = Math.Round(item.RowNetTotal, 2),

                            // Rounded to 2 decimals to avoid UJP "error greater than threshold".
                            docItemTotalVat = Math.Round(item.RowVatAmount, 2),
                            docItemTotalPriceWVat = Math.Round(item.RowGrossTotal, 2),

                            docItemTaxIndicator = rate.ApiCode,
                            docItemDomesticProduct = (string?)null
                        };
                    }).ToArray(),
                    docTotals = new
                    {
                        docNetAmount = Math.Round(NetAmount, 2),
                        docDiscountAmount = 0,
                        docNetAmountDisc = Math.Round(NetAmount, 2),
                        docVatAmount = Math.Round(VatAmount, 2),
                        docGrossAmount = Math.Round(GrossAmount, 2),
                        // Must be a whole number (denar) — see docGrossAmountR/docFinalAmount above.
                        docGrossAmountR = docGrossAmountR,
                        docAvansAmount = docAvansAmount,
                        docFinalAmount = docFinalAmount
                    },
                    // Plug in the dynamic calculation we built at the top!
                    vatTotals = calculatedVatTotals
                }
            };
            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),
                WriteIndented = true
            };
            string payloadJson = System.Text.Json.JsonSerializer.Serialize(payload, jsonOptions);

            StatusMessage = "Се потпишува и испраќа до УЈП...";

            // Submit. The service returns a populated result whether UJP accepts or rejects.
            var result = await _ujpService.SubmitInvoiceAsync(payload);

            // Persist the invoice to the local audit trail regardless of outcome.
            await _databaseService.SaveInvoiceAsync(new InvoiceRecord
            {
                DocNumber = InvoiceNumber,
                DocType = SelectedDocumentType.Code,
                DocId = docId,
                BuyerEdb = buyerRecord.Edb,
                BuyerName = buyerRecord.Name,
                IssueDate = InvoiceDate.ToString("yyyy-MM-dd"),
                CreatedAtUtc = DateTime.UtcNow,
                NetAmount = Math.Round(NetAmount, 2),
                VatAmount = Math.Round(VatAmount, 2),
                GrossAmount = Math.Round(GrossAmount, 2),
                Currency = "MKD",
                Status = result.Success ? "Испратено" : "Неуспешно",
                HttpStatusCode = result.StatusCode,
                PayloadJson = payloadJson,
                SignedJws = result.SignedJws,
                UjpResponse = result.ResponseBody,
                PdfModelJson = System.Text.Json.JsonSerializer.Serialize(BuildPdfModel(buyerRecord), jsonOptions)
            });

            if (result.Success)
            {
                // Only a successful issuance consumes a number — keeps the sequence gap-free.
                await _databaseService.CommitInvoiceSeqAsync(_currentSequenceYear);
                await GenerateNextInvoiceNumberAsync();

                // Clear the line items so the next invoice starts fresh (buyer is kept for convenience).
                InvoiceItems.Clear();
                RecalculateTotals();

                // Best-effort usage reporting — never lets a network hiccup here
                // block or fail the invoice that was already successfully sent.
                await _usageService.IncrementAsync();
                await RefreshUsageAsync();
            }

            StatusMessage = result.Success
                ? $"УСПЕШНО! Фактурата е регистрирана и зачувана во историјата. УЈП: {result.ResponseBody}"
                : $"ОДБИЕНА од УЈП ({result.StatusCode}). Зачувана во историјата за преглед. {result.ResponseBody}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"ИСПРАЌАЊЕТО НЕ УСПЕА: {ex.Message}";

            // Even on a network/certificate failure, keep a record so work is never lost.
            try
            {
                await _databaseService.SaveInvoiceAsync(new InvoiceRecord
                {
                    DocNumber = InvoiceNumber,
                    DocType = SelectedDocumentType.Code,
                    BuyerEdb = BuyerEdb ?? string.Empty,
                    BuyerName = BuyerName ?? string.Empty,
                    IssueDate = InvoiceDate.ToString("yyyy-MM-dd"),
                    CreatedAtUtc = DateTime.UtcNow,
                    NetAmount = Math.Round(NetAmount, 2),
                    VatAmount = Math.Round(VatAmount, 2),
                    GrossAmount = Math.Round(GrossAmount, 2),
                    Currency = "MKD",
                    Status = "Неуспешно",
                    UjpResponse = ex.Message
                });
            }
            catch
            {
                // Ignore secondary persistence errors; the primary error is already shown.
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}