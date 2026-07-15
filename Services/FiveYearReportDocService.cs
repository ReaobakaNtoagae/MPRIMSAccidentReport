using CrashReport.ViewModels;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using static CrashReport.ViewModels.FiveYearReportRequest;

namespace CrashReport.Services;

public class FiveYearReportDocService
{
    private const string FontName = "Arial";

    private const int PageWidthDxa = 9026;

    public Task<byte[]> GenerateAsync(FiveYearReportViewModel vm)
    {
        using var stream = new MemoryStream();

        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            // ── Title ──
            body.AppendChild(Heading(vm.ReportTitle, size: 32, spacingAfter: 200));
            body.AppendChild(Heading($"{vm.StartYear} \u2013 {vm.EndYear}", size: 24, spacingAfter: 300));

            // ── Memo header block ──
            body.AppendChild(Para($"Report date: {vm.ReportDate}"));
            body.AppendChild(Para($"Ref: {vm.RefNumber}"));
            body.AppendChild(Para($"Enquiries: {vm.EnquiryName}  \u00b7  Tel: {vm.EnquiryTel}"));
            body.AppendChild(Para($"To: {vm.ToName}, {vm.ToTitle}"));
            body.AppendChild(Para($"From: {vm.FromName}, {vm.FromTitle}"));
            body.AppendChild(new Paragraph(new Run(new Break())));

            var years = Enumerable.Range(vm.StartYear, vm.EndYear - vm.StartYear + 1).ToArray();

            // ── Section 1: Regional status ──
            foreach (var region in vm.RegionSummaries)
            {
                body.AppendChild(Heading($"REGIONAL STATUS: {region.RegionName}", size: 24));
                body.AppendChild(BuildRegionSummaryTable(region, years));
                body.AppendChild(SpacerParagraph());
            }

            // ── Section 2: Problematic routes ──
            body.AppendChild(Heading("PROVINCIAL PROBLEMATIC ROUTES: CRASHES", size: 24));
            body.AppendChild(BuildRankedTable(vm.ProvincialRoutes.CrashRoutes, years, "ROUTES"));
            body.AppendChild(SpacerParagraph());

            body.AppendChild(Heading("PROVINCIAL PROBLEMATIC ROUTES: FATALITIES", size: 24));
            body.AppendChild(BuildRankedTable(vm.ProvincialRoutes.FatalityRoutes, years, "ROUTES"));
            body.AppendChild(SpacerParagraph());

            foreach (var region in vm.RegionRoutes)
            {
                body.AppendChild(Heading($"{region.RegionName} REGION", size: 24));
                body.AppendChild(BuildRankedTable(region.CrashRoutes, years, "ROUTES"));
                body.AppendChild(SpacerParagraph());

                body.AppendChild(Heading($"{region.RegionName} REGION PROBLEMATIC ROUTES: FATALITIES", size: 24));
                body.AppendChild(BuildRankedTable(region.FatalityRoutes, years, "ROUTES"));
                body.AppendChild(SpacerParagraph());
            }

            // ── Section 3: Crash types & vehicle categories ──
            body.AppendChild(Heading("PROVINCIAL CRASH TYPES", size: 24));
            body.AppendChild(BuildRankedTable(vm.CrashTypes, years, "TYPE"));
            body.AppendChild(SpacerParagraph());

            body.AppendChild(Heading("PROVINCIAL VEHICLE CATEGORIES", size: 24));
            body.AppendChild(BuildRankedTable(vm.VehicleCategories, years, "CATEGORY"));
            body.AppendChild(SpacerParagraph());

            // ── Section 4: Time of day ──
            body.AppendChild(Heading("PROVINCE PREVALENT TIMES: CRASHES", size: 24));
            body.AppendChild(BuildRankedTable(vm.TimeSlotsCrashes, years, "TIME"));
            body.AppendChild(SpacerParagraph());

            body.AppendChild(Heading("PROVINCE PREVALENT TIMES: FATALITIES", size: 24));
            body.AppendChild(BuildRankedTable(vm.TimeSlotsFatalities, years, "TIME"));
            body.AppendChild(SpacerParagraph());

            // ── Section 5: Day of week ──
            body.AppendChild(Heading("PROVINCE DAYS OF THE WEEK: CRASHES", size: 24));
            body.AppendChild(BuildRankedTable(vm.DaysOfWeekCrashes, years, "DAY"));
            body.AppendChild(SpacerParagraph());

            body.AppendChild(Heading("PROVINCE DAYS OF THE WEEK: FATALITIES", size: 24));
            body.AppendChild(BuildRankedTable(vm.DaysOfWeekFatalities, years, "DAY"));
            body.AppendChild(SpacerParagraph());

            // ── Section 6: Shock weekend ──
            body.AppendChild(Heading(vm.WeekendsCrashes.Title, size: 24));
            body.AppendChild(BuildRankedTable(vm.WeekendsCrashes, years, "WEEKEND"));
            body.AppendChild(SpacerParagraph());

            body.AppendChild(Heading(vm.WeekendsFatalities.Title, size: 24));
            body.AppendChild(BuildRankedTable(vm.WeekendsFatalities, years, "WEEKEND"));
            body.AppendChild(SpacerParagraph());

            // ── Section 7: Demographics (data-quality caveat) ──
            body.AppendChild(Heading("PROVINCE: VICTIMS PER AGE GROUP", size: 24));
            if (vm.DemographicsHasGaps)
            {
                var missing = years.Except(vm.DemographicYearsAvailable);
                body.AppendChild(CaveatParagraph(
                    $"Note: demographic data was not submitted for {string.Join(", ", missing)}. " +
                    "This section is sourced from manually-captured station summaries, which are " +
                    "known to be incomplete in some periods — treat these figures as indicative, " +
                    "not a complete count of all victims."));
            }
            body.AppendChild(BuildDemographicTable(vm.AgeGroups, vm.DemographicYearsAvailable, "AGE"));
            body.AppendChild(SpacerParagraph());

            body.AppendChild(Heading("PROVINCE: VICTIMS PER GENDER \u2014 MALE", size: 24));
            body.AppendChild(BuildDemographicTable(vm.MaleByRole, vm.DemographicYearsAvailable, "MALE"));
            body.AppendChild(SpacerParagraph());

            body.AppendChild(Heading("PROVINCE: VICTIMS PER GENDER \u2014 FEMALE", size: 24));
            body.AppendChild(BuildDemographicTable(vm.FemaleByRole, vm.DemographicYearsAvailable, "FEMALE"));

            mainPart.Document.Save();
        }

