using CrashReport.ViewModels;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Linq;

namespace CrashReport.Services;


public class StandbyReportWordService
{
    private const string NAVY = "1A3C6E";
    private const string NAVY2 = "253F6E";   // year sub-header row
    private const string LIGHT_BLUE = "E8F0FB";
    private const string ALT_ROW = "F5F8FF";
    private const string WHITE = "FFFFFF";
    private const string BLACK = "000000";
    private const string GREY_TEXT = "888888";

    private const string PT11 = "22";
    private const string PT9 = "18";
    private const string PT8 = "16";
    private const string PT7 = "14";

   


    private const int LBL_W = 1600;
    private const int YR_W = 1259;   // each year column
    private const int DIST_W = 2518;   // single-year district column

    
    public byte[] Generate(StandbyReportViewModel vm)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(BuildBody(vm));
            ApplyPageLayout(main);
            main.Document.Save();
            doc.Save();
        }
        return ms.ToArray();
    }

    private Body BuildBody(StandbyReportViewModel vm)
    {
        var body = new Body();
        bool prior = true;
        int py = vm.DateFrom.Year - 1;
        int cy = vm.DateFrom.Year;

        string fullRange = $"{vm.DateFrom:dd MMMM yyyy}".ToUpper() + " TO " + $"{vm.DateTo:dd MMMM yyyy}".ToUpper();

        // Title
        body.Append(StyledPara(PT11, true, JustificationValues.Center, 80, $"WEEKLY STATISTICS REPORT: {fullRange}"));
        body.Append(StyledPara(PT9, false, JustificationValues.Center, 80, $"({vm.DayRange})"));
        body.Append(BlankLine());

        // Full-week summary table
        var mainTable = BuildSixColTable(vm, prior, py, cy,
            new[] { "CRASHES", "FATALITIES", "SERIOUS", "SLIGHT" },
            new Func<DistrictStats, int>[] { d => d.Crashes, d => d.Fatalities, d => d.Serious, d => d.Slight },
            isTimeBand: false);
        body.Append(mainTable);
        body.Append(BlankLine());

        // Time-band table
        body.Append(StyledPara(PT9, true, JustificationValues.Left, 60, "FATALITIES"));
        var timeTable = BuildSixColTable(vm, prior, py, cy,
            new[] { "06H00 – 14H00", "14H00 – 22H00", "22H00 – 06H00" },
            new Func<DistrictStats, int>[] { d => d.FatalTime1, d => d.FatalTime2, d => d.FatalTime3 },
            isTimeBand: true);
        body.Append(timeTable);
        body.Append(BlankLine());

        // Narrative
        int tb1 = vm.CurrentProvince.FatalTime1, tb2 = vm.CurrentProvince.FatalTime2, tb3 = vm.CurrentProvince.FatalTime3;
        body.Append(StyledPara(PT9, false, JustificationValues.Left, 60,
            $"FATALITIES OCCURRED BETWEEN 06:00 TO 14:00 ({tb1}) 14:00 TO 22:00 ({tb2}) 22:00 TO 06:00 ({tb3})"));

        int totalPed = vm.CurrentProvince.FatalPedestrians;
        if (totalPed > 0)
        {
            body.Append(StyledPara(PT9, true, JustificationValues.Left, 60,
                $"PROVINCE HAD {totalPed} FATAL PEDESTRIAN{(totalPed != 1 ? "S" : "")}"));
            var parts = new List<string>();
            if (vm.CurrentEhlanzeni.FatalPedestrians > 0) parts.Add($"{vm.CurrentEhlanzeni.FatalPedestrians} (EHLANZENI)");
            if (vm.CurrentBohlabelo.FatalPedestrians > 0) parts.Add($"{vm.CurrentBohlabelo.FatalPedestrians} (BOHLABELO)");
            if (vm.CurrentGertSibande.FatalPedestrians > 0) parts.Add($"{vm.CurrentGertSibande.FatalPedestrians} (GERT SIBANDE)");
            if (vm.CurrentNkangala.FatalPedestrians > 0) parts.Add($"{vm.CurrentNkangala.FatalPedestrians} (NKANGALA)");
            body.Append(StyledPara(PT9, false, JustificationValues.Left, 80, string.Join("    ", parts)));
        }
        body.Append(BlankLine());

        // Problematic routes
        if (vm.ProblematicRoutes.Any())
        {
            body.Append(StyledPara(PT9, true, JustificationValues.Left, 60, "PROBLEMATIC ROUTES"));
            foreach (var district in vm.ProblematicRoutes.Select(r => r.District).Distinct().OrderBy(d => d))
            {
                body.Append(StyledPara(PT9, true, JustificationValues.Left, 40, $"{district} DISTRICT"));
                foreach (var r in vm.ProblematicRoutes.Where(x => x.District == district))
                {
                    string locs = string.IsNullOrWhiteSpace(r.Locations) ? "" : $" ({r.Locations})";
                    body.Append(IndentedPara($"– {r.Route} – {r.Crashes} Crash{(r.Crashes != 1 ? "es" : "")} with {r.Fatalities} Fatalit{(r.Fatalities != 1 ? "ies" : "y")}{locs}"));
                }
            }
            body.Append(BlankLine());
        }

        // Fatal crash detail
        var fatalDistricts = new[]
        {
            ("EHLANZENI", vm.CurrentEhlanzeni),
            ("BOHLABELO", vm.CurrentBohlabelo),
            ("GERT SIBANDE", vm.CurrentGertSibande),
            ("NKANGALA", vm.CurrentNkangala),
        }.Where(d => d.Item2.FatalDetails.Any()).ToList();

        if (fatalDistricts.Any())
        {
            body.Append(StyledPara(PT9, true, JustificationValues.Left, 60, "FATAL CRASH DETAIL"));
            body.Append(BuildFatalDetailTable(fatalDistricts));
            body.Append(BlankLine());
        }

        // Sub-period
        if (vm.SubPeriod is not null)
        {
            var sp = vm.SubPeriod;
            string spRange = $"{sp.From:dd MMMM yyyy}".ToUpper() + " – " + $"{sp.To:dd MMMM yyyy}".ToUpper();
            body.Append(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
            body.Append(StyledPara(PT11, true, JustificationValues.Center, 80, sp.Label.ToUpper()));
            body.Append(StyledPara(PT9, false, JustificationValues.Center, 120, spRange));

            body.Append(BuildSubPeriodTable(sp, cy));
            body.Append(BlankLine());
            body.Append(StyledPara(PT9, true, JustificationValues.Left, 60, "FATALITIES"));
            body.Append(BuildSubPeriodTimeBandTable(sp, cy));
            body.Append(BlankLine());
            body.Append(StyledPara(PT9, true, JustificationValues.Left, 60, "PROVINCE:"));
            body.Append(BuildAgeTable(vm.Victims));
            body.Append(BlankLine());
            body.Append(BuildGenderTable(vm.Victims));
        }

        body.Append(new SectionProperties());
        return body;
    }

    
    private Table BuildSixColTable(
        StandbyReportViewModel vm,
        bool hasPrior, int py, int cy,
        string[] rowLabels,
        Func<DistrictStats, int>[] getters,
        bool isTimeBand)
    {
        var districtNames = new[] { "PROVINCE", "EHLANZENI", "BOHLABELO", "GERT SIBANDE", "NKANGALA" };
        string firstColLabel = isTimeBand ? "PREVALENT TIME" : "";

        int[] colWidths;
        if (hasPrior)
        {
            colWidths = new[] { LBL_W }.Concat(Enumerable.Repeat(YR_W, 10)).ToArray();
        }
        else
        {
            colWidths = new[] { LBL_W }.Concat(Enumerable.Repeat(DIST_W, 5)).ToArray();
        }

        var table = NewTable(colWidths);
        var tableGrid = new TableGrid();
        foreach (var w in colWidths)
            tableGrid.AppendChild(new GridColumn { Width = w.ToString() });
        table.AppendChild(tableGrid);

        if (hasPrior)
        {
            // Header row 1: District names with colspan=2
            var hdrRow1 = new TableRow();
            var rowProps1 = new TableRowProperties();
            rowProps1.AppendChild(new TableRowHeight { Val = 400, HeightType = HeightRuleValues.Exact });
            hdrRow1.AppendChild(rowProps1);

            // First cell - empty corner
            var cornerCell = new TableCell();
            var cornerProps = new TableCellProperties(
                new TableCellWidth { Width = colWidths[0].ToString(), Type = TableWidthUnitValues.Dxa },
                new Shading { Fill = NAVY, Val = ShadingPatternValues.Clear });
            cornerCell.AppendChild(cornerProps);
            cornerCell.AppendChild(HdrPara(firstColLabel, NAVY, PT8));
            hdrRow1.AppendChild(cornerCell);

            // District cells with colspan=2
            for (int i = 0; i < districtNames.Length; i++)
            {
                var cell = new TableCell();
                var cellProps = new TableCellProperties(
                    new TableCellWidth { Width = (colWidths[1 + i * 2] + colWidths[2 + i * 2]).ToString(), Type = TableWidthUnitValues.Dxa },
                    new GridSpan { Val = 2 },
                    new Shading { Fill = NAVY, Val = ShadingPatternValues.Clear });
                cell.AppendChild(cellProps);
                cell.AppendChild(HdrPara(districtNames[i], NAVY, PT8));
                hdrRow1.AppendChild(cell);
            }
            table.Append(hdrRow1);

            // Header row 2: Years
            var hdrRow2 = new TableRow();
            var rowProps2 = new TableRowProperties();
            rowProps2.AppendChild(new TableRowHeight { Val = 300, HeightType = HeightRuleValues.Exact });
            hdrRow2.AppendChild(rowProps2);

            // Empty cell under corner
            var emptyCell = new TableCell();
            var emptyProps = new TableCellProperties(
                new TableCellWidth { Width = colWidths[0].ToString(), Type = TableWidthUnitValues.Dxa },
                new Shading { Fill = NAVY2, Val = ShadingPatternValues.Clear });
            emptyCell.AppendChild(emptyProps);
            emptyCell.AppendChild(new Paragraph());
            hdrRow2.AppendChild(emptyCell);

            // Year cells
            for (int i = 0; i < 5; i++)
            {
                hdrRow2.AppendChild(YearCell(py.ToString(), YR_W));
                hdrRow2.AppendChild(YearCell(cy.ToString(), YR_W));
            }
            table.Append(hdrRow2);
        }
        else
        {
            // Single-row header
            var hdrRow = new TableRow();
            var rowProps = new TableRowProperties();
            rowProps.AppendChild(new TableRowHeight { Val = 400, HeightType = HeightRuleValues.Exact });
            hdrRow.AppendChild(rowProps);

            var labelCell = new TableCell();
            var labelProps = new TableCellProperties(
                new TableCellWidth { Width = colWidths[0].ToString(), Type = TableWidthUnitValues.Dxa },
                new Shading { Fill = NAVY, Val = ShadingPatternValues.Clear });
            labelCell.AppendChild(labelProps);
            labelCell.AppendChild(HdrPara(firstColLabel, NAVY, PT8));
            hdrRow.AppendChild(labelCell);

            for (int i = 0; i < districtNames.Length; i++)
            {
                var cell = new TableCell();
                var cellProps = new TableCellProperties(
                    new TableCellWidth { Width = colWidths[i + 1].ToString(), Type = TableWidthUnitValues.Dxa },
                    new Shading { Fill = NAVY, Val = ShadingPatternValues.Clear });
                cell.AppendChild(cellProps);
                cell.AppendChild(HdrPara(districtNames[i], NAVY, PT8));
                hdrRow.AppendChild(cell);
            }
            table.Append(hdrRow);
        }

        // Data rows
        for (int i = 0; i < rowLabels.Length; i++)
        {
            var fn = getters[i];
            string bg = i % 2 == 1 ? ALT_ROW : WHITE;
            var tr = new TableRow();

            // Label cell
            var labelCell = new TableCell();
            var labelProps = new TableCellProperties(
                new TableCellWidth { Width = colWidths[0].ToString(), Type = TableWidthUnitValues.Dxa },
                new Shading { Fill = LIGHT_BLUE, Val = ShadingPatternValues.Clear });
            labelCell.AppendChild(labelProps);
            labelCell.AppendChild(DataPara(rowLabels[i], LIGHT_BLUE, true, true));
            tr.AppendChild(labelCell);

            if (hasPrior)
            {
                var districts = new[] { vm.PriorProvince, vm.CurrentProvince,
                                        vm.PriorEhlanzeni, vm.CurrentEhlanzeni,
                                        vm.PriorBohlabelo, vm.CurrentBohlabelo,
                                        vm.PriorGertSibande, vm.CurrentGertSibande,
                                        vm.PriorNkangala, vm.CurrentNkangala };
                for (int j = 0; j < districts.Length; j++)
                {
                    var ds = districts[j];
                    bool isCurrent = j % 2 == 1;
                    string textClr = isCurrent ? NAVY : GREY_TEXT;
                    bool bold = isCurrent;
                    int colWidth = YR_W;

                    var cell = new TableCell();
                    var cellProps = new TableCellProperties(
                        new TableCellWidth { Width = colWidth.ToString(), Type = TableWidthUnitValues.Dxa },
                        new Shading { Fill = bg, Val = ShadingPatternValues.Clear });
                    cell.AppendChild(cellProps);
                    cell.AppendChild(DataPara(fn(ds).ToString(), bg, bold, false, textClr));
                    tr.AppendChild(cell);
                }
            }
            else
            {
                var currentDistricts = new[] { vm.CurrentProvince, vm.CurrentEhlanzeni,
                                               vm.CurrentBohlabelo, vm.CurrentGertSibande,
                                               vm.CurrentNkangala };
                for (int j = 0; j < currentDistricts.Length; j++)
                {
                    var ds = currentDistricts[j];
                    string textClr = j == 0 ? NAVY : BLACK;
                    bool bold = j == 0;
                    int colWidth = DIST_W;

                    var cell = new TableCell();
                    var cellProps = new TableCellProperties(
                        new TableCellWidth { Width = colWidth.ToString(), Type = TableWidthUnitValues.Dxa },
                        new Shading { Fill = bg, Val = ShadingPatternValues.Clear });
                    cell.AppendChild(cellProps);
                    cell.AppendChild(DataPara(fn(ds).ToString(), bg, bold, false, textClr));
                    tr.AppendChild(cell);
                }
            }
            table.Append(tr);
        }

        return table;
    }

    // ── Sub-period tables (single year, simple header) ────────────────────────
    private Table BuildSubPeriodTable(SubPeriodStats sp, int cy)
    {
        int[] widths = { LBL_W, DIST_W, DIST_W, DIST_W, DIST_W, DIST_W };
        var t = NewTable(widths);
        t.Append(SimpleHdrRow(widths, new[] { "", "PROVINCE", "EHLANZENI", "BOHLABELO", "GERT SIBANDE", "NKANGALA" }));
        foreach (var (label, fn, i) in new (string, Func<DistrictStats, int>, int)[]
        {
            ("CRASHES",    d => d.Crashes,    0),
            ("FATALITIES", d => d.Fatalities, 1),
            ("SERIOUS",    d => d.Serious,    2),
            ("SLIGHT",     d => d.Slight,     3),
        })
        {
            t.Append(SingleYearRow(widths, label, fn, sp, i));
        }
        return t;
    }

    private Table BuildSubPeriodTimeBandTable(SubPeriodStats sp, int cy)
    {
        int[] widths = { LBL_W + 200, DIST_W, DIST_W, DIST_W, DIST_W, DIST_W };
        var t = NewTable(widths);
        t.Append(SimpleHdrRow(widths, new[]
        {
            "PREVALENT TIME",
            $"PROVINCE {cy}", $"EHLANZENI {cy}", $"BOHLABELO {cy}", $"GERT {cy}", $"NKANGALA {cy}"
        }));
        foreach (var (label, fn, i) in new (string, Func<DistrictStats, int>, int)[]
        {
            ("06H00 – 14H00", d => d.FatalTime1, 0),
            ("14H00 – 22H00", d => d.FatalTime2, 1),
            ("22H00 – 06H00", d => d.FatalTime3, 2),
        })
        {
            t.Append(SingleYearRow(widths, label, fn, sp, i));
        }
        return t;
    }

    private TableRow SingleYearRow(int[] widths, string label,
        Func<DistrictStats, int> fn, SubPeriodStats sp, int rowIdx)
    {
        string bg = rowIdx % 2 == 1 ? ALT_ROW : WHITE;
        var tr = new TableRow();
        var lc = MakeCell(widths[0], LIGHT_BLUE);
        lc.AppendChild(DataPara(label, LIGHT_BLUE, true, true));
        tr.AppendChild(lc);
        foreach (var (ds, i) in new[] { sp.Province, sp.Ehlanzeni, sp.Bohlabelo, sp.GertSibande, sp.Nkangala }
                                       .Select((d, i) => (d, i)))
        {
            var tc = MakeCell(widths[i + 1], bg);
            tc.AppendChild(DataPara(fn(ds).ToString(), bg, i == 0, false, i == 0 ? NAVY : BLACK));
            tr.AppendChild(tc);
        }
        return tr;
    }

    // ── Fatal crash detail table ──────────────────────────────────────────────
    private Table BuildFatalDetailTable(List<(string District, DistrictStats Stats)> districts)
    {
        int[] cols = { 1300, 1600, 1200, 900, 2200, 3500, 1688 };
        var t = NewTable(cols);
        t.Append(SimpleHdrRow(cols, new[] { "DISTRICT", "CR NO", "DATE", "TIME", "ROUTE", "LOCATION", "FATAL" }));
        int row = 0;
        foreach (var (district, stats) in districts)
        {
            foreach (var d in stats.FatalDetails)
            {
                string bg = row++ % 2 == 1 ? ALT_ROW : WHITE;
                var tr = new TableRow();
                foreach (var (text, w, left, bold, clr) in new (string, int, bool, bool, string)[]
                {
                    (district,          cols[0], true,  true,  NAVY),
                    (d.CrNo,            cols[1], false, false, BLACK),
                    (d.Date,            cols[2], false, false, BLACK),
                    (d.Time,            cols[3], false, false, BLACK),
                    (d.Route   ?? "–",  cols[4], false, false, BLACK),
                    (d.Location?? "–",  cols[5], true,  false, BLACK),
                    (d.Count.ToString(),cols[6], false, true,  NAVY),
                })
                {
                    string cellBg = bold && left ? LIGHT_BLUE : bg;
                    var tc = MakeCell(w, cellBg);
                    tc.AppendChild(DataPara(text, cellBg, bold, left, clr));
                    tr.AppendChild(tc);
                }
                t.Append(tr);
            }
        }
        return t;
    }

    // ── Age table ─────────────────────────────────────────────────────────────
    private Table BuildAgeTable(VictimDemographics v)
    {
        int cw = 2398;
        int[] widths = { cw, cw, cw, cw, cw, cw };
        var t = NewTable(widths);
        t.Append(SimpleHdrRow(widths, new[] { "TOTAL", "0–7", "8–12", "13–18", "19–35", "36+" }));
        var tr = new TableRow();
        foreach (var (val, w, i) in new[] { v.TotalFatalities, v.Age0to7, v.Age8to12, v.Age13to18, v.Age19to35, v.Age36Plus }
                                          .Select((x, i) => (x, widths[i], i)))
        {
            var tc = MakeCell(w, WHITE);
            tc.AppendChild(DataPara(val.ToString(), WHITE, i == 0, false, i == 0 ? NAVY : BLACK));
            tr.AppendChild(tc);
        }
        t.Append(tr);
        return t;
    }

    // ── Gender table ──────────────────────────────────────────────────────────
    private Table BuildGenderTable(VictimDemographics v)
    {
        int[] widths = { 5000, 3000, 3388 };
        var t = NewTable(widths);
        t.Append(SimpleHdrRow(widths, new[] { "VICTIMS GENDER", "M", "F" }));
        var rows = new (string L, int M, int F)[]
        {
            ("TOTAL",       v.MaleTotal,      v.FemaleTotal),
            ("DRIVER",      v.MaleDriver,     v.FemaleDriver),
            ("PASSENGER",   v.MalePassenger,  v.FemalePassenger),
            ("PEDESTRIANS", v.MalePedestrian, v.FemalePedestrian),
            ("CYCLIST",     v.MaleCyclist,    v.FemaleCyclist),
        };
        foreach (var (row, i) in rows.Select((r, i) => (r, i)))
        {
            string bg = i % 2 == 1 ? ALT_ROW : WHITE;
            var tr = new TableRow();
            var lc = MakeCell(widths[0], LIGHT_BLUE);
            lc.AppendChild(DataPara(row.L, LIGHT_BLUE, true, true));
            tr.AppendChild(lc);
            var mc = MakeCell(widths[1], bg);
            mc.AppendChild(DataPara(row.M.ToString(), bg, i == 0, false, i == 0 ? NAVY : BLACK));
            tr.AppendChild(mc);
            var fc = MakeCell(widths[2], bg);
            fc.AppendChild(DataPara(row.F.ToString(), bg, false, false));
            tr.AppendChild(fc);
            t.Append(tr);
        }
        return t;
    }

    // =========================================================================
    // LOW-LEVEL OPENXML HELPERS
    // =========================================================================
    private static Table NewTable(int[] colWidths)
    {
        var bv = new EnumValue<BorderValues>(BorderValues.Single);
        const string BC = "C8D4E8"; const uint SZ = 4;
        var borders = new TableBorders(
            new TopBorder { Val = bv, Size = SZ, Color = BC },
            new BottomBorder { Val = bv, Size = SZ, Color = BC },
            new LeftBorder { Val = bv, Size = SZ, Color = BC },
            new RightBorder { Val = bv, Size = SZ, Color = BC },
            new InsideHorizontalBorder { Val = bv, Size = SZ, Color = BC },
            new InsideVerticalBorder { Val = bv, Size = SZ, Color = BC });

        var t = new Table();
        t.AppendChild(new TableProperties(
            new TableWidth { Width = colWidths.Sum().ToString(), Type = TableWidthUnitValues.Dxa },
            new TableLayout { Type = TableLayoutValues.Fixed },
            borders));
        t.AppendChild(new TableGrid(
            colWidths.Select(w => new GridColumn { Width = w.ToString() })
                     .Cast<OpenXmlElement>().ToArray()));
        return t;
    }

    private static TableRow SimpleHdrRow(int[] widths, string[] texts)
    {
        var tr = new TableRow();
        var rowProps = new TableRowProperties();
        rowProps.AppendChild(new TableRowHeight { Val = 400 });
        tr.AppendChild(rowProps);
        for (int i = 0; i < texts.Length; i++)
        {
            var tc = MakeCell(widths[i], NAVY);
            tc.AppendChild(HdrPara(texts[i], NAVY, PT8));
            tr.AppendChild(tc);
        }
        return tr;
    }

    private static TableCell YearCell(string year, int width)
    {
        var tc = MakeCell(width, NAVY2);
        var rpr = new RunProperties(
            new RunFonts { Ascii = "Arial", HighAnsi = "Arial" },
            new FontSize { Val = PT7 },
            new Color { Val = "BBCCEE" });
        tc.AppendChild(new Paragraph(
            new ParagraphProperties(
                new Justification { Val = JustificationValues.Center },
                new SpacingBetweenLines { Before = "0", After = "0" }),
            new Run(rpr, new Text(year))));
        return tc;
    }

    private static Paragraph HdrPara(string text, string bgColor, string size)
    {
        var rpr = new RunProperties(
            new RunFonts { Ascii = "Arial", HighAnsi = "Arial" },
            new Bold(),
            new FontSize { Val = size },
            new Color { Val = WHITE });
        return new Paragraph(
            new ParagraphProperties(
                new Justification { Val = JustificationValues.Center },
                new SpacingBetweenLines { Before = "0", After = "0" }),
            new Run(rpr, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static Paragraph DataPara(string text, string bg, bool bold, bool left,
                                       string color = BLACK)
    {
        var rpr = new RunProperties(
            new RunFonts { Ascii = "Arial", HighAnsi = "Arial" },
            new FontSize { Val = PT9 },
            new Color { Val = color });
        if (bold) rpr.AppendChild(new Bold());
        return new Paragraph(
            new ParagraphProperties(
                new Justification { Val = left ? JustificationValues.Left : JustificationValues.Center },
                new SpacingBetweenLines { Before = "0", After = "0" }),
            new Run(rpr, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static TableCell MakeCell(int width, string fill)
    {
        var tcp = new TableCellProperties(
            new TableCellWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa },
            new Shading { Fill = fill, Color = "auto", Val = ShadingPatternValues.Clear },
            new TableCellMargin(
                new TopMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                new BottomMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                new LeftMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                new RightMargin { Width = "100", Type = TableWidthUnitValues.Dxa }));
        var tc = new TableCell();
        tc.AppendChild(tcp);
        return tc;
    }

    // ─────────────────────────────────────────────────────────────────────────
    private static Paragraph StyledPara(string size, bool bold, JustificationValues just,
                                         uint spacingAfter, string text)
    {
        var rpr = new RunProperties(
            new RunFonts { Ascii = "Arial", HighAnsi = "Arial" },
            new FontSize { Val = size },
            new Color { Val = BLACK });
        if (bold) rpr.AppendChild(new Bold());
        return new Paragraph(
            new ParagraphProperties(
                new Justification { Val = just },
                new SpacingBetweenLines { Before = "0", After = spacingAfter.ToString() }),
            new Run(rpr, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static Paragraph IndentedPara(string text)
    {
        var rpr = new RunProperties(
            new RunFonts { Ascii = "Arial", HighAnsi = "Arial" },
            new FontSize { Val = PT9 },
            new Color { Val = BLACK });
        return new Paragraph(
            new ParagraphProperties(
                new Indentation { Left = "360" },
                new SpacingBetweenLines { Before = "0", After = "60" }),
            new Run(rpr, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static Paragraph BlankLine() =>
        new(new ParagraphProperties(new SpacingBetweenLines { Before = "0", After = "120" }));

    private static void ApplyPageLayout(MainDocumentPart main)
    {
        var body = main.Document.Body!;
        var secPr = body.Elements<SectionProperties>().FirstOrDefault()
                    ?? body.AppendChild(new SectionProperties());
        secPr.PrependChild(new PageMargin { Top = 720, Bottom = 720, Left = 720, Right = 720 });
        secPr.PrependChild(new PageSize { Width = 16838, Height = 11906, Orient = PageOrientationValues.Landscape });
    }
}