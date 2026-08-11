using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Obcred.Models;
using Obcred.Services;

namespace Obcred.ViewModels;

/// <summary>
/// Backs the PDF Template screen: pick one of the built-in layouts, optionally attach
/// a company logo, and preview exactly what a generated invoice PDF will look like —
/// all before saving, at which point "Save PDF" everywhere else in the app picks it up.
/// </summary>
public partial class PdfSettingsViewModel : ViewModelBase
{
    private readonly IUserSettingsService _settingsService;
    private readonly IInvoicePdfService _pdfService;

    public IReadOnlyList<PdfTemplateOption> Templates => PdfTemplateOption.All;

    // Wired by the view: opens a file picker restricted to images, returns the chosen path (or null).
    public Func<Task<string?>>? BrowseLogoFileAction { get; set; }

    [ObservableProperty] private string _selectedTemplateId = "Classic";
    [ObservableProperty] private string _logoPath = string.Empty;
    [ObservableProperty] private Bitmap? _logoPreview;
    [ObservableProperty] private Bitmap? _pdfPreview;
    [ObservableProperty] private string? _statusMessage;

    [ObservableProperty] private bool _isBusy;
    public bool IsNotBusy => !IsBusy;
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));

    public bool HasLogo => !string.IsNullOrWhiteSpace(LogoPath);

    public PdfSettingsViewModel(IUserSettingsService settingsService, IInvoicePdfService pdfService)
    {
        _settingsService = settingsService;
        _pdfService = pdfService;

        var current = _settingsService.CurrentSettings;
        _selectedTemplateId = string.IsNullOrWhiteSpace(current.PdfTemplate) ? "Classic" : current.PdfTemplate;
        _logoPath = current.PdfLogoPath ?? string.Empty;

        RefreshPreview();
    }

    partial void OnSelectedTemplateIdChanged(string value) => RefreshPreview();

    partial void OnLogoPathChanged(string value)
    {
        OnPropertyChanged(nameof(HasLogo));
        RefreshPreview();
    }

    [RelayCommand]
    private void SelectTemplate(string templateId) => SelectedTemplateId = templateId;

    [RelayCommand]
    private async Task BrowseLogoAsync()
    {
        if (BrowseLogoFileAction == null) return;

        var picked = await BrowseLogoFileAction();
        if (string.IsNullOrWhiteSpace(picked)) return;

        try
        {
            // Copy into our own AppData folder so the logo survives the original
            // file being moved, renamed, or deleted after being picked.
            string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string myAppFolder = Path.Combine(appDataFolder, "IntegritiEFakturi");
            Directory.CreateDirectory(myAppFolder);

            string extension = Path.GetExtension(picked);
            string destination = Path.Combine(myAppFolder, $"logo{extension}");

            File.Copy(picked, destination, overwrite: true);
            LogoPath = destination;
            StatusMessage = "Logo updated — don't forget to Save.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't use that file: {ex.Message}";
        }
    }

    [RelayCommand]
    private void RemoveLogo()
    {
        LogoPath = string.Empty;
        StatusMessage = "Logo removed — don't forget to Save.";
    }

    [RelayCommand]
    private void Save()
    {
        var settings = _settingsService.CurrentSettings;
        settings.PdfTemplate = SelectedTemplateId;
        settings.PdfLogoPath = LogoPath;
        _settingsService.SaveSettings(settings);
        StatusMessage = "Saved — new PDFs will use this template.";
    }

    private void RefreshPreview()
    {
        try
        {
            var sampleModel = BuildSampleModel();
            byte[] pngBytes = _pdfService.GeneratePreviewImage(
                sampleModel, SelectedTemplateId, string.IsNullOrWhiteSpace(LogoPath) ? null : LogoPath);

            using var ms = new MemoryStream(pngBytes);
            PdfPreview = new Bitmap(ms);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Preview failed: {ex.Message}";
            PdfPreview = null;
        }

        if (!string.IsNullOrWhiteSpace(LogoPath) && File.Exists(LogoPath))
        {
            try
            {
                using var logoStream = File.OpenRead(LogoPath);
                LogoPreview = new Bitmap(logoStream);
            }
            catch
            {
                LogoPreview = null;
            }
        }
        else
        {
            LogoPreview = null;
        }
    }

    private InvoicePdfModel BuildSampleModel()
    {
        var settings = _settingsService.CurrentSettings;
        string sellerName = string.IsNullOrWhiteSpace(settings.SellerName) ? "Your Company DOOEL" : settings.SellerName;
        string sellerEdb = string.IsNullOrWhiteSpace(settings.SellerEdb) ? "4030000000000" : settings.SellerEdb;
        string sellerAddress = string.IsNullOrWhiteSpace(settings.SellerStreet)
            ? "Sample Street 1, Skopje"
            : $"{settings.SellerStreet} {settings.SellerNumber}, {settings.SellerCity}";

        return new InvoicePdfModel
        {
            DocNumber = "2026-0001",
            DocTypeName = "Фактура",
            IssueDate = DateTime.Now.ToString("yyyy-MM-dd"),
            TurnoverDate = DateTime.Now.ToString("yyyy-MM-dd"),
            SellerName = sellerName,
            SellerEdb = sellerEdb,
            SellerVatNumber = settings.SellerVatNumber,
            SellerAddress = sellerAddress,
            BuyerName = "Sample Buyer DOOEL",
            BuyerEdb = "4030111111111",
            BuyerVatNumber = "МК4030111111111",
            BuyerAddress = "Buyer Street 5, Bitola",
            Lines = new List<InvoicePdfLine>
            {
                new() { LineNo = 1, Description = "Consulting services", Qty = 2, Unit = "h", UnitPrice = 1500m, VatLabel = "18%", LineNet = 3000m, LineVat = 540m, LineGross = 3540m },
                new() { LineNo = 2, Description = "Software license", Qty = 1, Unit = "pcs", UnitPrice = 4200m, VatLabel = "18%", LineNet = 4200m, LineVat = 756m, LineGross = 4956m }
            },
            NetAmount = 7200m,
            VatAmount = 1296m,
            GrossAmount = 8496m,
            Currency = "MKD"
        };
    }
}