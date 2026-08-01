using System.Globalization;
using System.IO;
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

    public byte[] Generate(InvoicePdfModel model)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(t => t.FontSize(10).FontFamily(FontFamily).FontColor("#1E293B"));

                page.Header().Element(h => ComposeHeader(h, model));
                page.Content().Element(c => ComposeContent(c, model));
                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Страна ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    public void Save(InvoicePdfModel model, string filePath)
    {
        File.WriteAllBytes(filePath, Generate(model));
    }

    private static void ComposeHeader(IContainer container, InvoicePdfModel model)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
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
    }

    private static void ComposeContent(IContainer container, InvoicePdfModel model)
    {
        container.PaddingVertical(10).Column(col =>
        {
            // Seller / Buyer blocks
            col.Item().PaddingBottom(12).Row(row =>
            {
                row.RelativeItem().Element(c => ComposeParty(c, "Издавач / Seller",
                    model.SellerName, model.SellerEdb, model.SellerVatNumber, model.SellerAddress));
                row.ConstantItem(20);
                row.RelativeItem().Element(c => ComposeParty(c, "Купувач / Buyer",
                    model.BuyerName, model.BuyerEdb, model.BuyerVatNumber, model.BuyerAddress));
            });

            // Line items table
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(22);   // #
                    columns.RelativeColumn(3);    // description
                    columns.RelativeColumn();     // qty
                    columns.RelativeColumn(1.4f); // unit price
                    columns.RelativeColumn();     // vat %
                    columns.RelativeColumn(1.4f); // net
                    columns.RelativeColumn(1.4f); // total
                });

                table.Header(header =>
                {
                    HeaderCell(header, "#");
                    HeaderCell(header, "Опис / Description");
                    HeaderCell(header, "Кол.");
                    HeaderCell(header, "Цена");
                    HeaderCell(header, "ДДВ");
                    HeaderCell(header, "Основа");
                    HeaderCell(header, "Вкупно");
                });

                foreach (var line in model.Lines)
                {
                    BodyCell(table).Text(line.LineNo.ToString());
                    BodyCell(table).Text(line.Description);
                    BodyCell(table).AlignRight().Text(Num(line.Qty));
                    BodyCell(table).AlignRight().Text(Num(line.UnitPrice));
                    BodyCell(table).AlignRight().Text(line.VatLabel);
                    BodyCell(table).AlignRight().Text(Num(line.LineNet));
                    BodyCell(table).AlignRight().Text(Num(line.LineGross));
                }
            });

            // Totals
            col.Item().PaddingTop(12).AlignRight().Column(c =>
            {
                c.Item().Width(240).Row(r =>
                {
                    r.RelativeItem().Text("Основа / Subtotal:").FontColor("#64748B");
                    r.ConstantItem(110).AlignRight().Text($"{Num(model.NetAmount)} {model.Currency}");
                });
                c.Item().Width(240).Row(r =>
                {
                    r.RelativeItem().Text("ДДВ / VAT:").FontColor("#64748B");
                    r.ConstantItem(110).AlignRight().Text($"{Num(model.VatAmount)} {model.Currency}");
                });
                c.Item().PaddingTop(4).Width(240).Row(r =>
                {
                    r.RelativeItem().Text("Вкупно / Total:").Bold();
                    r.ConstantItem(110).AlignRight().Text($"{Num(model.GrossAmount)} {model.Currency}").Bold().FontColor("#1B3A6B");
                });
            });
        });
    }

    private static void ComposeParty(IContainer container, string title,
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

    private static void HeaderCell(TableCellDescriptor header, string text)
    {
        header.Cell().Background("#F1F5F9").Padding(5)
            .Text(text).FontSize(9).Bold().FontColor("#64748B");
    }

    private static IContainer BodyCell(TableDescriptor table)
    {
        return table.Cell().BorderBottom(1).BorderColor("#F1F5F9").Padding(5);
    }

    private static string Num(decimal value) =>
        value.ToString("N2", CultureInfo.InvariantCulture);
}
