using ClosedXML.Excel;
using CrashReport.Data;
using CrashReport.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CrashReport.Services;


public class ExcelImportService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ExcelImportService> _logger;

    public ExcelImportService(AppDbContext context, ILogger<ExcelImportService> logger)
    {
        _context = context;
        _logger = logger;
    }


    private class ColumnMap
    {
        public int Saps, ArNo, Cas, Date, Day, Time, Route, Location, Type, Involved;
        public int FatalD, FatalP, FatalPd, FatalC, GenderM, GenderF;
        public int SeriousD, SeriousP, SeriousPd, SeriousC;
        public int SlightD, SlightP, SlightPd, SlightC;

        public List<string> MissingCriticalFields()
        {
            var missing = new List<string>();
            if (Saps == 0) missing.Add("SAPS");
            if (Date == 0) missing.Add("DATE");
            if (Type == 0) missing.Add("TYPE");
            if (Involved == 0) missing.Add("INVOLVED (vehicles)");
            return missing;
        }
    }

    private static string NormalizeHeader(string raw) =>
        (raw ?? "").Trim().ToUpperInvariant()
                   .Replace(" ", "").Replace("-", "").Replace(".", "").Replace("_", "");

    private (int HeaderRowNumber, ColumnMap Map)? BuildColumnMap(IXLWorksheet ws)
    {
        var rows = ws.RowsUsed().ToList();

        int subHeaderIdx = -1;
        const int scanCols = 60;

        for (int i = 0; i < rows.Count && subHeaderIdx == -1; i++)
        {
            var row = rows[i];
            for (int c = 1; c <= scanCols; c++)
            {
                if (NormalizeHeader(row.Cell(c).GetString()) != "SAPS") continue;

                for (int c2 = c + 1; c2 <= c + 5; c2++)
                {
                    if (NormalizeHeader(row.Cell(c2).GetString()) == "ARNO")
                    {
                        subHeaderIdx = i;
                        break;
                    }
                }
                if (subHeaderIdx != -1) break;
            }
        }

        if (subHeaderIdx == -1)
            return null;

        var headerRow = rows[subHeaderIdx];
        var groupRow = subHeaderIdx > 0 ? rows[subHeaderIdx - 1] : null;
        var map = new ColumnMap();

        for (int c = 1; c <= scanCols; c++)
        {
            switch (NormalizeHeader(headerRow.Cell(c).GetString()))
            {
                case "SAPS": map.Saps = c; break;
                case "ARNO": map.ArNo = c; break;
                case "CAS": map.Cas = c; break;
                case "DATE": map.Date = c; break;
                case "DAY": map.Day = c; break;
                case "TIME": map.Time = c; break;
                case "ROUTE": map.Route = c; break;
                case "LOCATION": map.Location = c; break;
                case "TYPE": map.Type = c; break;
                case "INVOLVED": map.Involved = c; break;
            }
        }

        if (groupRow != null)
        {
            var groups = new List<(string Group, int StartCol)>();
            for (int c = 1; c <= scanCols; c++)
            {
                var g = NormalizeHeader(groupRow.Cell(c).GetString());
                if (g is "FATAL" or "GENDER" or "SERIOUS" or "SLIGHT")
                    groups.Add((g, c));
            }
            groups.Add(("__END__", scanCols + 1));

            for (int gi = 0; gi < groups.Count - 1; gi++)
            {
                var (group, startCol) = groups[gi];
                var endCol = groups[gi + 1].StartCol;

                for (int c = startCol; c < endCol; c++)
                {
                    var sub = NormalizeHeader(headerRow.Cell(c).GetString());
                    switch (group)
                    {
                        case "FATAL":
                            if (sub == "D") map.FatalD = c;
                            else if (sub == "P") map.FatalP = c;
                            else if (sub == "PD") map.FatalPd = c;
                            else if (sub == "C") map.FatalC = c;
                            break;
                        case "GENDER":
                            if (sub == "M") map.GenderM = c;
                            else if (sub == "F") map.GenderF = c;
                            break;
                        case "SERIOUS":
                            if (sub == "D") map.SeriousD = c;
                            else if (sub == "P") map.SeriousP = c;
                            else if (sub == "PD") map.SeriousPd = c;
                            else if (sub == "C") map.SeriousC = c;
                            break;
                        case "SLIGHT":
                            if (sub == "D") map.SlightD = c;
                            else if (sub == "P") map.SlightP = c;
                            else if (sub == "PD") map.SlightPd = c;
                            else if (sub == "C") map.SlightC = c;
                            break;
                    }
                }
            }
        }

        return (headerRow.RowNumber(), map);
    }


    private class ReportHeaderInfo
    {
        public DateOnly? PeriodFrom { get; set; }
        public DateOnly? PeriodTo { get; set; }
        public string? District { get; set; }
        public bool IsProvincial { get; set; }
    }

    private static readonly Dictionary<string, int> MonthNameToNumber =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["JAN"] = 1,
            ["JANUARY"] = 1,
            ["FEB"] = 2,
            ["FEBRUARY"] = 2,
            ["MAR"] = 3,
            ["MARCH"] = 3,
            ["APR"] = 4,
            ["APRIL"] = 4,
            ["MAY"] = 5,
            ["JUN"] = 6,
            ["JUNE"] = 6,
            ["JUL"] = 7,
            ["JULY"] = 7,
            ["AUG"] = 8,
            ["AUGUST"] = 8,
            ["SEP"] = 9,
            ["SEPT"] = 9,
            ["SEPTEMBER"] = 9,
            ["OCT"] = 10,
            ["OCTOBER"] = 10,
            ["NOV"] = 11,
            ["NOVEMBER"] = 11,
            ["DEC"] = 12,
            ["DECEMBER"] = 12,
        };

    private static readonly string[] KnownDistricts =
    {
        "EHLANZENI SOUTH", "EHLANZENI NORTH", "EHLANZENI",
        "GERT SIBANDE", "NKANGALA", "BOHLABELA"
    };

    private static readonly HashSet<string> ProvincialLabels =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "PROVINCIAL", "MPUMALANGA", "CONSOLIDATED", "CONSOLIDATED REPORT"
        };

    private static readonly Regex PeriodPattern = new(
        @"(\d{1,2})\s*-\s*(\d{1,2})\s+([A-Za-z]+)\s+(\d{4})",
        RegexOptions.Compiled);

    private ReportHeaderInfo ExtractReportHeaderInfo(IXLWorksheet ws)
    {
        var info = new ReportHeaderInfo();
        var rows = ws.RowsUsed().Take(8).ToList();

        foreach (var row in rows)
        {
            for (int c = 1; c <= 30; c++)
            {
                var text = row.Cell(c).GetString();
                if (string.IsNullOrWhiteSpace(text)) continue;

                if (info.PeriodFrom == null)
                {
                    var m = PeriodPattern.Match(text);
                    if (m.Success && MonthNameToNumber.TryGetValue(m.Groups[3].Value, out var monthNum))
                    {
                        if (int.TryParse(m.Groups[1].Value, out var dayFrom) &&
                            int.TryParse(m.Groups[2].Value, out var dayTo) &&
                            int.TryParse(m.Groups[4].Value, out var year))
                        {
                            try
                            {
                                var daysInMonth = DateTime.DaysInMonth(year, monthNum);
                                info.PeriodFrom = new DateOnly(year, monthNum, Math.Min(dayFrom, daysInMonth));
                                info.PeriodTo = new DateOnly(year, monthNum, Math.Min(dayTo, daysInMonth));
                            }
                            catch { }
                        }
                    }
                }

                if (info.District == null && !info.IsProvincial)
                {
                    var normalized = text.Trim().ToUpperInvariant();

                    if (ProvincialLabels.Contains(normalized))
                        info.IsProvincial = true;
                    else
                    {
                        var match = KnownDistricts.FirstOrDefault(d => normalized == d);
                        if (match != null)
                            info.District = match;
                    }
                }
            }
        }

        return info;
    }


    public async Task<ImportResult> ImportAsync(Stream stream, string fileName, string province = "MP")
    {
        var result = new ImportResult { FileName = fileName };

        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();

        var located = BuildColumnMap(ws);
        if (located == null)
        {
            result.AddError("Could not find header row with SAPS/AR NO/CAS columns.");
            return result;
        }

        var (headerRowNumber, map) = located.Value;

        var missing = map.MissingCriticalFields();
        if (missing.Count > 0)
        {
            result.AddError(
                "Header row was found, but these required columns could not be located: " +
                string.Join(", ", missing) + ".");
            return result;
        }

        var headerInfo = ExtractReportHeaderInfo(ws);

        DateOnly periodFrom, periodTo;
        if (headerInfo.PeriodFrom.HasValue && headerInfo.PeriodTo.HasValue)
        {
            periodFrom = headerInfo.PeriodFrom.Value;
            periodTo = headerInfo.PeriodTo.Value;
        }
        else
        {
            (periodFrom, periodTo) = ExtractPeriodFromFileName(fileName);
            _logger.LogWarning(
                "No period found in worksheet header — falling back to filename: {From} to {To}",
                periodFrom, periodTo);
        }

        result.DetectedPeriodFrom = periodFrom;
        result.DetectedPeriodTo = periodTo;
        result.DetectedDistrict = headerInfo.District;
        result.DetectedIsProvincial = headerInfo.IsProvincial;

        var allRows = ws.RowsUsed().Skip(headerRowNumber).ToList();

        var dataRows = new List<IXLRow>();
        var summaryRows = new List<IXLRow>();
        bool inSummary = false;

        foreach (var row in allRows)
        {
            var saps = row.Cell(map.Saps).GetString().Trim();

            if (string.IsNullOrWhiteSpace(saps) || saps.Equals("TOTAL", StringComparison.OrdinalIgnoreCase))
            {
                if (inSummary) summaryRows.Add(row);
                else inSummary = true;
                continue;
            }

            var col7 = row.Cell(map.Location > 0 ? map.Location : 8).GetString().Trim();

            if (col7.Equals("GRAND TOTAL", StringComparison.OrdinalIgnoreCase) ||
                saps.StartsWith("TOTAL:", StringComparison.OrdinalIgnoreCase))
            {
                inSummary = true;
                continue;
            }

            if (!inSummary)
            {
                if (saps.StartsWith("VICTIMS", StringComparison.OrdinalIgnoreCase) ||
                    saps.StartsWith("AGE", StringComparison.OrdinalIgnoreCase) ||
                    saps.StartsWith("RACE", StringComparison.OrdinalIgnoreCase) ||
                    saps.StartsWith("DRIVER", StringComparison.OrdinalIgnoreCase) ||
                    saps.StartsWith("PASSENGER", StringComparison.OrdinalIgnoreCase) ||
                    saps.StartsWith("PEDESTRIAN", StringComparison.OrdinalIgnoreCase) ||
                    saps.StartsWith("CYLIST", StringComparison.OrdinalIgnoreCase))
                {
                    inSummary = true;
                    summaryRows.Add(row);
                    continue;
                }

                dataRows.Add(row);
            }
            else
            {
                summaryRows.Add(row);
            }
        }

   
        result.Demographics = ParseDemographics(summaryRows, ws);
        await SaveDemographicsAsync(result.Demographics, periodFrom, periodTo, province);

        var existingSummaryCrNos = await _context.CrashSummaries
            .Select(s => s.CrNo)
            .ToHashSetAsync();


        var existingFormCrNos = await _context.Crashes
            .Where(c => c.CrNo != null)
            .Select(c => c.CrNo!)
            .ToHashSetAsync();

        foreach (var row in dataRows)
        {
            result.TotalRows++;
            try
            {
                var summary = ParseSummaryRow(row, map, fileName, periodFrom.Year);
                if (summary == null)
                {
                    result.Skipped++;
                    result.AddWarning($"Row {row.RowNumber()}: could not parse — skipped.");
                    continue;
                }

                if (existingSummaryCrNos.Contains(summary.CrNo))
                {
                    result.Skipped++;
                    result.AddWarning($"Row {row.RowNumber()}: CrNo '{summary.CrNo}' already imported — skipped.");
                    continue;
                }

                if (existingFormCrNos.Contains(summary.CrNo))
                {
                    result.Skipped++;
                    result.AddWarning(
                        $"Row {row.RowNumber()}: CrNo '{summary.CrNo}' already exists as a full CR1 " +
                        $"form capture — skipped (the form record takes precedence in reports).");
                    continue;
                }

                if (summary.CrashDate < periodFrom || summary.CrashDate > periodTo)
                {
                    result.AddWarning(
                        $"Row {row.RowNumber()}: crash date {summary.CrashDate:dd/MM/yyyy} falls outside " +
                        $"the declared report period ({periodFrom:dd/MM/yyyy} – {periodTo:dd/MM/yyyy}). " +
                        $"Imported anyway — please verify the DATE column on this row.");
                }

                _context.CrashSummaries.Add(summary);
                existingSummaryCrNos.Add(summary.CrNo);
                result.Imported++;
            }
            catch (Exception ex)
            {
                result.Errors++;
                result.AddError($"Row {row.RowNumber()}: {ex.Message}");
                _logger.LogWarning(ex, "Import error on row {row}", row.RowNumber());
            }
        }

        await _context.SaveChangesAsync();
        return result;
    }

  
    private CrashSummary? ParseSummaryRow(IXLRow row, ColumnMap map, string fileName, int fallbackYear)
    {
        var saps = row.Cell(map.Saps).GetString().Trim().ToUpper();
        if (string.IsNullOrEmpty(saps)) return null;

        var arNo = map.ArNo > 0 ? row.Cell(map.ArNo).GetString().Trim() : "";
        var casNo = map.Cas > 0 ? row.Cell(map.Cas).GetString().Trim() : "";
        var dateRaw = row.Cell(map.Date).GetString().Trim();
        var timeRaw = map.Time > 0 ? row.Cell(map.Time).GetString().Trim() : "";
        var route = map.Route > 0 ? row.Cell(map.Route).GetString().Trim().ToUpper() : "";
        var location = map.Location > 0 ? row.Cell(map.Location).GetString().Trim() : "";
        var crashType = row.Cell(map.Type).GetString().Trim().ToUpper();
        var vehicles = row.Cell(map.Involved).GetString().Trim();

        
        DateOnly crashDate = new DateOnly(fallbackYear, 1, 1);
        if (!string.IsNullOrEmpty(dateRaw))
        {
            var parts = dateRaw.Replace('-', '/').Split('/');
            if (parts.Length >= 2 &&
                int.TryParse(parts[0], out var dd) && int.TryParse(parts[1], out var mm))
            {
                var year = fallbackYear;
                if (parts.Length >= 3 && int.TryParse(parts[2], out var yyyy))
                    year = yyyy;

                if (mm >= 1 && mm <= 12)
                {
                    dd = Math.Min(Math.Max(dd, 1), DateTime.DaysInMonth(year, mm));
                    crashDate = new DateOnly(year, mm, dd);
                }
            }
        }

        TimeOnly? crashTime = null;
        if (!string.IsNullOrEmpty(timeRaw))
        {
            var norm = timeRaw.ToUpper().Replace("H", ":");
            if (TimeOnly.TryParse(norm, out var t)) crashTime = t;
        }


        var vehicleCount = vehicles
            .Split('/')
            .Select(p => p.Trim())
            .Count(p => !string.IsNullOrWhiteSpace(p)
                        && !p.Equals("P/D", StringComparison.OrdinalIgnoreCase)
                        && !p.Equals("HIT N RUN", StringComparison.OrdinalIgnoreCase)
                        && !p.Equals("HIT & RUN", StringComparison.OrdinalIgnoreCase));

        return new CrashSummary
        {
            CrNo = string.IsNullOrEmpty(arNo) ? saps : $"{saps}-{arNo}",
            Station = saps,
            CasNo = string.IsNullOrEmpty(casNo) ? null : casNo,
            CrashDate = crashDate,
            CrashTime = crashTime,
            Route = string.IsNullOrEmpty(route) ? null : route,
            Location = string.IsNullOrEmpty(location) ? null : location,
            CrashType = string.IsNullOrEmpty(crashType) ? null : crashType,
            VehiclesString = string.IsNullOrEmpty(vehicles) ? null : vehicles,
            VehicleCount = (byte)Math.Min(vehicleCount, 255),

            FatalDrivers = ByteCell(row, map.FatalD),
            FatalPassengers = ByteCell(row, map.FatalP),
            FatalPedestrians = ByteCell(row, map.FatalPd),
            FatalCyclists = ByteCell(row, map.FatalC),
            FatalMale = ByteCell(row, map.GenderM),
            FatalFemale = ByteCell(row, map.GenderF),

            SeriousDrivers = ByteCell(row, map.SeriousD),
            SeriousPassengers = ByteCell(row, map.SeriousP),
            SeriousPedestrians = ByteCell(row, map.SeriousPd),
            SeriousCyclists = ByteCell(row, map.SeriousC),

            SlightDrivers = ByteCell(row, map.SlightD),
            SlightPassengers = ByteCell(row, map.SlightP),
            SlightPedestrians = ByteCell(row, map.SlightPd),
            SlightCyclists = ByteCell(row, map.SlightC),

            SourceFile = fileName,
            ImportedAt = DateTime.UtcNow
        };
    }



    private ImportDemographics ParseDemographics(List<IXLRow> summaryRows, IXLWorksheet ws)
    {
        var demo = new ImportDemographics();

        if (!summaryRows.Any())
        {
            _logger.LogWarning("No summary rows found to parse demographics");
            return demo;
        }

        var data = new List<string[]>();
        foreach (var row in summaryRows)
        {
            var cells = new string[10];
            for (int col = 1; col <= 9; col++)
            {
                var value = row.Cell(col).GetString().Trim();
                cells[col - 1] = string.IsNullOrEmpty(value) ? "" : value;
            }
            data.Add(cells);
        }

        ParseRaceDataFromWorksheet(ws, demo);

        for (int i = 0; i < data.Count; i++)
        {
            if (data[i].Length == 0) continue;

            var label = data[i][0].ToUpper();

            switch (label)
            {
                case "DRIVER":
                    int.TryParse(data[i][1], out int driverMale);
                    demo.DriverMale = driverMale;
                    break;

                case "PASSENGER":
                    int.TryParse(data[i][1], out int passengerMale);
                    int.TryParse(data[i][2], out int passengerFemale);
                    demo.PassengerMale = passengerMale;
                    demo.PassengerFemale = passengerFemale;
                    break;

                case "PEDESTRIAN":
                    int.TryParse(data[i][1], out int pedestrianMale);
                    int.TryParse(data[i][2], out int pedestrianFemale);
                    demo.PedestrianMale = pedestrianMale;
                    demo.PedestrianFemale = pedestrianFemale;
                    break;

                case "CYLIST":
                case "CYCLIST":
                    int.TryParse(data[i][1], out int cyclistMale);
                    int.TryParse(data[i][2], out int cyclistFemale);
                    demo.CyclistMale = cyclistMale;
                    demo.CyclistFemale = cyclistFemale;
                    break;

                case "AGE":
                    if (i + 1 < data.Count)
                    {
                        var ageValueRow = data[i + 1];
                        for (int col = 1; col <= 5; col++)
                        {
                            var ageLabel = data[i][col].Replace(" ", "").Replace("-", "").ToUpper();
                            int.TryParse(ageValueRow[col], out int val);

                            switch (ageLabel)
                            {
                                case "07": demo.Age0to7 = val; break;
                                case "0812":
                                case "812": demo.Age8to12 = val; break;
                                case "1318": demo.Age13to18 = val; break;
                                case "1935": demo.Age19to35 = val; break;
                                case "36":
                                case "36+": demo.Age36Plus = val; break;
                            }
                        }
                    }
                    break;

                case "RACE":
                    if (i + 1 < data.Count)
                    {
                        var raceDataRow = data[i + 1];
                        int black = 0, coloured = 0, white = 0, indian = 0, other = 0;

                        int.TryParse(raceDataRow[1], out black);
                        int.TryParse(raceDataRow[2], out coloured);
                        int.TryParse(raceDataRow[3], out white);
                        int.TryParse(raceDataRow[4], out indian);
                        int.TryParse(raceDataRow[5], out other);

                        if (black == 0 && coloured == 0 && white == 0 && indian == 0 && other == 0)
                        {
                            int.TryParse(data[i][6], out black);
                            int.TryParse(data[i][7], out coloured);
                            int.TryParse(data[i][8], out white);
                        }

                        demo.RaceBlack = black;
                        demo.RaceColoured = coloured;
                        demo.RaceWhite = white;
                        demo.RaceIndian = indian;
                        demo.RaceOther = other;
                    }
                    break;
            }
        }

        return demo;
    }

    private void ParseRaceDataFromWorksheet(IXLWorksheet ws, ImportDemographics demo)
    {
        var allRows = ws.RowsUsed().ToList();

        for (int rowIdx = 0; rowIdx < allRows.Count; rowIdx++)
        {
            var row = allRows[rowIdx];

            for (int col = 1; col <= 20; col++)
            {
                var cellValue = row.Cell(col).GetString().Trim().ToUpper();

                if (cellValue == "RACE")
                {
                    int raceBCol = -1, raceCCol = -1, raceWCol = -1, raceICol = -1, raceOCol = -1;

                    for (int searchCol = col + 1; searchCol <= col + 10; searchCol++)
                    {
                        var headerValue = row.Cell(searchCol).GetString().Trim().ToUpper();
                        switch (headerValue)
                        {
                            case "B": raceBCol = searchCol; break;
                            case "C": raceCCol = searchCol; break;
                            case "W": raceWCol = searchCol; break;
                            case "I": raceICol = searchCol; break;
                            case "O": raceOCol = searchCol; break;
                        }
                    }

                    if (rowIdx + 1 < allRows.Count)
                    {
                        var dataRow = allRows[rowIdx + 1];

                        if (raceBCol != -1)
                        {
                            int.TryParse(dataRow.Cell(raceBCol).GetString().Trim(), out int black);
                            demo.RaceBlack = black;
                        }
                        if (raceCCol != -1)
                        {
                            int.TryParse(dataRow.Cell(raceCCol).GetString().Trim(), out int coloured);
                            demo.RaceColoured = coloured;
                        }
                        if (raceWCol != -1)
                        {
                            int.TryParse(dataRow.Cell(raceWCol).GetString().Trim(), out int white);
                            demo.RaceWhite = white;
                        }
                        if (raceICol != -1)
                        {
                            int.TryParse(dataRow.Cell(raceICol).GetString().Trim(), out int indian);
                            demo.RaceIndian = indian;
                        }
                        if (raceOCol != -1)
                        {
                            int.TryParse(dataRow.Cell(raceOCol).GetString().Trim(), out int other);
                            demo.RaceOther = other;
                        }
                    }

                    return;
                }
            }
        }

        _logger.LogWarning("Could not find RACE data in worksheet");
    }

    private (DateOnly From, DateOnly To) ExtractPeriodFromFileName(string fileName)
    {
        var match = Regex.Match(fileName, @"(\d{1,2})[._-](\d{1,2})[._-](\d{4})");

        if (match.Success)
        {
            int a = int.Parse(match.Groups[1].Value);
            int b = int.Parse(match.Groups[2].Value);
            int year = int.Parse(match.Groups[3].Value);

            int day = a, month = b;
            if (month > 12 && day <= 12)
                (day, month) = (b, a);

            try
            {
                var fromDate = new DateOnly(year, month, 1);
                var toDate = fromDate.AddMonths(1).AddDays(-1);
                return (fromDate, toDate);
            }
            catch { }
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var defaultFrom = new DateOnly(today.Year, today.Month, 1);
        return (defaultFrom, defaultFrom.AddMonths(1).AddDays(-1));
    }

    private async Task SaveDemographicsAsync(
        ImportDemographics demographics, DateOnly periodFrom, DateOnly periodTo, string province)
    {
        if (!demographics.HasAgeData && !demographics.HasGenderData && !demographics.HasRaceData)
        {
            _logger.LogInformation("No demographics data to save");
            return;
        }

        var existingRecord = await _context.CrashDemographics
            .FirstOrDefaultAsync(d => d.PeriodFrom == periodFrom &&
                                      d.PeriodTo == periodTo &&
                                      d.ProvinceCode == province);

        if (existingRecord != null)
        {
            existingRecord.Age0to7 = demographics.Age0to7;
            existingRecord.Age8to12 = demographics.Age8to12;
            existingRecord.Age13to18 = demographics.Age13to18;
            existingRecord.Age19to35 = demographics.Age19to35;
            existingRecord.Age36Plus = demographics.Age36Plus;
            existingRecord.DriverMale = demographics.DriverMale;
            existingRecord.DriverFemale = demographics.DriverFemale;
            existingRecord.PassengerMale = demographics.PassengerMale;
            existingRecord.PassengerFemale = demographics.PassengerFemale;
            existingRecord.PedestrianMale = demographics.PedestrianMale;
            existingRecord.PedestrianFemale = demographics.PedestrianFemale;
            existingRecord.CyclistMale = demographics.CyclistMale;
            existingRecord.CyclistFemale = demographics.CyclistFemale;
            existingRecord.RaceBlack = demographics.RaceBlack;
            existingRecord.RaceColoured = demographics.RaceColoured;
            existingRecord.RaceWhite = demographics.RaceWhite;
            existingRecord.RaceIndian = demographics.RaceIndian;
            existingRecord.RaceOther = demographics.RaceOther;
            existingRecord.CreatedAt = DateTime.UtcNow;
        }
        else
        {
            _context.CrashDemographics.Add(new CrashDemographicRecord
            {
                PeriodFrom = periodFrom,
                PeriodTo = periodTo,
                ProvinceCode = province,
                Age0to7 = demographics.Age0to7,
                Age8to12 = demographics.Age8to12,
                Age13to18 = demographics.Age13to18,
                Age19to35 = demographics.Age19to35,
                Age36Plus = demographics.Age36Plus,
                DriverMale = demographics.DriverMale,
                DriverFemale = demographics.DriverFemale,
                PassengerMale = demographics.PassengerMale,
                PassengerFemale = demographics.PassengerFemale,
                PedestrianMale = demographics.PedestrianMale,
                PedestrianFemale = demographics.PedestrianFemale,
                CyclistMale = demographics.CyclistMale,
                CyclistFemale = demographics.CyclistFemale,
                RaceBlack = demographics.RaceBlack,
                RaceColoured = demographics.RaceColoured,
                RaceWhite = demographics.RaceWhite,
                RaceIndian = demographics.RaceIndian,
                RaceOther = demographics.RaceOther,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
    }

    private static byte ByteCell(IXLRow row, int col)
    {
        if (col <= 0) return 0;

        try
        {
            var cell = row.Cell(col);
            if (cell.IsEmpty()) return 0;

            if (cell.TryGetValue(out int i)) return (byte)Math.Clamp(i, 0, 255);

            var str = cell.GetString().Trim();
            if (int.TryParse(str, out var p)) return (byte)Math.Clamp(p, 0, 255);
        }
        catch { }
        return 0;
    }
}


public class ImportResult
{
    public string FileName { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> ErrorMessages { get; set; } = new();
    public ImportDemographics Demographics { get; set; } = new();

    public DateOnly? DetectedPeriodFrom { get; set; }
    public DateOnly? DetectedPeriodTo { get; set; }
    public string? DetectedDistrict { get; set; }
    public bool DetectedIsProvincial { get; set; }

    public void AddWarning(string msg) { if (Warnings.Count < 50) Warnings.Add(msg); }
    public void AddError(string msg) { if (ErrorMessages.Count < 50) ErrorMessages.Add(msg); }
}

public class ImportDemographics
{
    public int Age0to7 { get; set; }
    public int Age8to12 { get; set; }
    public int Age13to18 { get; set; }
    public int Age19to35 { get; set; }
    public int Age36Plus { get; set; }
    public int AgeTotal => Age0to7 + Age8to12 + Age13to18 + Age19to35 + Age36Plus;

    public int DriverMale { get; set; }
    public int DriverFemale { get; set; }
    public int PassengerMale { get; set; }
    public int PassengerFemale { get; set; }
    public int PedestrianMale { get; set; }
    public int PedestrianFemale { get; set; }
    public int CyclistMale { get; set; }
    public int CyclistFemale { get; set; }

    public int TotalMale => DriverMale + PassengerMale + PedestrianMale + CyclistMale;
    public int TotalFemale => DriverFemale + PassengerFemale + PedestrianFemale + CyclistFemale;
    public int GenderTotal => TotalMale + TotalFemale;

    public int RaceBlack { get; set; }
    public int RaceColoured { get; set; }
    public int RaceWhite { get; set; }
    public int RaceIndian { get; set; }
    public int RaceOther { get; set; }
    public int RaceTotal => RaceBlack + RaceColoured + RaceWhite + RaceIndian + RaceOther;

    public bool HasAgeData => AgeTotal > 0;
    public bool HasGenderData => GenderTotal > 0;
    public bool HasRaceData => RaceTotal > 0;
}