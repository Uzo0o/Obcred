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
    private readonly ITemplateImportService _templateImportService;

    public IReadOnlyList<PdfTemplateOption> Templates => PdfTemplateOption.All;
    public IReadOnlyList<PdfAccentColor> AccentColors => PdfAccentColor.All;

    // Wired by the view: opens a file picker restricted to images, returns the chosen path (or null).
    public Func<Task<string?>>? BrowseLogoFileAction { get; set; }

    // Wired by the view: opens a file picker restricted to PDFs, returns the chosen path (or null).
    public Func<Task<string?>>? BrowseTemplatePdfFileAction { get; set; }

    [ObservableProperty] private string _selectedTemplateId = "Classic";
    [ObservableProperty] private string _selectedAccentColorId = "Blue";
    [ObservableProperty] private string _logoPath = string.Empty;
    [ObservableProperty] private Bitmap? _logoPreview;
    [ObservableProperty] private Bitmap? _pdfPreview;
    [ObservableProperty] private string? _statusMessage;

    // Invoice defaults shown/edited here and reused on every invoice — see UserSettings.cs.
    [ObservableProperty] private string _bankName = string.Empty;
    [ObservableProperty] private string _bankAccount = string.Empty;
    [ObservableProperty] private string _iban = string.Empty;
    [ObservableProperty] private string _contactPhone = string.Empty;
    [ObservableProperty] private string _contactEmail = string.Empty;
    [ObservableProperty] private string _website = string.Empty;
    [ObservableProperty] private int _defaultPaymentDueDays;
    [ObservableProperty] private string _paymentTermsNote = string.Empty;
    [ObservableProperty] private string _paymentInterestNote = string.Empty;
    [ObservableProperty] private string _invoiceFooterNote = string.Empty;
    [ObservableProperty] private string _authorizedSignerName = string.Empty;

    // "Custom" template only: rasterized letterhead/footer crops from an imported PDF.
    [ObservableProperty] private string _headerImagePath = string.Empty;
    [ObservableProperty] private string _footerImagePath = string.Empty;

    public bool HasCustomTemplateAssets =>
        !string.IsNullOrWhiteSpace(HeaderImagePath) || !string.IsNullOrWhiteSpace(FooterImagePath);

    partial void OnBankNameChanged(string value) => RefreshPreview();
    partial void OnBankAccountChanged(string value) => RefreshPreview();
    partial void OnIbanChanged(string value) => RefreshPreview();
    partial void OnContactPhoneChanged(string value) => RefreshPreview();
    partial void OnContactEmailChanged(string value) => RefreshPreview();
    partial void OnWebsiteChanged(string value) => RefreshPreview();
    partial void OnDefaultPaymentDueDaysChanged(int value) => RefreshPreview();
    partial void OnPaymentTermsNoteChanged(string value) => RefreshPreview();
    partial void OnPaymentInterestNoteChanged(string value) => RefreshPreview();
    partial void OnInvoiceFooterNoteChanged(string value) => RefreshPreview();
    partial void OnAuthorizedSignerNameChanged(string value) => RefreshPreview();
    partial void OnHeaderImagePathChanged(string value)
    {
        OnPropertyChanged(nameof(HasCustomTemplateAssets));
        RefreshPreview();
    }
    partial void OnFooterImagePathChanged(string value)
    {
        OnPropertyChanged(nameof(HasCustomTemplateAssets));
        RefreshPreview();
    }

    [ObservableProperty] private bool _isBusy;
    public bool IsNotBusy => !IsBusy;
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));

    public bool HasLogo => !string.IsNullOrWhiteSpace(LogoPath);

    public PdfSettingsViewModel(IUserSettingsService settingsService, IInvoicePdfService pdfService, ITemplateImportService templateImportService)
    {
        _settingsService = settingsService;
        _pdfService = pdfService;
        _templateImportService = templateImportService;

        var current = _settingsService.CurrentSettings;
        _selectedTemplateId = string.IsNullOrWhiteSpace(current.PdfTemplate) ? "Classic" : current.PdfTemplate;
        _selectedAccentColorId = string.IsNullOrWhiteSpace(current.PdfAccentColorId) ? "Blue" : current.PdfAccentColorId;
        _logoPath = current.PdfLogoPath ?? string.Empty;

        _bankName = current.BankName ?? string.Empty;
        _bankAccount = current.BankAccount ?? string.Empty;
        _iban = current.Iban ?? string.Empty;
        _contactPhone = current.ContactPhone ?? string.Empty;
        _contactEmail = current.ContactEmail ?? string.Empty;
        _website = current.Website ?? string.Empty;
        _defaultPaymentDueDays = current.DefaultPaymentDueDays;
        _paymentTermsNote = current.PaymentTermsNote ?? string.Empty;
        _paymentInterestNote = current.PaymentInterestNote ?? string.Empty;
        _invoiceFooterNote = current.InvoiceFooterNote ?? string.Empty;
        _authorizedSignerName = current.AuthorizedSignerName ?? string.Empty;
        _headerImagePath = current.HeaderImagePath ?? string.Empty;
        _footerImagePath = current.FooterImagePath ?? string.Empty;

        RefreshPreview();
    }

    partial void OnSelectedTemplateIdChanged(string value) => RefreshPreview();
    partial void OnSelectedAccentColorIdChanged(string value) => RefreshPreview();

    partial void OnLogoPathChanged(string value)
    {
        OnPropertyChanged(nameof(HasLogo));
        RefreshPreview();
    }

    [RelayCommand]
    private void SelectTemplate(string templateId) => SelectedTemplateId = templateId;

    [RelayCommand]
    private void SelectColor(string colorId) => SelectedAccentColorId = colorId;

    [RelayCommand]
    private async Task BrowseLogoAsync()
    {
        if (BrowseLogoFileAction == null) return;

        var picked = await BrowseLogoFileAction();
        if (string.IsNullOrWhiteSpace(picked)) return;

        try
        {
            byte[] bytes = File.ReadAllBytes(picked);
            LogoPath = SaveLogoBytes(bytes, Path.GetExtension(picked));
            StatusMessage = "Логото е ажурирано — не заборавајте да зачувате.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Не можеше да се употреби таа датотека: {ex.Message}";
        }
    }

    [RelayCommand]
    private void RemoveLogo()
    {
        LogoPath = string.Empty;
        StatusMessage = "Логото е отстрането — не заборавајте да зачувате.";
    }

    [RelayCommand]
    private void RemoveCustomTemplateAssets()
    {
        HeaderImagePath = string.Empty;
        FooterImagePath = string.Empty;
        if (SelectedTemplateId == "Custom")
            SelectedTemplateId = "Classic";
        StatusMessage = "Увезеното заглавие/подножје се отстранети — не заборавајте да зачувате.";
    }

    [RelayCommand]
    private async Task ImportTemplateAsync()
    {
        if (BrowseTemplatePdfFileAction == null) return;

        var picked = await BrowseTemplatePdfFileAction();
        if (string.IsNullOrWhiteSpace(picked)) return;

        IsBusy = true;
        try
        {
            var result = await _templateImportService.ExtractAsync(picked);

            bool logoApplied = false;
            if (result.LogoCandidates.Count > 0)
            {
                var best = result.LogoCandidates[0];
                LogoPath = SaveLogoBytes(best.Bytes, best.Extension);
                logoApplied = true;
            }

            bool chromeApplied = false;
            if (result.HeaderImageBytes != null)
            {
                HeaderImagePath = SaveImportedImage(result.HeaderImageBytes, "header-template.png");
                chromeApplied = true;
            }
            if (result.FooterImageBytes != null)
            {
                FooterImagePath = SaveImportedImage(result.FooterImageBytes, "footer-template.png");
                chromeApplied = true;
            }
            // The whole point of importing was to get this look — switch to it now so
            // "Save" captures it; the user can still pick a different layout afterward.
            if (chromeApplied)
                SelectedTemplateId = "Custom";

            // Only fill blanks — never overwrite something the user already typed themselves.
            int fieldsApplied = 0;
            fieldsApplied += ApplyIfBlank(() => BankName, v => BankName = v, result.SuggestedBankName);
            fieldsApplied += ApplyIfBlank(() => BankAccount, v => BankAccount = v, result.SuggestedBankAccount);
            fieldsApplied += ApplyIfBlank(() => Iban, v => Iban = v, result.SuggestedIban);
            fieldsApplied += ApplyIfBlank(() => ContactPhone, v => ContactPhone = v, result.SuggestedContactPhone);
            fieldsApplied += ApplyIfBlank(() => ContactEmail, v => ContactEmail = v, result.SuggestedContactEmail);
            fieldsApplied += ApplyIfBlank(() => Website, v => Website = v, result.SuggestedWebsite);
            fieldsApplied += ApplyIfBlank(() => InvoiceFooterNote, v => InvoiceFooterNote = v, result.SuggestedFooterNote);

            StatusMessage = logoApplied || chromeApplied || fieldsApplied > 0
                ? $"Увезени {fieldsApplied} полиња{(logoApplied ? ", логото" : "")}{(chromeApplied ? " и заглавие/подножје" : "")} од фактурата. Проверете ги и зачувајте."
                : "Не најдовме препознатливи податоци во оваа фактура — проверете ги полињата рачно.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Увезувањето не успеа: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static int ApplyIfBlank(Func<string> getCurrent, Action<string> setValue, string? suggested)
    {
        if (string.IsNullOrWhiteSpace(suggested) || !string.IsNullOrWhiteSpace(getCurrent()))
            return 0;

        setValue(suggested);
        return 1;
    }

    // Shared by manual logo upload and PDF template import — copies logo bytes into our
    // AppData folder so the logo survives the original file being moved/deleted, and
    // returns the stored path to assign to LogoPath.
    private static string SaveLogoBytes(byte[] bytes, string extension) =>
        SaveImportedImage(bytes, $"logo{extension}");

    private static string SaveImportedImage(byte[] bytes, string fileName)
    {
        string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string myAppFolder = Path.Combine(appDataFolder, "IntegritiEFakturi");
        Directory.CreateDirectory(myAppFolder);

        string destination = Path.Combine(myAppFolder, fileName);
        File.WriteAllBytes(destination, bytes);
        return destination;
    }

    [RelayCommand]
    private void Save()
    {
        var settings = _settingsService.CurrentSettings;
        settings.PdfTemplate = SelectedTemplateId;
        settings.PdfLogoPath = LogoPath;
        settings.PdfAccentColorId = SelectedAccentColorId;

        settings.BankName = BankName;
        settings.BankAccount = BankAccount;
        settings.Iban = Iban;
        settings.ContactPhone = ContactPhone;
        settings.ContactEmail = ContactEmail;
        settings.Website = Website;
        settings.DefaultPaymentDueDays = DefaultPaymentDueDays;
        settings.PaymentTermsNote = PaymentTermsNote;
        settings.PaymentInterestNote = PaymentInterestNote;
        settings.InvoiceFooterNote = InvoiceFooterNote;
        settings.AuthorizedSignerName = AuthorizedSignerName;
        settings.HeaderImagePath = HeaderImagePath;
        settings.FooterImagePath = FooterImagePath;

        _settingsService.SaveSettings(settings);
        StatusMessage = "Зачувано — новите PDF-и ќе го користат овој шаблон.";
    }

    private void RefreshPreview()
    {
        try
        {
            var sampleModel = BuildSampleModel();
            byte[] pngBytes = _pdfService.GeneratePreviewImage(
                sampleModel, SelectedTemplateId, string.IsNullOrWhiteSpace(LogoPath) ? null : LogoPath, SelectedAccentColorId,
                string.IsNullOrWhiteSpace(HeaderImagePath) ? null : HeaderImagePath,
                string.IsNullOrWhiteSpace(FooterImagePath) ? null : FooterImagePath);

            using var ms = new MemoryStream(pngBytes);
            PdfPreview = new Bitmap(ms);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Прегледот не успеа: {ex.Message}";
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
        string sellerName = string.IsNullOrWhiteSpace(settings.SellerName) ? "Вашата компанија ДООЕЛ" : settings.SellerName;
        string sellerEdb = string.IsNullOrWhiteSpace(settings.SellerEdb) ? "4030000000000" : settings.SellerEdb;
        string sellerAddress = string.IsNullOrWhiteSpace(settings.SellerStreet)
            ? "Примерна улица 1, Скопје"
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
            BuyerName = "Примерок купувач ДООЕЛ",
            BuyerEdb = "4030111111111",
            BuyerVatNumber = "МК4030111111111",
            BuyerAddress = "Купувачка улица 5, Битола",
            Lines = new List<InvoicePdfLine>
            {
                new() { LineNo = 1, Code = "1/ Ф 006", Description = "Консултантски услуги", Qty = 2, Unit = "ч.", UnitPrice = 1500m, VatLabel = "18%", LineNet = 3000m, LineVat = 540m, LineGross = 3540m },
                new() { LineNo = 2, Code = "2/ Ф 014", Description = "Софтверска лиценца", Qty = 1, Unit = "ком.", UnitPrice = 4200m, VatLabel = "18%", LineNet = 4200m, LineVat = 756m, LineGross = 4956m }
            },
            NetAmount = 7200m,
            VatAmount = 1296m,
            GrossAmount = 8496m,
            Currency = "MKD",

            DeliveryNoteNumber = "29/26",
            DeliveryNoteDate = DateTime.Now.ToString("yyyy-MM-dd"),
            PaymentDueDays = DefaultPaymentDueDays > 0 ? DefaultPaymentDueDays : null,
            PaymentTermsNote = PaymentTermsNote,
            PaymentInterestNote = PaymentInterestNote,
            FooterNote = InvoiceFooterNote,
            BankName = BankName,
            BankAccount = BankAccount,
            Iban = Iban,
            ContactPhone = ContactPhone,
            ContactEmail = ContactEmail,
            Website = Website,
            AuthorizedSignerName = AuthorizedSignerName
        };
    }
}