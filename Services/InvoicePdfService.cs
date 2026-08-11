using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Obcred.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Obcred.Services;

public class InvoicePdfService : IInvoicePdfService
{
    static InvoicePdfService()
    {
        // QuestPDF Community license (free for organizations under the revenue threshold).
        QuestPDF.Settings.License = LicenseType.Community;
    }

    // "Arial" is chosen because it ships on Windows with full Cyrillic coverage,
    // so Macedonian names/addresses render correctly.
    private const string FontFamily = "Arial";

    private readonly IUserSettingsService _settingsService;

    public InvoicePdfService(IUserSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public byte[] Generate(InvoicePdfModel model)
    {
        var settings = _settingsService.CurrentSettings;
        return Generate(model, settings.PdfTemplate, settings.PdfLogoPath);
    }

    public void Save(InvoicePdfModel model, string filePath)
    {
        File.WriteAllBytes(filePath, Generate(model));
    }

    public byte[] Generate(InvoicePdfModel model, string templateId, string? logoPath)
    {
        return BuildDocument(model, templateId, TryLoadLogo(logoPath)).GeneratePdf();
    }

    public byte[] GeneratePreviewImage(InvoicePdfModel model, string templateId, string? logoPath)
    {
        return BuildDocument(model, templateId, TryLoadLogo(logoPath))
            .GenerateImages()
            .First();
    }

    private static byte[]? TryLoadLogo(string? logoPath)
    {
        if (string.IsNullOrWhiteSpace(logoPath) || !File.Exists(logoPath))
            return null;

        try
        {
            return File.ReadAllBytes(logoPath);
        }
        catch
        {
            // A broken/unreadable logo file shouldn't stop the PDF from generating.
            return null;
        }
    }

    private static IDocument BuildDocument(InvoicePdfModel model, string templateId, byte[]? logo)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(t => t.FontSize(10).FontFamily(FontFamily).FontColor("#1E293B"));

                switch (templateId)
                {
                    case "Modern":
                        ComposeModern(page, model, logo);
                        break;
                    case "Minimal":
                        ComposeMinimal(page, model, logo);
                        break;
                    default:
                        ComposeClassic(page, model, logo);
                        break;
                }
            });
        });
    }

    // ======================================================================
    // CLASSIC — bordered seller/buyer boxes, ruled table, navy accents
    // ======================================================================
    private static void ComposeClassic(PageDescriptor page, InvoicePdfModel model, byte[]? logo)
    {
        page.Margin(36);

        page.Header().Column(col =>
        {
            col.Item().Row(row =>
            {
                if (logo != null)
                    row.ConstantItem(56).Height(56).Image(logo).FitArea();

                row.RelativeItem().PaddingLeft(logo != null ? 12 : 0).Column(c =>
                {
                    c.Item().Text(model.DocTypeName).FontSize(20).Bold().FontColor("#1B3A6B");
                    c.Item().Text($"Бр. / No.: {model.DocNumber}").FontSize(11);
                });

                row.ConstantItem(200).Column(c =>
                {
                    c.Item().AlignRight().Text($"Датум / Date: {model.IssueDate}");
                    c.Item().AlignRight().Text($"Промет / Turnover: {model.TurnoverDate}");
                });
            });

            col.Item().PaddingTop(8).LineHorizontal(1).LineColor("#E2E8F0");
        });

        page.Content().PaddingVertical(10).Column(col =>
        {
            col.Item().PaddingBottom(12).Row(row =>
            {
                row.RelativeItem().Element(c => ClassicParty(c, "Издавач / Seller",
                    model.SellerName, model.SellerEdb, model.SellerVatNumber, model.SellerAddress));
                row.ConstantItem(20);
                row.RelativeItem().Element(c => ClassicParty(c, "Купувач / Buyer",
                    model.BuyerName, model.BuyerEdb, model.BuyerVatNumber, model.BuyerAddress));
            });

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(22);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn();
                    columns.RelativeColumn(1.4f);
                    columns.RelativeColumn();
                    columns.RelativeColumn(1.4f);
                    columns.RelativeColumn(1.4f);
                });

                table.Header(header =>
                {
                    ClassicHeaderCell(header, "#");
                    ClassicHeaderCell(header, "Опис / Description");
                    ClassicHeaderCell(header, "Кол.");
                    ClassicHeaderCell(header, "Цена");
                    ClassicHeaderCell(header, "ДДВ");
                    ClassicHeaderCell(header, "Основа");
                    ClassicHeaderCell(header, "Вкупно");
                });

                foreach (var line in model.Lines)
                {
                    ClassicBodyCell(table).Text(line.LineNo.ToString());
                    ClassicBodyCell(table).Text(line.Description);
                    ClassicBodyCell(table).AlignRight().Text(Num(line.Qty));
                    ClassicBodyCell(table).AlignRight().Text(Num(line.UnitPrice));
                    ClassicBodyCell(table).AlignRight().Text(line.VatLabel);
                    ClassicBodyCell(table).AlignRight().Text(Num(line.LineNet));
                    ClassicBodyCell(table).AlignRight().Text(Num(line.LineGross));
                }
            });

            col.Item().PaddingTop(12).AlignRight().Column(c =>
            {
                TotalsRow(c, "Основа / Subtotal:", Num(model.NetAmount), model.Currency, false);
                TotalsRow(c, "ДДВ / VAT:", Num(model.VatAmount), model.Currency, false);
                TotalsRow(c, "Вкупно / Total:", Num(model.GrossAmount), model.Currency, true);
            });
        });

        page.Footer().AlignCenter().Text(t =>
        {
            t.Span("Страна ");
            t.CurrentPageNumber();
            t.Span(" / ");
            t.TotalPages();
        });
    }

    private static void ClassicParty(IContainer container, string title,
        string name, string edb, string vat, string address)
    {
        container.Border(1).BorderColor("#E2E8F0").Padding(10).Column(c =>
        {
            c.Item().Text(title).FontSize(9).Bold().FontColor("#64748B");
            c.Item().PaddingTop(2).Text(name).Bold();
            if (!string.IsNullOrWhiteSpace(address))
                c.Item().Text(address);
            c.Item().Text($"ЕДБ / EDB: {edb}");
            if (!string.IsNullOrWhiteSpace(vat))
                c.Item().Text($"ДДВ бр. / VAT: {vat}");
        });
    }

    private static void ClassicHeaderCell(TableCellDescriptor header, string text)
    {
        header.Cell().Background("#F1F5F9").Padding(5)
            .Text(text).FontSize(9).Bold().FontColor("#64748B");
    }

    private static IContainer ClassicBodyCell(TableDescriptor table)
    {
        return table.Cell().BorderBottom(1).BorderColor("#F1F5F9").Padding(5);
    }

    // ======================================================================
    // MODERN — bold accent header band, logo in-band, shaded table rows
    // ======================================================================
    private static void ComposeModern(PageDescriptor page, InvoicePdfModel model, byte[]? logo)
    {
        page.Margin(0);

        page.Header().Background("#1B3A6B").Padding(28).Row(row =>
        {
            if (logo != null)
            {
                row.ConstantItem(50).Height(50).Background(Colors.White).Padding(6).Image(logo).FitArea();
                row.ConstantItem(14);
            }

            row.RelativeItem().Column(c =>
            {
                c.Item().Text(model.DocTypeName).FontSize(22).Bold().FontColor(Colors.White);
                c.Item().Text($"No. {model.DocNumber}").FontSize(11).FontColor("#B9CCE8");
            });

            row.ConstantItem(190).Column(c =>
            {
                c.Item().AlignRight().Text($"Date: {model.IssueDate}").FontColor(Colors.White);
                c.Item().AlignRight().Text($"Turnover: {model.TurnoverDate}").FontColor("#B9CCE8");
            });
        });

        page.Content().Padding(28).Column(col =>
        {
            col.Item().PaddingBottom(16).Row(row =>
            {
                row.RelativeItem().Element(c => ModernParty(c, "SELLER",
                    model.SellerName, model.SellerEdb, model.SellerVatNumber, model.SellerAddress));
                row.ConstantItem(20);
                row.RelativeItem().Element(c => ModernParty(c, "BUYER",
                    model.BuyerName, model.BuyerEdb, model.BuyerVatNumber, model.BuyerAddress));
            });

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(22);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn();
                    columns.RelativeColumn(1.4f);
                    columns.RelativeColumn();
                    columns.RelativeColumn(1.4f);
                    columns.RelativeColumn(1.4f);
                });

                table.Header(header =>
                {
                    ModernHeaderCell(header, "#");
                    ModernHeaderCell(header, "Description");
                    ModernHeaderCell(header, "Qty");
                    ModernHeaderCell(header, "Price");
                    ModernHeaderCell(header, "VAT");
                    ModernHeaderCell(header, "Net");
                    ModernHeaderCell(header, "Total");
                });

                for (int i = 0; i < model.Lines.Count; i++)
                {
                    var line = model.Lines[i];
                    string bg = i % 2 == 0 ? Colors.White : "#F4F7FC";

                    ModernBodyCell(table, bg).Text(line.LineNo.ToString());
                    ModernBodyCell(table, bg).Text(line.Description);
                    ModernBodyCell(table, bg).AlignRight().Text(Num(line.Qty));
                    ModernBodyCell(table, bg).AlignRight().Text(Num(line.UnitPrice));
                    ModernBodyCell(table, bg).AlignRight().Text(line.VatLabel);
                    ModernBodyCell(table, bg).AlignRight().Text(Num(line.LineNet));
                    ModernBodyCell(table, bg).AlignRight().Text(Num(line.LineGross));
                }
            });

            col.Item().PaddingTop(16).AlignRight().Width(240).Background("#F4F7FC").Padding(14).Column(c =>
            {
                TotalsRow(c, "Subtotal:", Num(model.NetAmount), model.Currency, false);
                TotalsRow(c, "VAT:", Num(model.VatAmount), model.Currency, false);
                c.Item().PaddingTop(4).LineHorizontal(1).LineColor("#E2E8F0");
                c.Item().PaddingTop(4).Row(r =>
                {
                    r.RelativeItem().Text("Total:").Bold().FontSize(12);
                    r.ConstantItem(110).AlignRight().Text($"{Num(model.GrossAmount)} {model.Currency}")
                        .Bold().FontSize(13).FontColor("#2E86FF");
                });
            });
        });

        page.Footer().PaddingBottom(20).AlignCenter().Text(t =>
        {
            t.DefaultTextStyle(s => s.FontColor("#94A3B8").FontSize(9));
            t.Span("Page ");
            t.CurrentPageNumber();
            t.Span(" / ");
            t.TotalPages();
        });
    }

    private static void ModernParty(IContainer container, string title,
        string name, string edb, string vat, string address)
    {
        container.Background("#F4F7FC").Padding(12).Column(c =>
        {
            c.Item().Text(title).FontSize(9).Bold().FontColor("#2E86FF").LetterSpacing(0.05f);
            c.Item().PaddingTop(3).Text(name).Bold();
            if (!string.IsNullOrWhiteSpace(address))
                c.Item().Text(address);
            c.Item().Text($"EDB: {edb}");
            if (!string.IsNullOrWhiteSpace(vat))
                c.Item().Text($"VAT: {vat}");
        });
    }

    private static void ModernHeaderCell(TableCellDescriptor header, string text)
    {
        header.Cell().Background("#1B3A6B").Padding(6)
            .Text(text).FontSize(9).Bold().FontColor(Colors.White);
    }

    private static IContainer ModernBodyCell(TableDescriptor table, string background)
    {
        return table.Cell().Background(background).Padding(6);
    }

    // ======================================================================
    // MINIMAL — no borders/shading, thin dividers, generous whitespace
    // ======================================================================
    private static void ComposeMinimal(PageDescriptor page, InvoicePdfModel model, byte[]? logo)
    {
        page.Margin(44);

        page.Header().Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                c.Item().Text(model.DocTypeName).FontSize(24).FontColor("#1E293B");
                c.Item().PaddingTop(2).Text($"{model.DocNumber}").FontSize(10).FontColor("#94A3B8");
            });

            if (logo != null)
                row.ConstantItem(70).Height(46).Image(logo).FitArea();
        });

        page.Content().PaddingTop(24).Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Text($"Date: {model.IssueDate}").FontSize(9).FontColor("#94A3B8");
                row.RelativeItem().AlignRight().Text($"Turnover: {model.TurnoverDate}").FontSize(9).FontColor("#94A3B8");
            });

            col.Item().PaddingVertical(16).LineHorizontal(0.5f).LineColor("#E2E8F0");

            col.Item().PaddingBottom(20).Row(row =>
            {
                row.RelativeItem().Element(c => MinimalParty(c, "Seller",
                    model.SellerName, model.SellerEdb, model.SellerVatNumber, model.SellerAddress));
                row.ConstantItem(30);
                row.RelativeItem().Element(c => MinimalParty(c, "Buyer",
                    model.BuyerName, model.BuyerEdb, model.BuyerVatNumber, model.BuyerAddress));
            });

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn();
                    columns.RelativeColumn(1.4f);
                    columns.RelativeColumn();
                    columns.RelativeColumn(1.4f);
                });

                table.Header(header =>
                {
                    MinimalHeaderCell(header, "Description");
                    MinimalHeaderCell(header, "Qty");
                    MinimalHeaderCell(header, "Price");
                    MinimalHeaderCell(header, "VAT");
                    MinimalHeaderCell(header, "Total");
                });

                foreach (var line in model.Lines)
                {
                    MinimalBodyCell(table).Text(line.Description);
                    MinimalBodyCell(table).AlignRight().Text(Num(line.Qty));
                    MinimalBodyCell(table).AlignRight().Text(Num(line.UnitPrice));
                    MinimalBodyCell(table).AlignRight().Text(line.VatLabel);
                    MinimalBodyCell(table).AlignRight().Text(Num(line.LineGross));
                }
            });

            col.Item().PaddingTop(18).AlignRight().Column(c =>
            {
                TotalsRow(c, "Subtotal", Num(model.NetAmount), model.Currency, false);
                TotalsRow(c, "VAT", Num(model.VatAmount), model.Currency, false);
                c.Item().PaddingTop(6).Width(220).LineHorizontal(0.5f).LineColor("#1E293B");
                c.Item().PaddingTop(6).Width(220).Row(r =>
                {
                    r.RelativeItem().Text("Total").FontSize(13);
                    r.ConstantItem(110).AlignRight().Text($"{Num(model.GrossAmount)} {model.Currency}").FontSize(13).Bold();
                });
            });
        });

        page.Footer().AlignCenter().Text(t =>
        {
            t.DefaultTextStyle(s => s.FontColor("#CBD5E1").FontSize(8));
            t.CurrentPageNumber();
            t.Span(" / ");
            t.TotalPages();
        });
    }

    private static void MinimalParty(IContainer container, string title,
        string name, string edb, string vat, string address)
    {
        container.Column(c =>
        {
            c.Item().Text(title.ToUpperInvariant()).FontSize(8).FontColor("#94A3B8").LetterSpacing(0.08f);
            c.Item().PaddingTop(3).Text(name);
            if (!string.IsNullOrWhiteSpace(address))
                c.Item().Text(address).FontColor("#64748B");
            c.Item().Text($"EDB {edb}").FontColor("#64748B");
            if (!string.IsNullOrWhiteSpace(vat))
                c.Item().Text($"VAT {vat}").FontColor("#64748B");
        });
    }

    private static void MinimalHeaderCell(TableCellDescriptor header, string text)
    {
        header.Cell().BorderBottom(0.5f).BorderColor("#1E293B").PaddingBottom(4)
            .Text(text.ToUpperInvariant()).FontSize(8).FontColor("#94A3B8").LetterSpacing(0.04f);
    }

    private static IContainer MinimalBodyCell(TableDescriptor table)
    {
        return table.Cell().BorderBottom(0.5f).BorderColor("#F1F5F9").PaddingVertical(6);
    }

    // ======================================================================
    // Shared helpers
    // ======================================================================
    private static void TotalsRow(ColumnDescriptor c, string label, string value, string currency, bool bold)
    {
        c.Item().Width(240).Row(r =>
        {
            var labelText = r.RelativeItem().Text(label);
            var valueText = r.ConstantItem(110).AlignRight().Text($"{value} {currency}");

            if (bold)
            {
                labelText.Bold();
                valueText.Bold().FontColor("#1B3A6B");
            }
            else
            {
                labelText.FontColor("#64748B");
            }
        });
    }

    private static string Num(decimal value) =>
        value.ToString("N2", CultureInfo.InvariantCulture);
}