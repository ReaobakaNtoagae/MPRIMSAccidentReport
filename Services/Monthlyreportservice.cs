using ClosedXML.Excel;
using CrashReport.Data;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Services;

public class ReportRow
{
    public string SapsStation { get; set; } = string.Empty;
    public string? ArNo { get; set; }
    public string? CasNo { get; set; }
    public string Date { get; set; } = string.Empty;
    public string Day { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string? Route { get; set; }
    public string? Location { get; set; }
    public string? CrashType { get; set; }
    // Fatal
    public int FatalDriver { get; set; }
    public int FatalPassenger { get; set; }
    public int FatalPedestrian { get; set; }
    public int FatalCyclist { get; set; }
    // Gender
    public int GenderMale { get; set; }
    public int GenderFemale { get; set; }
    // Serious
    public int SeriousDriver { get; set; }
    public int SeriousPassenger { get; set; }
    public int SeriousPedestrian { get; set; }
    public int SeriousCyclist { get; set; }
    // Slight
    public int SlightDriver { get; set; }
    public int SlightPassenger { get; set; }
    public int SlightPedestrian { get; set; }
    public int SlightCyclist { get; set; }
    // Vehicles
    public string? VehiclesInvolved { get; set; }
}

public class MonthlyReportService
{
    private readonly AppDbContext _context;

    // ── Colour constants (ARGB) ───────────────────────────────
    private const string YELLOW = "FFFFFF00";
    private const string LT_GREY = "FFD9D9D9";
    private const string LT_BLUE = "FFDCE6F1";
    private const string BLACK = "FF000000";

    // ── Day abbreviations ─────────────────────────────────────
    private static readonly string[] DayAbbr =
        { "SU", "M", "TU", "WE", "TH", "FR", "SA" };

    public MonthlyReportService(AppDbContext context)
    {
        _context = context;
    }

