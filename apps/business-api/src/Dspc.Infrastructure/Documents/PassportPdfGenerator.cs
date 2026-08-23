using Dspc.Application.Modules.Passports;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Dspc.Infrastructure.Documents;

/// <summary>
/// Offline A4 passport renderer. QuestPDF Community licence (see docs/licenses.md); the bundled Lato family covers
/// Polish diacritics, so no font is fetched at runtime. Every page carries the demonstrator disclaimer, the document
/// version and — in the footer — the SHA-256 placeholder resolved by the caller after hashing is impossible, hence the
/// hash of the rendered bytes is stored alongside the version and printed on the QR page instead.
/// </summary>
public sealed class PassportPdfGenerator : IPassportPdfGenerator
{
    private const string DisclaimerPl = "Demonstrator wykorzystuje fikcyjne dane. Prezentowane mapowanie wymagań jakościowych nie stanowi formalnego potwierdzenia zgodności ani certyfikacji.";
    private const string DisclaimerEn = "This demonstrator uses fictional data. The quality-requirement mapping shown is not a formal statement of conformity or certification.";

    private static readonly Color Ink = Color.FromHex("#0F172A");
    private static readonly Color Muted = Color.FromHex("#475569");
    private static readonly Color Line = Color.FromHex("#CBD5E1");
    private static readonly Color Accent = Color.FromHex("#0E7490");
    private static readonly Color Warn = Color.FromHex("#B45309");