        return Task.FromResult(stream.ToArray());
    }

  
    private static int[] ComputeWidths(int columnCount, int labelWidth = 2200)
    {
        if (columnCount <= 1) return new[] { PageWidthDxa };

        var widths = new int[columnCount];
        widths[0] = labelWidth;
        var remaining = PageWidthDxa - labelWidth;
        var per = remaining / (columnCount - 1);

        for (int i = 1; i < columnCount; i++) widths[i] = per;
      
        widths[columnCount - 1] += remaining - per * (columnCount - 1);
        return widths;
    }

   
    private static Table BuildRegionSummaryTable(RegionSummary region, int[] years)
    {
        var widths = ComputeWidths(years.Length + 3);

        var headerCells = new List<string> { "" };
        headerCells.AddRange(years.Select(y => y.ToString()));
        headerCells.Add("TOTAL");
        headerCells.Add("AVERAGE");

        var rows = new List<TableRow> { MakeRow(headerCells, widths, bold: true, shaded: true, isHeader: true) };

        foreach (var stat in region.Stats)
        {
            var cells = new List<string> { stat.Label };
            cells.AddRange(stat.Years.Select(v => v.ToString()));
            cells.Add(stat.Total.ToString());
            cells.Add(stat.Average.ToString());
            rows.Add(MakeRow(cells, widths));
        }

        return MakeTable(rows, widths);
    }

    private static Table BuildRankedTable(RankedTable table, int[] years, string labelHeader)
    {
        var widths = ComputeWidths(years.Length + 4);

        var headerCells = new List<string> { labelHeader };
        headerCells.AddRange(years.Select(y => y.ToString()));
        headerCells.Add("TOTAL");
        headerCells.Add("AVERAGE");
        headerCells.Add("%");

        var rows = new List<TableRow> { MakeRow(headerCells, widths, bold: true, shaded: true, isHeader: true) };

        foreach (var row in table.Rows)
        {
            var cells = new List<string> { row.Label };
            cells.AddRange(row.Years.Select(v => v.ToString()));
            cells.Add(row.Total.ToString());
            cells.Add(row.Average.ToString());
            cells.Add($"{row.Percent:0}%");
            rows.Add(MakeRow(cells, widths));
        }

        // TOTAL footer row, matching the reference document's tables
        var totalCells = new List<string> { "TOTAL" };
        for (int i = 0; i < years.Length; i++)
            totalCells.Add(table.Rows.Sum(r => r.Years.Length > i ? r.Years[i] : 0).ToString());
        totalCells.Add(table.GrandTotal.ToString());
        totalCells.Add("");
        totalCells.Add("100%");
        rows.Add(MakeRow(totalCells, widths, bold: true));

        return MakeTable(rows, widths);
    }

    
    private static Table BuildDemographicTable(List<DemographicYearRow> rows, List<int> availableYears, string cornerLabel)
    {
        var widths = ComputeWidths(availableYears.Count + 2);

        var headerCells = new List<string> { cornerLabel };
        headerCells.AddRange(availableYears.Select(y => y.ToString()));
        headerCells.Add("TOTAL");

        var tableRows = new List<TableRow> { MakeRow(headerCells, widths, bold: true, shaded: true, isHeader: true) };

        foreach (var row in rows)
        {
            var cells = new List<string> { row.Label };
            cells.AddRange(availableYears.Select(y => row.ByYear.TryGetValue(y, out var v) ? v.ToString() : "0"));
            cells.Add(row.Total.ToString());
            tableRows.Add(MakeRow(cells, widths));
        }

        var totalRow = new List<string> { "TOTAL" };
        foreach (var y in availableYears)
            totalRow.Add(rows.Sum(r => r.ByYear.TryGetValue(y, out var v) ? v : 0).ToString());
        totalRow.Add(rows.Sum(r => r.Total).ToString());
        tableRows.Add(MakeRow(totalRow, widths, bold: true));

        return MakeTable(tableRows, widths);
    }


    private static Paragraph Heading(string text, int size = 22, int spacingAfter = 120)
    {
        var run = new Run(new Text(text));
        run.RunProperties = new RunProperties(
            new RunFonts { Ascii = FontName },
            new Bold(),
            new FontSize { Val = size.ToString() });

        return new Paragraph(run)
        {
            ParagraphProperties = new ParagraphProperties(
                new SpacingBetweenLines { After = spacingAfter.ToString() })
        };
    }

    private static Paragraph Para(string text, int size = 20)
    {
        var run = new Run(new Text(text));
        run.RunProperties = new RunProperties(
            new RunFonts { Ascii = FontName },
            new FontSize { Val = size.ToString() });
        return new Paragraph(run);
    }

    private static Paragraph CaveatParagraph(string text)
    {
        var run = new Run(new Text(text));
        run.RunProperties = new RunProperties(
            new RunFonts { Ascii = FontName },
            new Italic(),
            new FontSize { Val = "18" },
            new Color { Val = "B45309" });
        return new Paragraph(run)
        {
            ParagraphProperties = new ParagraphProperties(
                new SpacingBetweenLines { After = "160" })
        };
    }

    private static Paragraph SpacerParagraph() =>
        new(new ParagraphProperties(new SpacingBetweenLines { After = "300" }));

    private static TableRow MakeRow(
        IReadOnlyList<string> cellTexts, int[] widths,
        bool bold = false, bool shaded = false, bool isHeader = false)
    {
        var row = new TableRow();

        if (isHeader)
            row.AppendChild(new TableRowProperties(new TableHeader()));

        for (int i = 0; i < cellTexts.Count; i++)
        {
            var width = i < widths.Length ? widths[i] : widths[^1];
            row.AppendChild(MakeCell(cellTexts[i], width, bold, shaded));
        }
        return row;
    }

    private static TableCell MakeCell(string text, int widthDxa, bool bold = false, bool shaded = false)
    {
        var run = new Run(new Text(text ?? string.Empty));
        run.RunProperties = new RunProperties(
            new RunFonts { Ascii = FontName },
            new FontSize { Val = "18" });
        if (bold) run.RunProperties.AppendChild(new Bold());

        var cell = new TableCell(new Paragraph(run));

       
        var cellProps = new TableCellProperties();

        cellProps.AppendChild(new TableCellWidth
        {
            Width = widthDxa.ToString(),
            Type = TableWidthUnitValues.Dxa
        });

        if (shaded)
            cellProps.AppendChild(new Shading { Val = ShadingPatternValues.Clear, Fill = "D9D9D9" });

        cellProps.AppendChild(new TableCellMargin(
            new TopMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
            new LeftMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
            new BottomMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
            new RightMargin { Width = "80", Type = TableWidthUnitValues.Dxa }));

        cell.TableCellProperties = cellProps;
        return cell;
    }

    private static Table MakeTable(List<TableRow> rows, int[] widths)
    {
        var table = new Table();

       
        var tableProps = new TableProperties(
            new TableWidth { Width = PageWidthDxa.ToString(), Type = TableWidthUnitValues.Dxa },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = "999999" },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "999999" },
                new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "999999" },
                new RightBorder { Val = BorderValues.Single, Size = 4, Color = "999999" },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" }
            ),
            new TableLayout { Type = TableLayoutValues.Fixed }
        );
        table.AppendChild(tableProps);

        var grid = new TableGrid();
        foreach (var w in widths)
            grid.AppendChild(new GridColumn { Width = w.ToString() });
        table.AppendChild(grid);

        foreach (var row in rows)
            table.AppendChild(row);

        return table;
    }
}