    // ── Main entry point ─────────────────────────────────────
    public async Task<byte[]> GenerateAsync(int year, int month, string district = "EHLANZENI")
    {
        var rows = await BuildReportRowsAsync(year, month);

        using var wb = new XLWorkbook();
        var monthName = new DateTime(year, month, 1).ToString("MMMM yyyy").ToUpper();
        var ws = wb.AddWorksheet($"{new DateTime(year, month, 1):MMMM yyyy} CAS".ToUpper());

        // ── Column widths ─────────────────────────────────────
        ws.Column(1).Width = 14;   // A SAPS
        ws.Column(2).Width = 5;    // B AR NO
        ws.Column(3).Width = 10;   // C CAS
        ws.Column(4).Width = 6;    // D DATE
        ws.Column(5).Width = 4.5;  // E DAY
        ws.Column(6).Width = 6;    // F TIME
        ws.Column(7).Width = 6;    // G ROUTE
        ws.Column(8).Width = 22;   // H LOCATION
        ws.Column(9).Width = 13;   // I TYPE
        for (int c = 10; c <= 23; c++) ws.Column(c).Width = 4;
        ws.Column(24).Width = 17;   // X VEHICLES

        // ── Row heights ───────────────────────────────────────
        ws.Row(1).Height = 14;
        ws.Row(2).Height = 5;
        ws.Row(3).Height = 14;
        ws.Row(4).Height = 14;
        ws.Row(5).Height = 14;
        ws.Row(6).Height = 14;

        // ── Row 1: Report title ───────────────────────────────
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var r1a = ws.Cell(1, 1);
        r1a.Value = "CONSOLIDATED";
        r1a.Style.Font.Bold = true; r1a.Style.Font.FontSize = 10; r1a.Style.Font.FontName = "Arial";
        ws.Range("A1:D1").Merge();

        var r1f = ws.Cell(1, 6);
        r1f.Value = $"ACCIDENT REPORT:   01 - {daysInMonth} {monthName}";
        r1f.Style.Font.Bold = true; r1f.Style.Font.FontSize = 10; r1f.Style.Font.FontName = "Arial";
        r1f.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range("F1:K1").Merge();

        var r1r = ws.Cell(1, 18);
        r1r.Value = "REPORTED ACCIDENTS";
        r1r.Style.Font.Bold = true; r1r.Style.Font.FontSize = 10; r1r.Style.Font.FontName = "Arial";
        r1r.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range("R1:X1").Merge();

        // ── Row 3: District ───────────────────────────────────
        var r3 = ws.Cell(3, 1);
        r3.Value = district.ToUpper();
        r3.Style.Font.Bold = true; r3.Style.Font.FontSize = 10; r3.Style.Font.FontName = "Arial";
        ws.Range("A3:B3").Merge();

        // ── Row 4: Sub-heading ────────────────────────────────
        var r4a = ws.Cell(4, 1);
        r4a.Value = "CONSOLIDATED REPORT";
        r4a.Style.Font.Bold = true; r4a.Style.Font.FontSize = 8; r4a.Style.Font.FontName = "Arial";
        ws.Range("A4:F4").Merge();

        var r4j = ws.Cell(4, 10);
        r4j.Value = "NATURE OF INJURIES";
        r4j.Style.Font.Bold = true; r4j.Style.Font.FontSize = 8; r4j.Style.Font.FontName = "Arial";

        // ── Row 5: Group headers ──────────────────────────────
        SetGroupHeader(ws, 5, 9, 9, "ACCIDENT", YELLOW);
        SetGroupHeader(ws, 5, 10, 13, "FATAL", LT_GREY);
        SetGroupHeader(ws, 5, 14, 15, "GENDER", LT_GREY);
        SetGroupHeader(ws, 5, 16, 19, "SERIOUS", LT_GREY);
        SetGroupHeader(ws, 5, 20, 23, "SLIGHT", LT_GREY);
        SetGroupHeader(ws, 5, 24, 24, "VEHICLES", YELLOW);

        // ── Row 6: Column headers ─────────────────────────────
        string[] hdrs = {
            "SAPS","AR NO","CAS","DATE","DAY","TIME","ROUTE","LOCATION","TYPE",
            "D","P","PD","C",
            "M","F",
            "D","P","PD","C",
            "D","P","PD","C",
            "INVOLVED"
        };
        for (int c = 1; c <= hdrs.Length; c++)
        {
            var cell = ws.Cell(6, c);
            cell.Value = hdrs[c - 1];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontSize = 8;
            cell.Style.Font.FontName = "Arial";
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        // ── Data rows ─────────────────────────────────────────
        int dataStart = 7;
        int rowIdx = dataStart;

        foreach (var row in rows)
        {
            bool altRow = (rowIdx % 2 == 0);
            WriteDataRow(ws, rowIdx, row, altRow);
            rowIdx++;
        }

        int lastDataRow = rowIdx - 1;

        // ── Blank separator ───────────────────────────────────
        rowIdx++;

        // ── Grand Total row ───────────────────────────────────
        int totalRow = rowIdx;
        var totCell = ws.Cell(totalRow, 1);
        totCell.Value = $"TOTAL:     {rows.Count}";
        totCell.Style.Font.Bold = true;
        totCell.Style.Font.FontSize = 8;
        totCell.Style.Font.FontName = "Arial";
        totCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");

        var gtCell = ws.Cell(totalRow, 8);
        gtCell.Value = "GRAND TOTAL";
        gtCell.Style.Font.Bold = true;
        gtCell.Style.Font.FontSize = 8;
        gtCell.Style.Font.FontName = "Arial";
        gtCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");
        gtCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // SUM formulas for injury columns J-W
        for (int c = 10; c <= 23; c++)
        {
            var colLetter = ((char)('A' + c - 1)).ToString();
            var tc = ws.Cell(totalRow, c);
            tc.FormulaA1 = $"SUM({colLetter}{dataStart}:{colLetter}{lastDataRow})";
            tc.Style.Font.Bold = true;
            tc.Style.Font.FontSize = 8;
            tc.Style.Font.FontName = "Arial";
            tc.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");
            tc.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // ── Victims summary section ───────────────────────────
        int summaryStart = totalRow + 3;
        WriteSummarySection(ws, summaryStart, rows, dataStart, lastDataRow);

        // ── Save to memory stream ─────────────────────────────
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ── Build report rows from database ──────────────────────
    private async Task<List<ReportRow>> BuildReportRowsAsync(int year, int month)
    {
        var crashes = await _context.Crashes
            .Include(c => c.CrashLocations)
            .Include(c => c.CrashConditions)
            .Include(c => c.CrashVehicles)
                .ThenInclude(cv => cv.Vehicle)
            .Include(c => c.CrashPeople)
            .Where(c => c.CrashDate.Year == year && c.CrashDate.Month == month)
            .OrderBy(c => c.CrashDate)
            .ThenBy(c => c.CrashTime)
            .ToListAsync();

        var rows = new List<ReportRow>();

        foreach (var crash in crashes)
        {
            var loc = crash.CrashLocations.FirstOrDefault();
            var cond = crash.CrashConditions.FirstOrDefault();
            var people = crash.CrashPeople.ToList();
            var vehicles = crash.CrashVehicles.ToList();


            // ── Extract SAPS station from CrNo (format: STATION-ARNO)
            string saps = string.Empty;
            string arNo = crash.CrNo ?? string.Empty;
            if (crash.CrNo?.Contains("-") == true)
            {
                var parts = crash.CrNo.Split('-', 2);
                saps = parts[0].Trim();
                arNo = parts[1].Trim();
            }
            else if (!string.IsNullOrEmpty(crash.CrNo))
            {
                saps = crash.CrNo;
            }

            // ── Build vehicle list string
            var vehicleTypes = vehicles
                .Where(v => v.Vehicle != null)
                .Select(v => $"{v.Vehicle!.VehicleCategory ?? v.Vehicle.Make ?? "UNK"}")
                .ToList();
            string vehicleStr = vehicleTypes.Any()
                ? string.Join(" / ", vehicleTypes)
                : crash.BriefDescription?.Contains("Vehicles:") == true
                    ? crash.BriefDescription.Split("Vehicles:").Last().Trim().Split('.')[0].Trim()
                    : string.Empty;

            // ── Count injuries by role and severity
            int CountBy(string role, string severity) =>
                people.Count(p =>
                    (string.IsNullOrEmpty(role) || p.Role == role) &&
                    (string.IsNullOrEmpty(severity) || p.SeverityOfInjury == severity));

            var row = new ReportRow
            {
                SapsStation = saps,
                ArNo = arNo,
                CasNo = crash.CasNo,
                Date = crash.CrashDate.ToString("dd/MM"),
                Day = DayAbbr[(int)crash.CrashDate.DayOfWeek],
                Time = crash.CrashTime.HasValue
                                    ? crash.CrashTime.Value.ToString("HH") + "H" +
                                      crash.CrashTime.Value.ToString("mm")
                                    : string.Empty,
                Route = crash.RoadNumber,
                Location = loc?.StreetRoadName ?? loc?.CityTown,
                CrashType = cond?.CrashType?.ToUpper(),
                FatalDriver = CountBy("Driver", "Fatal"),
                FatalPassenger = CountBy("Passenger", "Fatal"),
                FatalPedestrian = CountBy("Pedestrian", "Fatal"),
                FatalCyclist = CountBy("Bicyclist", "Fatal"),
                GenderMale = people.Count(p => p.Person?.Gender == "Male"),
                GenderFemale = people.Count(p => p.Person?.Gender == "Female"),
                SeriousDriver = CountBy("Driver", "Serious"),
                SeriousPassenger = CountBy("Passenger", "Serious"),
                SeriousPedestrian = CountBy("Pedestrian", "Serious"),
                SeriousCyclist = CountBy("Bicyclist", "Serious"),
                SlightDriver = CountBy("Driver", "Slight"),
                SlightPassenger = CountBy("Passenger", "Slight"),
                SlightPedestrian = CountBy("Pedestrian", "Slight"),
                SlightCyclist = CountBy("Bicyclist", "Slight"),
                VehiclesInvolved = vehicleStr
            };

            rows.Add(row);
        }

        return rows;
    }

    // ── Write a single data row ───────────────────────────────
    private void WriteDataRow(IXLWorksheet ws, int r, ReportRow row, bool alt)
    {
        var bg = alt ? XLColor.FromHtml("#DCE6F1") : XLColor.NoColor;

        void W(int col, object? val, bool bold = false, bool centre = false)
        {
            var c = ws.Cell(r, col);
            if (val is int iv && iv == 0) c.Value = Blank.Value;
            else if (val != null) c.Value = XLCellValue.FromObject(val);
            c.Style.Font.Bold = bold;
            c.Style.Font.FontSize = 8;
            c.Style.Font.FontName = "Arial";
            if (alt) c.Style.Fill.BackgroundColor = bg;
            if (centre)
                c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        W(1, row.SapsStation);
        W(2, row.ArNo, false, true);
        W(3, row.CasNo, false, true);
        W(4, row.Date, false, true);
        W(5, row.Day, false, true);
        W(6, row.Time, false, true);
        W(7, row.Route);
        W(8, row.Location);
        W(9, row.CrashType);
        // Fatal
        W(10, row.FatalDriver, true, true);
        W(11, row.FatalPassenger, true, true);
        W(12, row.FatalPedestrian, true, true);
        W(13, row.FatalCyclist, true, true);
        // Gender
        W(14, row.GenderMale, true, true);
        W(15, row.GenderFemale, true, true);
        // Serious
        W(16, row.SeriousDriver, true, true);
        W(17, row.SeriousPassenger, true, true);
        W(18, row.SeriousPedestrian, true, true);
        W(19, row.SeriousCyclist, true, true);
        // Slight
        W(20, row.SlightDriver, true, true);
        W(21, row.SlightPassenger, true, true);
        W(22, row.SlightPedestrian, true, true);
        W(23, row.SlightCyclist, true, true);
        // Vehicles
        W(24, row.VehiclesInvolved);
    }

    // ── Write victims summary section ─────────────────────────
    private void WriteSummarySection(IXLWorksheet ws, int startRow,
        List<ReportRow> rows, int dataStart, int lastDataRow)
    {
        void Hdr(int r, int c, string val)
        {
            var cell = ws.Cell(r, c);
            cell.Value = val;
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontSize = 8;
            cell.Style.Font.FontName = "Arial";
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");
        }

        // VICTIMS header
        Hdr(startRow, 1, "VICTIMS");

        // Age groups
        Hdr(startRow + 1, 1, "AGE");
        string[] ageGroups = { "0 - 7", "08-12", "13-18", "19-35", "36+" };
        for (int i = 0; i < ageGroups.Length; i++)
            Hdr(startRow + 1, 2 + i, ageGroups[i]);

        // Age data row (empty — no age data captured)
        ws.Cell(startRow + 2, 1).Value = "";

        // Age TOTAL formula
        var totalCell = ws.Cell(startRow + 3, 1);
        totalCell.Value = "TOTAL";
        totalCell.Style.Font.Bold = true;
        totalCell.Style.Font.FontSize = 8;
        totalCell.Style.Font.FontName = "Arial";
        var totSum = ws.Cell(startRow + 3, 2);
        totSum.FormulaA1 = $"SUM(B{startRow + 2}:F{startRow + 2})";
        totSum.Style.Font.Bold = true;
        totSum.Style.Font.FontSize = 8;

        // Victim gender
        int gRow = startRow + 5;
        Hdr(gRow, 1, "VICTIM GENDER");
        Hdr(gRow, 2, "M");
        Hdr(gRow, 3, "F");

        string[] victimTypes = { "DRIVER", "PASSENGER", "PEDESTRIAN", "CYCLIST" };
        for (int i = 0; i < victimTypes.Length; i++)
        {
            int vr = gRow + 1 + i;
            Hdr(vr, 1, victimTypes[i]);
           
            ws.Cell(vr, 2).Value = CountGender(rows, victimTypes[i], "Male");
            ws.Cell(vr, 2).Style.Font.FontSize = 8;
            // Females
            ws.Cell(vr, 3).Value = CountGender(rows, victimTypes[i], "Female");
            ws.Cell(vr, 3).Style.Font.FontSize = 8;
        }

        int gTotalRow = gRow + victimTypes.Length + 1;
        var gtCell = ws.Cell(gTotalRow, 1);
        gtCell.Value = "TOTAL";
        gtCell.Style.Font.Bold = true;
        gtCell.Style.Font.FontSize = 8;

        var gTotM = ws.Cell(gTotalRow, 2);
        gTotM.FormulaA1 = $"SUM(B{gRow + 1}:B{gRow + victimTypes.Length})";
        gTotM.Style.Font.Bold = true;
        gTotM.Style.Font.FontSize = 8;

        var gTotF = ws.Cell(gTotalRow, 3);
        gTotF.FormulaA1 = $"SUM(C{gRow + 1}:C{gRow + victimTypes.Length})";
        gTotF.Style.Font.Bold = true;
        gTotF.Style.Font.FontSize = 8;

        int grandRow = gTotalRow + 1;
        var grCell = ws.Cell(grandRow, 1);
        grCell.Value = "GRAND TOTAL";
        grCell.Style.Font.Bold = true;
        grCell.Style.Font.FontSize = 8;

        var grTot = ws.Cell(grandRow, 2);
        grTot.FormulaA1 = $"B{gTotalRow}+C{gTotalRow}";
        grTot.Style.Font.Bold = true;
        grTot.Style.Font.FontSize = 8;
    }

    private static int CountGender(List<ReportRow> rows, string victimType, string gender)
    {
        
        return 0;
    }

    
    private void SetGroupHeader(IXLWorksheet ws, int row, int colFrom, int colTo,
    string text, string colorHex)
    {
        var cell = ws.Cell(row, colFrom);
        cell.Value = text;
        cell.Style.Font.Bold = true;
        cell.Style.Font.FontSize = 8;
        cell.Style.Font.FontName = "Arial";

        
        var rgb = colorHex.Length == 8 ? colorHex.Substring(2) : colorHex;
        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#" + rgb);

        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        if (colFrom < colTo)
            ws.Range(row, colFrom, row, colTo).Merge();
    }
}