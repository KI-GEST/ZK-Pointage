using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KIGest.App.Services;

/// <summary>Exports Excel (ClosedXML) et PDF (QuestPDF) pour les rapports. Marque : rouge #FF4055.</summary>
public static class ExportHelper
{
    private const string BrandHex = "#FF4055";
    private const string BrandHexArgb = "FFFF4055";

    public static void ExportExcel(string titre, string sousTitre, IReadOnlyList<string> entetes,
        IReadOnlyList<IReadOnlyList<object?>> lignes, string filePath)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Rapport");

        // Titre
        ws.Cell(1, 1).Value = titre;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 18;
        ws.Cell(1, 1).Style.Font.FontColor = XLColor.FromArgb(255, 64, 85);
        ws.Range(1, 1, 1, Math.Max(1, entetes.Count)).Merge();

        ws.Cell(2, 1).Value = sousTitre;
        ws.Cell(2, 1).Style.Font.FontColor = XLColor.Gray;
        ws.Cell(2, 1).Style.Font.FontSize = 11;
        ws.Range(2, 1, 2, Math.Max(1, entetes.Count)).Merge();

        int headerRow = 4;
        for (int i = 0; i < entetes.Count; i++)
        {
            var c = ws.Cell(headerRow, i + 1);
            c.Value = entetes[i];
            c.Style.Font.Bold = true;
            c.Style.Font.FontColor = XLColor.White;
            c.Style.Fill.BackgroundColor = XLColor.FromArgb(255, 64, 85);
            c.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        int r = headerRow + 1;
        foreach (var ligne in lignes)
        {
            for (int i = 0; i < Math.Min(entetes.Count, ligne.Count); i++)
            {
                var cell = ws.Cell(r, i + 1);
                var v = ligne[i];
                if (v is null) cell.Value = string.Empty;
                else if (v is DateTime dt) { cell.Value = dt; cell.Style.DateFormat.Format = "dd/MM/yyyy HH:mm"; }
                else if (v is double dbl) { cell.Value = dbl; cell.Style.NumberFormat.Format = "0.00"; }
                else if (v is int num) { cell.Value = num; }
                else cell.Value = v.ToString();
            }
            if ((r - headerRow) % 2 == 0)
                ws.Range(r, 1, r, entetes.Count).Style.Fill.BackgroundColor = XLColor.FromArgb(255, 245, 247);
            r++;
        }

        var tableRange = ws.Range(headerRow, 1, r - 1, entetes.Count);
        tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        tableRange.Style.Border.OutsideBorderColor = XLColor.FromArgb(203, 213, 225);
        tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        tableRange.Style.Border.InsideBorderColor = XLColor.FromArgb(226, 232, 240);
        tableRange.SetAutoFilter(true);

        ws.SheetView.FreezeRows(headerRow);
        ws.Columns().AdjustToContents();
        foreach (var col in ws.ColumnsUsed())
        {
            if (col.Width > 60) col.Width = 60;
            if (col.Width < 12) col.Width = 12;
        }
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.FitToPages(1, 0);
        ws.PageSetup.Footer.Center.AddText("KI-GEST Pointage — généré le " + DateTime.Now.ToString("dd/MM/yyyy HH:mm")).SetFontSize(9).SetFontColor(XLColor.Gray);

        wb.SaveAs(filePath);
    }

    public static void ExportPdf(string titre, string sousTitre, IReadOnlyList<string> entetes,
        IReadOnlyList<IReadOnlyList<object?>> lignes, string filePath, bool paysage = false)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        // Ne pas échouer si la police système demandée est introuvable
        QuestPDF.Settings.ThrowOnMissingFontFamilies = false;

        Document.Create(container =>
        {
            container.Page(page =>
            {
                var size = PageSizes.A4;
                page.Size(paysage ? size.Landscape() : size);
                page.Margin(25);
                page.DefaultTextStyle(t => t.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text(titre).Bold().FontSize(18).FontColor(BrandHex);
                    col.Item().Text(sousTitre).FontSize(10).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(4).BorderBottom(2).BorderColor(BrandHex).Height(2);
                });

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var _ in entetes) columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        foreach (var entete in entetes)
                        {
                            header.Cell().Background(BrandHex).PaddingVertical(5).PaddingHorizontal(4)
                                .Text(entete).Bold().FontColor(Colors.White);
                        }
                    });

                    var bgWhite = Colors.White;
                    var bgOdd = Color.FromHex("#FFF5F7");
                    int idx = 0;
                    foreach (var ligne in lignes)
                    {
                        var bg = idx++ % 2 == 0 ? bgWhite : bgOdd;
                        foreach (var valeur in ligne)
                        {
                            string texte = valeur is DateTime dt
                                ? dt.ToString("dd/MM/yyyy HH:mm")
                                : valeur is double d ? d.ToString("0.00") : valeur?.ToString() ?? string.Empty;
                            table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#E2E8F0")
                                .PaddingVertical(4).PaddingHorizontal(4).Text(texte);
                        }
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("KI-GEST Pointage — généré le " + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + " — page ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf(filePath);
    }
}
