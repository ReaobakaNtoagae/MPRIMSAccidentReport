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
                   .Replace(" ", "").Replace("-", "").Replace(".", "").Replace("_", "").Replace("/", "");

    private (int HeaderRowNumber, ColumnMap Map)? BuildColumnMap(IXLWorksheet ws)
    {
        var rows = ws.RowsUsed().ToList();

        int subHeaderIdx = -1;
        const int scanCols = 60;

        for (int i = 0; i < rows.Count && subHeaderIdx == -1; i++)
        {
            var row = rows[i];
            var rowAbove = i > 0 ? rows[i - 1] : null;

            for (int c = 1; c <= scanCols; c++)
            {
                // FIX: was an exact match against "SAPS" — some files have
                // "SAPS STATION" in one cell instead of bare "SAPS", which
                // never matched at all.
                if (!NormalizeHeader(row.Cell(c).GetString()).StartsWith("SAPS")) continue;

                for (int c2 = c + 1; c2 <= c + 5; c2++)
                {
                    // FIX: AR NO's label sometimes sits split across two
                    // rows ("A/R" in the row above, "NO" in the header row
                    // itself) rather than as one "AR NO" cell — check both
                    // rows, not just the header row alone.
                    var headerCell = NormalizeHeader(row.Cell(c2).GetString());
                    var aboveCell = rowAbove != null ? NormalizeHeader(rowAbove.Cell(c2).GetString()) : "";

                    if (headerCell == "ARNO" || aboveCell == "AR")
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
            var headerText = NormalizeHeader(headerRow.Cell(c).GetString());
            var aboveText = groupRow != null ? NormalizeHeader(groupRow.Cell(c).GetString()) : "";

            
            if (headerText.StartsWith("SAPS")) { map.Saps = c; continue; }

            
            if (headerText == "ARNO" || aboveText == "AR") { map.ArNo = c; continue; }
            if (headerText == "ROUTE" || aboveText == "ROUTE") { map.Route = c; continue; }

            switch (headerText)
            {
                case "CAS": map.Cas = c; break;
                case "DATE": map.Date = c; break;
                case "DAY": map.Day = c; break;
                case "TIME": map.Time = c; break;
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

    
    private static readonly Regex MonthYearOnlyPattern = new(
        @"\b([A-Za-z]+)\s+(\d{4})\b",
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
                    else
                    {
                        // FIX: fallback for titles with no day-range, just
                        // "SEPTEMBER 2024" — period becomes the whole month.
                        var m2 = MonthYearOnlyPattern.Match(text);
                        if (m2.Success && MonthNameToNumber.TryGetValue(m2.Groups[1].Value, out var monthNum2) &&
                            int.TryParse(m2.Groups[2].Value, out var year2))
                        {
                            try
                            {
                                var from = new DateOnly(year2, monthNum2, 1);
                                info.PeriodFrom = from;
                                info.PeriodTo = from.AddMonths(1).AddDays(-1);
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

        var sheetsProcessed = new List<string>();
        var sheetsSkipped = new List<string>();

        var existingFormCrNos = await _context.Crashes
            .Where(c => c.CrNo != null)
            .Select(c => c.CrNo!)
            .ToHashSetAsync();

        // FIX: AR numbers are NOT reliably unique per station within a
        // file — confirmed against real data (e.g. two genuinely different
        // WITBANK crashes on the same day both filed under AR "158/09";
        // two TWEEFONTEIN crashes SIX DAYS apart both under "11/09").
        // Treating every AR collision as an automatic duplicate silently
        // discards real crash records. Instead: group by the AR-based
        // CrNo, and only treat a collision as a genuine duplicate if the
        // colliding rows' actual details (date/time/location/type) match —
        // otherwise it's a different crash reusing the same AR number,
        // which gets a disambiguated CrNo and is imported, with a visible
        // warning so it's auditable rather than silently guessed at.
        var crNoGroups = new Dictionary<string, List<CrashSummary>>();
        var stationSequenceCounters = new Dictionary<string, int>();

        foreach (var ws in wb.Worksheets)
        {
            var located = BuildColumnMap(ws);
            if (located == null)
            {
                sheetsSkipped.Add(ws.Name);
                continue;
            }

            var (headerRowNumber, map) = located.Value;

            var missing = map.MissingCriticalFields();
            if (missing.Count > 0)
            {
                sheetsSkipped.Add(ws.Name);
                result.AddWarning(
                    $"Sheet '{ws.Name}': header row found, but missing required columns " +
                    $"({string.Join(", ", missing)}) — skipped.");
                continue;
            }

            sheetsProcessed.Add(ws.Name);

            var headerInfo = ExtractReportHeaderInfo(ws);

            if (headerInfo.District == null && !headerInfo.IsProvincial)
            {
                var sheetNameNormalized = ws.Name.Trim().ToUpperInvariant();
                var sheetMatch = KnownDistricts.FirstOrDefault(d => sheetNameNormalized.Contains(d));
                if (sheetMatch != null)
                    headerInfo.District = sheetMatch;
            }

            DateOnly periodFrom, periodTo;
            bool periodGenuinelyDetected;
            if (headerInfo.PeriodFrom.HasValue && headerInfo.PeriodTo.HasValue)
            {
                periodFrom = headerInfo.PeriodFrom.Value;
                periodTo = headerInfo.PeriodTo.Value;
                periodGenuinelyDetected = true;
            }
            else
            {
                (periodFrom, periodTo) = ExtractPeriodFromFileName(fileName);
                periodGenuinelyDetected = false;
                _logger.LogWarning(
                    "Sheet '{Sheet}': no period found in worksheet header — falling back to filename: {From} to {To}",
                    ws.Name, periodFrom, periodTo);
            }

            // Only trust bare day-of-month date cells (see ParseCrashDate)
            // when the period came from the sheet's own header, not a
            // filename/today guess — otherwise a wrong guessed month would
            // silently propagate into every crash date in the sheet.
            int? fallbackMonth = periodGenuinelyDetected ? periodFrom.Month : null;

            result.DetectedPeriodFrom ??= periodFrom;
            result.DetectedPeriodTo ??= periodTo;
            result.DetectedDistrict ??= headerInfo.District;
            if (headerInfo.IsProvincial) result.DetectedIsProvincial = true;

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

            var demographics = ParseDemographics(summaryRows, ws, ws.Name);
            await SaveDemographicsAsync(demographics, periodFrom, periodTo, province);
            if (result.Demographics.AgeTotal == 0 && result.Demographics.GenderTotal == 0 && result.Demographics.RaceTotal == 0)
                result.Demographics = demographics;

            foreach (var row in dataRows)
            {
                result.TotalRows++;
                try
                {
                    var summary = ParseSummaryRow(row, map, fileName, periodFrom.Year, fallbackMonth, ws.Name, stationSequenceCounters);
                    if (summary == null)
                    {
                        result.Skipped++;
                        result.AddWarning($"Sheet '{ws.Name}', row {row.RowNumber()}: could not parse — skipped.");
                        continue;
                    }

                    if (existingFormCrNos.Contains(summary.CrNo))
                    {
                        result.Skipped++;
                        result.AddWarning(
                            $"Sheet '{ws.Name}', row {row.RowNumber()}: CrNo '{summary.CrNo}' already exists as a full CR1 form — skipped.");
                        continue;
                    }

                    if (crNoGroups.TryGetValue(summary.CrNo, out var existingGroup))
                    {
                        // Only a genuine duplicate if date/time/location/type all
                        // match an already-imported row under this same CrNo —
                        // otherwise this is a different crash that happens to
                        // reuse the same AR number, and gets disambiguated
                        // rather than silently dropped.
                        var isGenuineDuplicate = existingGroup.Any(existing =>
                            existing.CrashDate == summary.CrashDate &&
                            existing.CrashTime == summary.CrashTime &&
                            string.Equals(existing.Location, summary.Location, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(existing.CrashType, summary.CrashType, StringComparison.OrdinalIgnoreCase));

                        if (isGenuineDuplicate)
                        {
                            result.Skipped++;
                            result.AddWarning(
                                $"Sheet '{ws.Name}', row {row.RowNumber()}: CrNo '{summary.CrNo}' matches an already-" +
                                $"imported row with the same date/time/location/type — genuine duplicate, skipped.");
                            continue;
                        }

                        // Different crash, same reused AR number — disambiguate
                        // rather than lose this row. Suffix letter based on how
                        // many rows already share this base CrNo.
                        var suffix = (char)('B' + existingGroup.Count - 1);
                        var originalCrNo = summary.CrNo;
                        summary.CrNo = $"{originalCrNo}-{suffix}";
                        existingGroup.Add(summary);

                        result.AddWarning(
                            $"Sheet '{ws.Name}', row {row.RowNumber()}: AR number reused — '{originalCrNo}' already " +
                            $"used by a different crash (different date/time/location/type). Imported as " +
                            $"'{summary.CrNo}' instead. This source file's AR numbers are not unique per station; " +
                            $"worth flagging to whoever compiled it.");
                    }
                    else
                    {
                        crNoGroups[summary.CrNo] = new List<CrashSummary> { summary };
                    }

                    if (summary.CrashDate < periodFrom || summary.CrashDate > periodTo)
                    {
                        result.AddWarning(
                            $"Sheet '{ws.Name}', row {row.RowNumber()}: crash date {summary.CrashDate:dd/MM/yyyy} outside period " +
                            $"({periodFrom:dd/MM/yyyy} – {periodTo:dd/MM/yyyy}) — imported anyway.");
                    }

                    _context.CrashSummaries.Add(summary);
                    result.Imported++;
                }
                catch (Exception ex)
                {
                    result.Errors++;
                    result.AddError($"Sheet '{ws.Name}', row {row.RowNumber()}: {ex.Message}");
                    _logger.LogWarning(ex, "Import error on sheet {Sheet} row {Row}", ws.Name, row.RowNumber());
                }
            }
        }

        if (sheetsProcessed.Count == 0)
        {
            result.AddError("No sheet in this workbook matched the expected crash-register layout (SAPS/AR NO/CAS header row).");
        }

        _logger.LogInformation(
            "Import of {File} complete. Sheets processed: {Processed}. Sheets skipped: {Skipped}.",
            fileName, string.Join(", ", sheetsProcessed), string.Join(", ", sheetsSkipped));

        await _context.SaveChangesAsync();
        return result;
    }



    private static readonly Regex StationCaseBleed1 = new(
        @"\.?\s*CAS\s*:{1,2}\s*.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StationCaseBleed2 = new(
        @"\.?\s*CAS\s*\d.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StationCaseBleed3 = new(
        @"\s+\d{1,4}-\d{1,2}-\d{4}$", RegexOptions.Compiled);

    private static string CleanStationName(string raw)
    {
        var s = raw.Trim();
        s = StationCaseBleed1.Replace(s, "");
        s = StationCaseBleed2.Replace(s, "");
        s = StationCaseBleed3.Replace(s, "");
        s = Regex.Replace(s, @"[.\s*]+$", "");
        return s.Trim().ToUpperInvariant();
    }

    private static DateOnly? ParseCrashDate(string dateRaw, int fallbackYear, int? fallbackMonth)
    {
        if (string.IsNullOrWhiteSpace(dateRaw)) return null;

        var parts = dateRaw.Replace('-', '/').Split('/');


        if (parts.Length == 1)
        {
            if (fallbackMonth == null) return null;
            if (!int.TryParse(parts[0], out var bareDay)) return null;

            try
            {
                var clampedDay = Math.Min(Math.Max(bareDay, 1), DateTime.DaysInMonth(fallbackYear, fallbackMonth.Value));
                return new DateOnly(fallbackYear, fallbackMonth.Value, clampedDay);
            }
            catch
            {
                return null;
            }
        }

        if (parts.Length < 2) return null;

        if (!int.TryParse(parts[0], out var dd) || !int.TryParse(parts[1], out var mm))
            return null;

        var year = fallbackYear;
        if (parts.Length >= 3 && int.TryParse(parts[2], out var yyyy))
            year = yyyy;

        if (mm < 1 || mm > 12) return null;

        try
        {
            dd = Math.Min(Math.Max(dd, 1), DateTime.DaysInMonth(year, mm));
            return new DateOnly(year, mm, dd);
        }
        catch
        {
            return null;
        }
    }

    private CrashSummary? ParseSummaryRow(
        IXLRow row, ColumnMap map, string fileName, int fallbackYear, int? fallbackMonth, string sheetName,
        Dictionary<string, int> stationSequenceCounters)
    {
        var rawSaps = row.Cell(map.Saps).GetString().Trim();
        if (string.IsNullOrEmpty(rawSaps)) return null;

        var saps = CleanStationName(rawSaps);
        if (string.IsNullOrEmpty(saps)) return null;

        var arNo = map.ArNo > 0 ? row.Cell(map.ArNo).GetString().Trim() : "";
        var casNo = map.Cas > 0 ? row.Cell(map.Cas).GetString().Trim() : "";
        var dateRaw = row.Cell(map.Date).GetString().Trim();
        var timeRaw = map.Time > 0 ? row.Cell(map.Time).GetString().Trim() : "";
        var route = map.Route > 0 ? row.Cell(map.Route).GetString().Trim().ToUpper() : "";
        var location = map.Location > 0 ? row.Cell(map.Location).GetString().Trim() : "";
        var crashType = row.Cell(map.Type).GetString().Trim().ToUpper();
        var vehicles = row.Cell(map.Involved).GetString().Trim();

        var crashDate = ParseCrashDate(dateRaw, fallbackYear, fallbackMonth);
        if (crashDate == null) return null;

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

        string crNo;
        if (!string.IsNullOrEmpty(arNo))
        {
            
            var arNoParts = arNo.Split('/').Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();

            string completedArNo;
            if (arNoParts.Length >= 3 && arNoParts[2].Length == 4)
            {
                completedArNo = arNo;
            }
            else if (arNoParts.Length == 2)
            {
                completedArNo = $"{arNoParts[0]}/{arNoParts[1]}/{fallbackYear}";
            }
            else if (arNoParts.Length == 1 && fallbackMonth.HasValue)
            {
                completedArNo = $"{arNoParts[0]}/{fallbackMonth.Value:D2}/{fallbackYear}";
            }
            else
            {
                completedArNo = arNo;
                _logger.LogWarning(
                    "Sheet '{Sheet}': AR number '{ArNo}' for station '{Station}' has no month/year and " +
                    "none could be confidently determined from the sheet's period — stored as-is, " +
                    "incomplete. This is not the full canonical number.",
                    sheetName, arNo, saps);
            }

            crNo = $"{saps}-{completedArNo}";
        }
        else
        {
            var key = $"{sheetName}:{saps}";
            stationSequenceCounters.TryGetValue(key, out var seq);
            seq++;
            stationSequenceCounters[key] = seq;
            crNo = $"{saps}-NOAR{seq:D3}";
        }

        return new CrashSummary
        {
            CrNo = crNo,
            Station = saps,
            CasNo = string.IsNullOrEmpty(casNo) ? null : casNo,
            CrashDate = crashDate.Value,
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


    

    private ImportDemographics ParseDemographics(List<IXLRow> summaryRows, IXLWorksheet ws, string sheetName)
    {
        var demo = new ImportDemographics();

        if (!summaryRows.Any())
        {
            _logger.LogWarning("Sheet '{Sheet}': no summary rows found to parse demographics", sheetName);
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


        bool foundFixedLabelTotals = false;

        for (int i = 0; i < data.Count; i++)
        {
            if (data[i].Length == 0) continue;

            var label = data[i][0].ToUpper();

            switch (label)
            {
                case "DRIVER":
                    int.TryParse(data[i][1], out int driverMale);
                    int.TryParse(data[i][2], out int driverFemale);
                    demo.DriverMale = driverMale;
                    demo.DriverFemale = driverFemale;
                    foundFixedLabelTotals = true;
                    break;

                case "PASSENGER":
                    int.TryParse(data[i][1], out int passengerMale);
                    int.TryParse(data[i][2], out int passengerFemale);
                    demo.PassengerMale = passengerMale;
                    demo.PassengerFemale = passengerFemale;
                    foundFixedLabelTotals = true;
                    break;

                case "PEDESTRIAN":
                    int.TryParse(data[i][1], out int pedestrianMale);
                    int.TryParse(data[i][2], out int pedestrianFemale);
                    demo.PedestrianMale = pedestrianMale;
                    demo.PedestrianFemale = pedestrianFemale;
                    foundFixedLabelTotals = true;
                    break;

                case "CYLIST":
                case "CYCLIST":
                    int.TryParse(data[i][1], out int cyclistMale);
                    int.TryParse(data[i][2], out int cyclistFemale);
                    demo.CyclistMale = cyclistMale;
                    demo.CyclistFemale = cyclistFemale;
                    foundFixedLabelTotals = true;
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
            }
        }

        if (!foundFixedLabelTotals)
        {
            ParsePerVictimGenderAgeRows(data, demo, sheetName, _logger);
        }
        else
        {
            _logger.LogInformation(
                "Sheet '{Sheet}': fixed-label gender/role totals found in footer — per-victim listing " +
                "(if present) treated as supplementary detail only, not re-tallied into the totals.",
                sheetName);
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


    private static readonly Regex GenderCountPattern = new(
       @"^(\d*)\s*([MF])$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private void ParsePerVictimGenderAgeRows(
      List<string[]> data, ImportDemographics demo, string sheetName, ILogger logger)
    {
        int headerIdx = -1;

        for (int i = 0; i < data.Count - 1; i++)
        {
            if (!string.Equals(data[i].ElementAtOrDefault(2)?.Trim(), "GENDER", StringComparison.OrdinalIgnoreCase))
                continue;

            var subRow = data[i + 1];
            var subLabels = subRow.Skip(3).Select(s => (s ?? "").Trim().ToUpperInvariant()).ToList();
            if (subLabels.Any(s => s is "DRI" or "PASS" or "PED/CYL" or "PED" or "CYL"))
            {
                headerIdx = i;
                break;
            }
        }

        if (headerIdx == -1)
            return;

        var subHeader = data[headerIdx + 1].Skip(3).Select(s => (s ?? "").Trim().ToUpperInvariant()).ToList();
        int driColOffset = subHeader.IndexOf("DRI");
        int passColOffset = subHeader.IndexOf("PASS");
        int pedCylColOffset = subHeader.FindIndex(s => s is "PED/CYL" or "PED" or "CYL");

        int pedCylTallied = 0;
        int rowsProcessed = 0;

        for (int i = headerIdx + 2; i < data.Count; i++)
        {
            var row = data[i];
            var firstCell = (row.ElementAtOrDefault(0) ?? "").Trim();

            if (string.IsNullOrEmpty(firstCell) || firstCell.Contains("TOTAL", StringComparison.OrdinalIgnoreCase))
                break;

            var genderRaw = (row.ElementAtOrDefault(2) ?? "").Trim();
            var m = GenderCountPattern.Match(genderRaw);
            if (!m.Success) continue;

            int count = string.IsNullOrEmpty(m.Groups[1].Value) ? 1 : int.Parse(m.Groups[1].Value);
            bool isMale = m.Groups[2].Value.Equals("M", StringComparison.OrdinalIgnoreCase);

            bool hasDri = driColOffset >= 0 && !string.IsNullOrWhiteSpace(row.ElementAtOrDefault(3 + driColOffset));
            bool hasPass = passColOffset >= 0 && !string.IsNullOrWhiteSpace(row.ElementAtOrDefault(3 + passColOffset));
            bool hasPedCyl = pedCylColOffset >= 0 && !string.IsNullOrWhiteSpace(row.ElementAtOrDefault(3 + pedCylColOffset));

            if (hasDri)
            {
                if (isMale) demo.DriverMale++; else demo.DriverFemale++;
            }
            else if (hasPass)
            {
                if (isMale) demo.PassengerMale++; else demo.PassengerFemale++;
            }
            else if (hasPedCyl)
            {
                if (isMale) demo.PedestrianMale++; else demo.PedestrianFemale++;
                pedCylTallied += count;
            }

            rowsProcessed++;
        }

        if (pedCylTallied > 0)
        {
            logger.LogWarning(
                "Sheet '{Sheet}': {Count} pedestrian/cyclist victims tallied from a combined " +
                "PED/CYL column — this source format cannot distinguish cyclists from pedestrians. " +
                "All {Count} were counted as Pedestrian; CyclistMale/Female remain 0 for this sheet.",
                sheetName, pedCylTallied, pedCylTallied);
        }

        logger.LogInformation(
            "Sheet '{Sheet}': parsed {Rows} per-victim gender/age rows.", sheetName, rowsProcessed);
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