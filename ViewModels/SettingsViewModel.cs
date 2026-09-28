using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Obcred.Models;
using Obcred.Services;

namespace Obcred.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IUserSettingsService _settingsService;
    private readonly IUjpService _ujpService;
    private readonly IUsageService _usageService;
    
    public Action? CloseAction { get; set; }
    public Func<Task<string?>>? BrowseFileAction { get; set; } // Replaces OpenFileDialog

    [ObservableProperty] private string _certPath = string.Empty;
    [ObservableProperty] private string _certPassword = string.Empty;
    [ObservableProperty] private string _certThumbprint = string.Empty;
    [ObservableProperty] private string _eujpId = string.Empty;
    [ObservableProperty] private string _sellerEdb = string.Empty;
    [ObservableProperty] private string _invoiceNumberPrefix = string.Empty;
    [ObservableProperty] private bool _useProductionEnvironment;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public SettingsViewModel(IUserSettingsService settingsService, IUjpService ujpService, IUsageService usageService)
    {
        _settingsService = settingsService;
        _ujpService = ujpService;
        _usageService = usageService;
        
        var current = _settingsService.CurrentSettings;
        CertPath = current.CertPath ?? string.Empty;
        CertPassword = current.CertPassword ?? string.Empty;
        CertThumbprint = current.CertThumbprint ?? string.Empty;
        EujpId = current.EujpId ?? string.Empty;
        SellerEdb = current.SellerEdb ?? string.Empty;
        InvoiceNumberPrefix = current.InvoiceNumberPrefix ?? string.Empty;
        UseProductionEnvironment = current.UseProductionEnvironment;
    }

    [RelayCommand]
    private void SelectUsbCertificate()
    {
        // Disables the compiler warning since this UI component is Windows-only
#pragma warning disable CA1416 
        try
        {
            using X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);

            X509Certificate2Collection selectedCerts = X509Certificate2UI.SelectFromCollection(
                store.Certificates,
                "Изберете го вашиот сертификат",
                "Изберете го вашиот KIBS/Telekom USB сертификат од листата.",
                X509SelectionFlag.SingleSelection);

            if (selectedCerts.Count > 0)
            {
                CertThumbprint = selectedCerts[0].Thumbprint;
                CertPath = string.Empty;
                CertPassword = string.Empty;
                StatusMessage = "USB сертификатот е успешно поврзан!";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"USB грешка: {ex.Message}";
        }
#pragma warning restore CA1416
    }

    [RelayCommand]
    private async Task BrowseCertificateAsync() // Changed to async
    {
        if (BrowseFileAction != null)
        {
            var selectedFile = await BrowseFileAction();
            if (!string.IsNullOrEmpty(selectedFile))
            {
                CertPath = selectedFile;
                CertThumbprint = string.Empty; 
            }
        }
    }

    [RelayCommand]
    private async Task SaveAndVerifyAsync()
    {
        bool hasCert = !string.IsNullOrWhiteSpace(CertPath) || !string.IsNullOrWhiteSpace(CertThumbprint);
        
        if (!hasCert || string.IsNullOrWhiteSpace(SellerEdb))
        {
            StatusMessage = "Внесете сертификат и вашиот ЕДБ.";
            return;
        }

        try
        {
            StatusMessage = "Се применува сертификатот на системот...";

            // Seed from the CURRENT settings (not a blank object) so fields this screen
            // doesn't own — like the PDF template/logo chosen on the PDF Template
            // screen — survive a re-save here instead of being silently reset.
            var initialSettings = _settingsService.CurrentSettings;
            initialSettings.CertPath = CertPath;
            initialSettings.CertPassword = CertPassword;
            initialSettings.CertThumbprint = CertThumbprint;
            initialSettings.EujpId = EujpId;
            initialSettings.SellerEdb = SellerEdb;
            initialSettings.InvoiceNumberPrefix = InvoiceNumberPrefix;
            initialSettings.UseProductionEnvironment = UseProductionEnvironment;
            _settingsService.SaveSettings(initialSettings);

            StatusMessage = "Се проверува компанијата со УЈП...";

            var company = await _ujpService.GetCompanyDetailsAsync(SellerEdb);

            initialSettings.SellerName = company?.Name ?? "Непознато";
            initialSettings.SellerVatNumber = company?.VatNumber ?? ""; 
            initialSettings.SellerStreet = company?.Address?.Street ?? "";
            initialSettings.SellerNumber = company?.Address?.Number ?? "-"; 
            initialSettings.SellerCity = company?.Address?.City ?? "";
            initialSettings.SellerZip = company?.Address?.Zip ?? "1000";
            
            _settingsService.SaveSettings(initialSettings);

            // UJP just confirmed this EDB is real — this is the one and only
            // place we should ever push it to the Worker (never an unverified
            // value someone just typed). Best-effort: failure here shouldn't
            // block finishing setup.
            _ = _usageService.SyncEdbAsync(SellerEdb);

            StatusMessage = "Поставувањето е завршено!";

            CloseAction?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Проверката не успеа: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[SETUP ERROR]: {ex.Message}");
        }
    }
}