    static PassportPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.EnableDebugging = false;
    }

    public byte[] RenderQr(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(8);
    }

    public byte[] Render(PassportRenderModel m)
    {
        var qr = RenderQr(m.QrPayload);
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(18, Unit.Millimetre);
                page.DefaultTextStyle(t => t.FontFamily("Lato").FontSize(9).FontColor(Ink));

                page.Header().Element(e => Header(e, m, qr));
                page.Content().PaddingVertical(8).Element(e => Content(e, m));
                page.Footer().Element(e => Footer(e, m));
            });
        }).GeneratePdf();
    }

    private static void Header(IContainer c, PassportRenderModel m, byte[] qr) => c.Column(col =>
    {
        col.Item().Row(row =>
        {
            row.RelativeItem().Column(left =>
            {
                left.Item().Text("CYFROWY PASZPORT JAKOŚCIOWY").FontSize(15).Bold().FontColor(Accent);
                left.Item().Text("Digital Quality Passport").FontSize(9).FontColor(Muted);
                left.Item().PaddingTop(6).Text(t =>
                {
                    t.Span("Numer seryjny / Serial: ").FontColor(Muted);
                    t.Span(m.Serial).Bold().FontSize(11);
                });
                left.Item().Text(t =>
                {
                    t.Span("Szablon / Template: ").FontColor(Muted);
                    t.Span($"{m.TemplateCode}   ");
                    t.Span("Wersja dokumentu / Version: ").FontColor(Muted);
                    t.Span($"v{m.Version}").Bold();
                });
            });
            row.ConstantItem(78).Column(right =>
            {
                right.Item().Width(72).Image(qr).FitWidth();
                right.Item().AlignCenter().Text("skan → rekord").FontSize(6.5f).FontColor(Muted);
            });
        });
        col.Item().PaddingTop(6).Background(Color.FromHex("#FEF3C7")).Padding(5).Column(d =>
        {
            d.Item().Text("DOKUMENT DEMONSTRACYJNY / DEMONSTRATION DOCUMENT").FontSize(7.5f).Bold().FontColor(Warn);
            d.Item().Text(DisclaimerPl).FontSize(6.5f).FontColor(Warn);
            d.Item().Text(DisclaimerEn).FontSize(6.5f).FontColor(Warn).Italic();
        });
        col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Line);
    });

    private static void Content(IContainer c, PassportRenderModel m) => c.Column(col =>
    {
        col.Spacing(10);

        col.Item().Element(e => Section(e, "1. Wyrób i zlecenie / Product and order", inner => inner.Column(g =>
        {
            g.Item().Row(r =>
            {
                Field(r, "Wyrób / Product", $"{m.ProductCode} — {m.ProductName}");
                Field(r, "Zlecenie / Order", m.OrderCode);
            });
            g.Item().PaddingTop(3).Row(r =>
            {
                Field(r, "Wersja BOM / BOM version", m.BomVersion);
                Field(r, "Zakład / Site", m.SiteName);
            });
        })));

        col.Item().Element(e => Section(e, "2. Kluczowe komponenty i partie / Key components and lots", inner => inner.Table(table =>
        {
            table.ColumnsDefinition(d =>
            {
                d.RelativeColumn(1.6f); d.RelativeColumn(1.7f); d.RelativeColumn(1.2f);
                d.RelativeColumn(1.9f); d.RelativeColumn(0.7f); d.RelativeColumn(1.2f); d.RelativeColumn(3.1f);
            });
            HeaderCells(table, "Indeks", "Partia / Lot", "Wytop / Heat", "Dostawca", "Kraj", "Status QC", "Certyfikat — SHA-256");
            foreach (var comp in m.Components)
            {
                Cell(table, comp.PartCode, bold: true);
                Cell(table, comp.LotNumber);
                Cell(table, comp.HeatNumber ?? "—");
                Cell(table, comp.SupplierName is null ? comp.SupplierCode : $"{comp.SupplierCode} · {comp.SupplierName}");
                Cell(table, comp.Country ?? "—");
                Cell(table, comp.LotStatus);
                Cell(table, comp.CertSha256 is null ? "—" : $"{comp.CertificateNumber}\n{comp.CertSha256}", mono: true);
            }
        })));

        col.Item().Element(e => Section(e, "3. Wyniki kontroli jakości / Quality inspection results", inner =>
        {
            if (m.Inspections.Count == 0) { inner.Text("Brak zarejestrowanych inspekcji. / No recorded inspections.").FontColor(Muted); return; }
            inner.Table(table =>
            {
                table.ColumnsDefinition(d => { d.RelativeColumn(1.4f); d.RelativeColumn(1f); d.RelativeColumn(1.4f); d.RelativeColumn(1.4f); d.RelativeColumn(4f); });
                HeaderCells(table, "Nr inspekcji", "Wynik", "Data", "Kontroler", "Uwagi");
                foreach (var i in m.Inspections)
                {
                    Cell(table, i.Code);
                    Cell(table, i.Result, bold: true);
                    Cell(table, i.InspectedAt.ToString("yyyy-MM-dd HH:mm"));
                    Cell(table, i.InspectedBy);
                    Cell(table, i.Notes ?? "—");
                }
            });
        }));

        col.Item().Element(e => Section(e, "4. Rejestr odstępstw i zatwierdzeń / Deviations and approvals", inner =>
        {
            if (m.Deviations.Count == 0) { inner.Text("Brak odstępstw. / No deviations recorded.").FontColor(Muted); return; }
            inner.Table(table =>
            {
                table.ColumnsDefinition(d => { d.RelativeColumn(1.2f); d.RelativeColumn(4.5f); d.RelativeColumn(1.3f); d.RelativeColumn(1.5f); });
                HeaderCells(table, "Kod", "Opis", "Zatwierdził", "Data");
                foreach (var d in m.Deviations)
                {
                    Cell(table, d.Code ?? "—");
                    Cell(table, d.Title);
                    Cell(table, d.ApprovedBy ?? "—");
                    Cell(table, d.ApprovedAt?.ToString("yyyy-MM-dd") ?? "—");
                }
            });
        }));

        col.Item().Element(e => Section(e, $"5. Kompletność wg szablonu {m.TemplateCode} / Completeness", inner => inner.Table(table =>
        {
            table.ColumnsDefinition(d => { d.RelativeColumn(2.6f); d.RelativeColumn(0.9f); d.RelativeColumn(5f); });
            HeaderCells(table, "Wymaganie", "Spełnione", "Dowód / Evidence");
            foreach (var (code, satisfied, evidence) in m.Requirements)
            {
                Cell(table, code);
                Cell(table, satisfied ? "TAK / YES" : "NIE / NO", bold: true);
                Cell(table, evidence ?? "—");
            }
        })));

        col.Item().Element(e => Section(e, "6. Zatwierdzenie / Approval", inner => inner.Row(r =>
        {
            Field(r, "Zatwierdził / Approved by", m.ApprovedBy ?? "—");
            Field(r, "Data zatwierdzenia / Approved at", m.ApprovedAt?.ToString("yyyy-MM-dd HH:mm") ?? "—");
            Field(r, "Wygenerował / Generated by", m.GeneratedBy);
            Field(r, "Data wygenerowania / Generated at", m.GeneratedAt.ToString("yyyy-MM-dd HH:mm 'UTC'"));
        })));
    });

    private static void Footer(IContainer c, PassportRenderModel m) => c.Column(col =>
    {
        col.Item().PaddingBottom(3).LineHorizontal(1).LineColor(Line);
        col.Item().Row(r =>
        {
            r.RelativeItem().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(6.5f).FontColor(Muted));
                t.Span($"{m.Serial} · {m.TemplateCode} v{m.Version} · ");
                t.Span("dokument demonstracyjny — nie stanowi potwierdzenia zgodności").Italic();
            });
            r.ConstantItem(90).AlignRight().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(6.5f).FontColor(Muted));
                t.Span("Strona ");
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });
        });
    });

    private static void Section(IContainer c, string title, Action<IContainer> body) => c.Column(col =>
    {
        col.Item().PaddingBottom(3).Text(title).FontSize(10).Bold().FontColor(Accent);
        col.Item().Border(1).BorderColor(Line).Padding(6).Element(body);
    });

    private static void Field(RowDescriptor row, string label, string value) => row.RelativeItem().Column(c =>
    {
        c.Item().Text(label).FontSize(6.5f).FontColor(Muted);
        c.Item().Text(value).FontSize(9);
    });

    private static void HeaderCells(TableDescriptor table, params string[] headers)
    {
        table.Header(h =>
        {
            foreach (var text in headers)
                h.Cell().Background(Color.FromHex("#F1F5F9")).BorderBottom(1).BorderColor(Line).Padding(3)
                    .Text(text).FontSize(7).Bold().FontColor(Muted);
        });
    }

    private static void Cell(TableDescriptor table, string text, bool bold = false, bool mono = false)
    {
        var span = table.Cell().BorderBottom(1).BorderColor(Color.FromHex("#E2E8F0")).Padding(3)
            .Text(text).FontSize(mono ? 6.2f : 7.5f).FontFamily(mono ? "Courier New" : "Lato");
        if (bold) span.Bold();
    }
}
