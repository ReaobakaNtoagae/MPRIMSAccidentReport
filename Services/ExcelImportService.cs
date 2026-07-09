using ClosedXML.Excel;
using CrashReport.Data;
using CrashReport.Models;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CrashReport.Services;

public class ExcelImportService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ExcelImportService> _logger;

    private static readonly HashSet<string> NonVehicleTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "P/D",
            "HIT N RUN",
            "HIT & RUN"
        };

    public ExcelImportService(AppDbContext context, ILogger<ExcelImportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ════════════════════════════════════════════════════════════
    // DYNAMIC COLUMN DETECTION
    //
    // Every field is located by its header TEXT, not by a fixed column
    // number. If a station inserts, removes, or reorders a column, the
    // map below still finds the right one — instead of every hardcoded
    // Cell(9)/Cell(24)/etc. silently reading the wrong field.
    //
    // The tricky part: the injury columns have TWO header rows —
    //   Row 5 (group):    FATAL          GENDER   SERIOUS        SLIGHT
    //   Row 6 (sub):   D  P  PD  C       M  F   D  P  PD  C   D  P  PD  C
    // "D" alone is ambiguous — it appears in three different blocks.
    // So we first find each group's column span from the group row,
    // then only look for D/P/PD/C inside that span.
    // ════════════════════════════════════════════════════════════

    private class ColumnMap
    {
        public int Saps, ArNo, Cas, Date, Day, Time, Route, Location, Type, Involved;
        public int FatalD, FatalP, FatalPd, FatalC, GenderM, GenderF;
        public int SeriousD, SeriousP, SeriousPd, SeriousC;
        public int SlightD, SlightP, SlightPd, SlightC;

        /// <summary>
        /// Lists any critical field that could not be located, so the
        /// import can fail loudly with a clear message instead of silently
        /// importing garbage from a wrong (or default-zero) column.
        /// </summary>
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

    /// <summary>
    /// Locates the header row (by finding "SAPS" with "AR NO" nearby, in
    /// whatever column they happen to sit) and builds a ColumnMap from it,
    /// combined with the group-header row directly above it.
    /// Returns null if no header row could be found at all.
    /// </summary>
    private (int HeaderRowNumber, ColumnMap Map)? BuildColumnMap(IXLWorksheet ws)
    {
        var rows = ws.RowsUsed().ToList();

        int subHeaderIdx = -1;
        const int scanCols = 60; // generous — real files use ~24, this tolerates extra columns

        for (int i = 0; i < rows.Count && subHeaderIdx == -1; i++)
        {
            var row = rows[i];
            for (int c = 1; c <= scanCols; c++)
            {
                if (NormalizeHeader(row.Cell(c).GetString()) != "SAPS") continue;

                // "AR NO" should appear within the next few columns
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

        // ── Simple, uniquely-named columns ─────────────────────
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

        // ── Grouped injury columns (FATAL / GENDER / SERIOUS / SLIGHT) ──
        if (groupRow != null)
        {
            var groups = new List<(string Group, int StartCol)>();
            for (int c = 1; c <= scanCols; c++)
            {
                var g = NormalizeHeader(groupRow.Cell(c).GetString());
                if (g is "FATAL" or "GENDER" or "SERIOUS" or "SLIGHT")
                    groups.Add((g, c));
            }
            groups.Add(("__END__", scanCols + 1)); // sentinel to close the last span

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

    // ════════════════════════════════════════════════════════════
    // REPORT HEADER DETECTION — period and district
    //
    // The period ("ACCIDENT REPORT:   01 - 31 JANUARY 2026") and the
    // district/area label ("EHLANZENI", "PROVINCIAL", etc.) are printed
    // as free text near the top of the sheet. Rather than assuming a
    // fixed cell, both are found by scanning the first few rows and
    // pattern-matching the text — same "search by content, not position"
    // approach as the column map above.
    // ════════════════════════════════════════════════════════════

    private class ReportHeaderInfo
    {
        public DateOnly? PeriodFrom { get; set; }
        public DateOnly? PeriodTo { get; set; }
        public string? District { get; set; }     // e.g. "EHLANZENI", "GERT SIBANDE" — null if not present
        public bool IsProvincial { get; set; }     // header explicitly said PROVINCIAL/CONSOLIDATED/MPUMALANGA
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

    // Longest/most-specific names first so "EHLANZENI SOUTH" is matched
    // in full rather than being caught by a looser check for "EHLANZENI".
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

    // Matches "01 - 31 JANUARY 2026", "1-5 February 2025", etc.
    private static readonly Regex PeriodPattern = new(
        @"(\d{1,2})\s*-\s*(\d{1,2})\s+([A-Za-z]+)\s+(\d{4})",
        RegexOptions.Compiled);

    private ReportHeaderInfo ExtractReportHeaderInfo(IXLWorksheet ws)
    {
        var info = new ReportHeaderInfo();

        // The header block (title, period, district label) always sits
        // in the first handful of rows, well above the SAPS/AR NO header.
        var rows = ws.RowsUsed().Take(8).ToList();

        foreach (var row in rows)
        {
            for (int c = 1; c <= 30; c++)
            {
                var text = row.Cell(c).GetString();
                if (string.IsNullOrWhiteSpace(text)) continue;

                // ── Period ──
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
                            catch
                            {
                                // Malformed date text (e.g. day 31 in a 30-day month
                                // typed incorrectly) — leave null, caller falls back.
                            }
                        }
                    }
                }

                // ── District / area label ──
                if (info.District == null && !info.IsProvincial)
                {
                    var normalized = text.Trim().ToUpperInvariant();

                    if (ProvincialLabels.Contains(normalized))
                    {
                        info.IsProvincial = true;
                    }
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

        DebugFindRaceTable(ws);

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
                string.Join(", ", missing) +
                ". Check the column headers in the source file match the expected names " +
                "(SAPS, AR NO, CAS, DATE, DAY, TIME, ROUTE, LOCATION, TYPE, INVOLVED, " +
                "and the FATAL/GENDER/SERIOUS/SLIGHT group headers with D/P/PD/C sub-columns).");
            return result;
        }

        // ── Period and district, read from the sheet's own header text ──
        var headerInfo = ExtractReportHeaderInfo(ws);

        DateOnly periodFrom, periodTo;
        if (headerInfo.PeriodFrom.HasValue && headerInfo.PeriodTo.HasValue)
        {
            periodFrom = headerInfo.PeriodFrom.Value;
            periodTo = headerInfo.PeriodTo.Value;
            _logger.LogInformation(
                "Period detected from worksheet header: {From} to {To}", periodFrom, periodTo);
        }
        else
        {
            (periodFrom, periodTo) = ExtractPeriodFromFileName(fileName);
            _logger.LogWarning(
                "Could not find a period ('DD - DD MONTH YYYY') in the worksheet header — " +
                "falling back to filename/date parsing: {From} to {To}", periodFrom, periodTo);
        }

        result.DetectedPeriodFrom = periodFrom;
        result.DetectedPeriodTo = periodTo;
        result.DetectedDistrict = headerInfo.District;
        result.DetectedIsProvincial = headerInfo.IsProvincial;

        _logger.LogInformation(
            "Report header — Period: {From} to {To}, District: {District}, Provincial: {IsProvincial}",
            periodFrom, periodTo, headerInfo.District ?? "(not found)", headerInfo.IsProvincial);

        var allRows = ws.RowsUsed().Skip(headerRowNumber).ToList();

        var dataRows = new List<IXLRow>();
        var summaryRows = new List<IXLRow>();
        bool inSummary = false;

        foreach (var row in allRows)
        {
            var saps = row.Cell(map.Saps).GetString().Trim();

            if (string.IsNullOrWhiteSpace(saps) || saps.Equals("TOTAL", StringComparison.OrdinalIgnoreCase))
            {
                if (inSummary)
                    summaryRows.Add(row);
                else
                    inSummary = true;
                continue;
            }

            var col7 = row.Cell(map.Location > 0 ? map.Location : 8).GetString().Trim();
            var col0 = saps;

            if (col7.Equals("GRAND TOTAL", StringComparison.OrdinalIgnoreCase) ||
                col0.StartsWith("TOTAL:", StringComparison.OrdinalIgnoreCase))
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

        var defaultVehicle = await GetOrCreateDefaultVehicle();
        var defaultVehicleId = defaultVehicle.VehicleId;

        var existingCrNos = await _context.Crashes
            .Select(c => c.CrNo)
            .Where(c => c != null)
            .ToHashSetAsync();

        foreach (var row in dataRows)
        {
            result.TotalRows++;
            try
            {
                var crash = ParseDataRow(row, map, province, defaultVehicleId, periodFrom.Year);
                if (crash == null)
                {
                    result.Skipped++;
                    result.AddWarning($"Row {row.RowNumber()}: could not parse — skipped.");
                    continue;
                }

                if (crash.CrNo != null && existingCrNos.Contains(crash.CrNo))
                {
                    result.Skipped++;
                    result.AddWarning($"Row {row.RowNumber()}: duplicate CrNo '{crash.CrNo}' — skipped.");
                    continue;
                }

                // Sanity check: does this row's date actually fall inside the
                // period declared at the top of the sheet? A mismatch usually
                // means a typo in the DATE column on a manually-maintained
                // file — worth flagging for review, not worth blocking the import.
                if (crash.CrashDate < periodFrom || crash.CrashDate > periodTo)
                {
                    result.AddWarning(
                        $"Row {row.RowNumber()}: crash date {crash.CrashDate:dd/MM/yyyy} falls outside " +
                        $"the declared report period ({periodFrom:dd/MM/yyyy} – {periodTo:dd/MM/yyyy}). " +
                        $"Imported anyway — please verify the DATE column on this row.");
                }

                _context.Crashes.Add(crash);
                if (crash.CrNo != null) existingCrNos.Add(crash.CrNo);
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

    private async Task<Vehicle> GetOrCreateDefaultVehicle()
    {
        var defaultVehicle = await _context.Vehicles
            .FirstOrDefaultAsync(v => v.Make == "IMPORTED" && v.Model == "DEFAULT");

        if (defaultVehicle == null)
        {
            defaultVehicle = new Vehicle
            {
                Make = "IMPORTED",
                Model = "DEFAULT",
                VehicleTypeCode = "UNKNOWN",
                CountryOfRegistration = "RSA",
                CreatedAt = DateTime.UtcNow
            };
            _context.Vehicles.Add(defaultVehicle);
            await _context.SaveChangesAsync();
        }

        return defaultVehicle;
    }

    private Crash? ParseDataRow(IXLRow row, ColumnMap map, string province, int defaultVehicleId, int fallbackYear)
    {
        var saps = row.Cell(map.Saps).GetString().Trim().ToUpper();
        var arNo = map.ArNo > 0 ? row.Cell(map.ArNo).GetString().Trim() : "";
        var casNo = map.Cas > 0 ? row.Cell(map.Cas).GetString().Trim() : "";
        var dateRaw = row.Cell(map.Date).GetString().Trim();
        var day = map.Day > 0 ? row.Cell(map.Day).GetString().Trim() : "";
        var timeRaw = map.Time > 0 ? row.Cell(map.Time).GetString().Trim() : "";
        var route = map.Route > 0 ? row.Cell(map.Route).GetString().Trim().ToUpper() : "";
        var location = map.Location > 0 ? row.Cell(map.Location).GetString().Trim() : "";

        var crashType = row.Cell(map.Type).GetString().Trim().ToUpper();

        var vehicles = row.Cell(map.Involved).GetString().Trim();
        var vehicleEntries = ParseVehicleEntries(vehicles);
        var vehicleCount = vehicleEntries.Count;

        if (string.IsNullOrEmpty(saps)) return null;

        DateOnly crashDate = DateOnly.FromDateTime(DateTime.Today);
        if (!string.IsNullOrEmpty(dateRaw))
        {
            var parts = dateRaw.Replace('-', '/').Split('/');
            if (parts.Length >= 2)
            {
                if (int.TryParse(parts[0], out var dd) && int.TryParse(parts[1], out var mm))
                {
                    // FIX: was DateTime.Today.Year — since the DATE column
                    // never actually contains a year ("22/01" only), that
                    // meant every row silently got the CURRENT year instead
                    // of the year the report actually covers. fallbackYear
                    // comes from the period already detected off the sheet's
                    // own header text ("01 - 31 JANUARY 2021" → 2021).
                    var year = fallbackYear;
                    if (parts.Length >= 3 && int.TryParse(parts[2], out var yyyy))
                        year = yyyy;

                    dd = Math.Min(dd, DateTime.DaysInMonth(year, mm));
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

        int fatalD = IntCell(row, map.FatalD);
        int fatalP = IntCell(row, map.FatalP);
        int fatalPD = IntCell(row, map.FatalPd);
        int fatalC = IntCell(row, map.FatalC);
        int fatalM = IntCell(row, map.GenderM);
        int fatalF = IntCell(row, map.GenderF);

        int serD = IntCell(row, map.SeriousD);
        int serP = IntCell(row, map.SeriousP);
        int serPD = IntCell(row, map.SeriousPd);
        int serC = IntCell(row, map.SeriousC);

        int sliD = IntCell(row, map.SlightD);
        int sliP = IntCell(row, map.SlightP);
        int sliPD = IntCell(row, map.SlightPd);
        int sliC = IntCell(row, map.SlightC);

        var crNo = string.IsNullOrEmpty(arNo) ? saps : $"{saps}-{arNo}";

        var crash = new Crash
        {
            CrNo = crNo,
            CasNo = string.IsNullOrEmpty(casNo) ? null : casNo,
            ProvinceCode = province,
            CrashDate = crashDate,
            CrashTime = crashTime,
            RoadNumber = string.IsNullOrEmpty(route) ? null : route,
            BriefDescription = BuildBriefDescription(location, crashType, vehicleCount),
            NoOfVehiclesInvolved = (byte)Math.Min(vehicleCount, 255),
            VehicleString = vehicles,
            CreatedAt = DateTime.UtcNow
        };

        if (!string.IsNullOrEmpty(location))
        {
            var crashLocation = new CrashLocation
            {
                Crash = crash,
                StreetRoadName = string.IsNullOrEmpty(route) ? null : route,
                CityTown = string.IsNullOrEmpty(location) ? null : location,
                Suburb = ParseSuburbFromLocation(location),
                BuiltUpArea = DetermineBuiltUpArea(location),
                AreaType = DetermineAreaType(location)
            };
            crash.CrashLocations.Add(crashLocation);
        }

        int vehicleSequence = 1;
        foreach (var vehicleEntry in vehicleEntries)
        {
            var crashVehicle = new CrashVehicle
            {
                Crash = crash,
                VehicleId = defaultVehicleId,
                VehicleType = vehicleEntry.VehicleType,
                VehicleReference = $"V{vehicleSequence}",
                DriverPersonId = null,
                SeatbeltUsed = null,
                AlcoholSuspected = null,
                AlcoholTestResult = null,
                DrugSuspected = null,
                DrugTestResult = null,
                VehicleManoeuvre = null,
                PositionBeforeCrash = null,
                PassengersForReward = null,
                BreakdownCompany = null
            };

            crash.CrashVehicles.Add(crashVehicle);
            vehicleSequence++;
        }

        if (!string.IsNullOrEmpty(crashType))
        {
            crash.CrashConditions.Add(new CrashCondition
            {
                CrashType = crashType
            });
        }

        var fatalTotal = fatalD + fatalP + fatalPD + fatalC;

        AddPersons(crash, "Driver", "Fatal", fatalD, fatalM, fatalF, fatalTotal);
        AddPersons(crash, "Passenger", "Fatal", fatalP, fatalM, fatalF, fatalTotal);
        AddPersons(crash, "Pedestrian", "Fatal", fatalPD, fatalM, fatalF, fatalTotal);
        AddPersons(crash, "Bicyclist", "Fatal", fatalC, fatalM, fatalF, fatalTotal);
        AddPersons(crash, "Driver", "Serious", serD, 0, 0, 0);
        AddPersons(crash, "Passenger", "Serious", serP, 0, 0, 0);
        AddPersons(crash, "Pedestrian", "Serious", serPD, 0, 0, 0);
        AddPersons(crash, "Bicyclist", "Serious", serC, 0, 0, 0);
        AddPersons(crash, "Driver", "Slight", sliD, 0, 0, 0);
        AddPersons(crash, "Passenger", "Slight", sliP, 0, 0, 0);
        AddPersons(crash, "Pedestrian", "Slight", sliPD, 0, 0, 0);
        AddPersons(crash, "Bicyclist", "Slight", sliC, 0, 0, 0);

        return crash;
    }

    private List<VehicleEntry> ParseVehicleEntries(string vehiclesStr)
    {
        var entries = new List<VehicleEntry>();

        if (string.IsNullOrWhiteSpace(vehiclesStr))
            return entries;

        var s = vehiclesStr.Trim();

        if (s.Equals("P/D", StringComparison.OrdinalIgnoreCase))
            return entries;

        var parts = s.Split('/')
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        int vehicleIndex = 1;
        foreach (var part in parts)
        {
            if (NonVehicleTypes.Contains(part))
                continue;

            var vehicleType = part.Trim();

            entries.Add(new VehicleEntry
            {
                Index = vehicleIndex,
                VehicleType = vehicleType,
                Reference = $"V{vehicleIndex}"
            });
            vehicleIndex++;
        }

        return entries;
    }

    private void AddPersons(Crash crash, string role, string severity, int count,
        int maleTotal, int femaleTotal, int totalFatal)
    {
        if (count <= 0) return;

        for (int i = 0; i < count; i++)
        {
            string? gender = null;

            if (severity == "Fatal" && totalFatal > 0 && (maleTotal > 0 || femaleTotal > 0))
            {
                var assignedFatalMales = crash.CrashPeople
                    .Count(p => p.SeverityOfInjury == "Fatal" &&
                                p.Role == role &&
                                p.Person?.Gender == "Male");

                var assignedFatalFemales = crash.CrashPeople
                    .Count(p => p.SeverityOfInjury == "Fatal" &&
                                p.Role == role &&
                                p.Person?.Gender == "Female");

                if (assignedFatalMales < maleTotal)
                    gender = "Male";
                else if (assignedFatalFemales < femaleTotal)
                    gender = "Female";
            }

            var person = new Person
            {
                Surname = "IMPORTED",
                FullNames = "RECORD",
                Gender = gender,
                IdType = "UNKNOWN"
            };

            var crashPerson = new CrashPerson
            {
                Person = person,
                Role = role,
                SeverityOfInjury = severity
            };

            if (role == "Driver" && crash.CrashVehicles.Any())
            {
                var firstVehicle = crash.CrashVehicles.First();
                crashPerson.CrashVehicle = firstVehicle;
                crashPerson.CrashVehicleId = firstVehicle.CrashVehicleId;
            }

            crash.CrashPeople.Add(crashPerson);
        }
    }

    private string BuildBriefDescription(string location, string crashType, int vehicleCount)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(location))
            parts.Add($"Location: {location}");

        if (!string.IsNullOrEmpty(crashType))
            parts.Add($"Type: {crashType}");

        if (vehicleCount > 0)
            parts.Add($"Vehicles: {vehicleCount}");

        return parts.Count > 0 ? string.Join(" | ", parts) : "Imported from Excel";
    }

    private static string? ParseSuburbFromLocation(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return null;

        if (Regex.IsMatch(location, @"\b(RD|ROAD|STR|STREET|DR|DRIVE)\b", RegexOptions.IgnoreCase))
            return null;

        if (location.Split(' ').Length <= 3 && !location.Contains("/"))
            return location;

        return null;
    }

    private static string? ParseCityTownFromLocation(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return null;

        var knownTowns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TONGA", "WHITE RIVER", "MASOYI", "BARBERTON", "KANYAMAZANE",
            "MATSULU", "NGODWANA", "MHALA", "CALCUTTA", "MASHISHING",
            "NELSPRUIT", "MALALANE", "SCHOEMANSDAL", "KOMATIPOORT",
            "HAZYVIEW", "SABIE", "ACORNHOEK", "KABOKWENI", "GRASKOP",
            "BUSHBUCKRIDGE", "KAMHLUSHWA"
        };

        foreach (var town in knownTowns)
        {
            if (location.Contains(town, StringComparison.OrdinalIgnoreCase))
                return town;
        }

        return null;
    }

    private static bool? DetermineBuiltUpArea(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return null;

        var builtUpIndicators = new[] { "STR", "STREET", "RD", "ROAD", "DRIVE", "AVE", "AVENUE" };

        foreach (var indicator in builtUpIndicators)
        {
            if (location.Contains(indicator, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (ParseCityTownFromLocation(location) != null)
            return true;

        return false;
    }

    private static string? DetermineAreaType(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return null;

        var ruralIndicators = new[] { "FARM", "NATURE RESERVE", "RURAL", "PLAAS" };
        foreach (var indicator in ruralIndicators)
        {
            if (location.Contains(indicator, StringComparison.OrdinalIgnoreCase))
                return "Rural";
        }

        var urbanIndicators = new[] { "STR", "STREET", "DRIVE", "AVE", "ROAD" };
        foreach (var indicator in urbanIndicators)
        {
            if (location.Contains(indicator, StringComparison.OrdinalIgnoreCase))
                return "Urban";
        }

        return "Unknown";
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

        _logger.LogInformation("Parsing demographics from {RowCount} summary rows", data.Count);

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

    private void DebugFindRaceTable(IXLWorksheet ws)
    {
        // Retained from the original for troubleshooting; consider
        // dropping to LogDebug or removing once the dynamic column
        // mapping above has been verified against real station files.
        var allRows = ws.RowsUsed().ToList();

        for (int rowIdx = 0; rowIdx < Math.Min(allRows.Count, 50); rowIdx++)
        {
            var row = allRows[rowIdx];
            for (int col = 1; col <= 60; col++)
            {
                var cellValue = row.Cell(col).GetString().Trim().ToUpper();
                if (cellValue == "RACE")
                {
                    _logger.LogInformation("Found 'RACE' at Row {Row}, Col {Col}", row.RowNumber(), col);
                }
            }
        }
    }

    /// <summary>
    /// Fallback only — used when the worksheet header itself doesn't contain
    /// a recognisable "DD - DD MONTH YYYY" period. Tries to pull a date out
    /// of the filename, accepting dots, dashes, or underscores as separators
    /// (e.g. "18.02.2025", "18-02-2025", or "EHL_Acc_Data_..._31_01_2026").
    /// </summary>
    private (DateOnly From, DateOnly To) ExtractPeriodFromFileName(string fileName)
    {
        var match = Regex.Match(fileName, @"(\d{1,2})[._-](\d{1,2})[._-](\d{4})");

        if (match.Success)
        {
            int a = int.Parse(match.Groups[1].Value);
            int b = int.Parse(match.Groups[2].Value);
            int year = int.Parse(match.Groups[3].Value);

            // Filenames in this project use DD_MM_YYYY (day first), matching
            // the DD/MM date format used throughout the rest of the sheet.
            int day = a, month = b;
            if (month > 12 && day <= 12)
            {
                // Defensive swap in case a MM_DD_YYYY-style name slips through.
                (day, month) = (b, a);
            }

            try
            {
                var fromDate = new DateOnly(year, month, 1);
                var toDate = fromDate.AddMonths(1).AddDays(-1);
                _logger.LogInformation("Extracted period from filename: {From} to {To}", fromDate, toDate);
                return (fromDate, toDate);
            }
            catch
            {
                // Fall through to the current-month default below.
            }
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var defaultFrom = new DateOnly(today.Year, today.Month, 1);
        var defaultTo = defaultFrom.AddMonths(1).AddDays(-1);

        _logger.LogWarning(
            "No period found in worksheet header or filename — using current month as a last resort: {From} to {To}",
            defaultFrom, defaultTo);
        return (defaultFrom, defaultTo);
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

            _logger.LogInformation("Updated existing demographics record for {Province} - Period: {PeriodFrom} to {PeriodTo}",
                province, periodFrom, periodTo);
        }
        else
        {
            var record = new CrashDemographicRecord
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
            };

            _context.CrashDemographics.Add(record);
            _logger.LogInformation("Added new demographics record for {Province} - Period: {PeriodFrom} to {PeriodTo}",
                province, periodFrom, periodTo);
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Demographics saved successfully for {Province}", province);
    }

    /// <summary>
    /// Reads an integer from the given column. Returns 0 for column 0
    /// (meaning that field wasn't found in the header at all) so a
    /// missing optional column degrades gracefully instead of throwing.
    /// </summary>
    private static int IntCell(IXLRow row, int col)
    {
        if (col <= 0) return 0;

        try
        {
            var cell = row.Cell(col);
            if (cell.IsEmpty()) return 0;

            if (cell.TryGetValue(out int i)) return i;

            var str = cell.GetString().Trim();
            if (string.IsNullOrEmpty(str)) return 0;

            if (int.TryParse(str, out var p)) return p;
        }
        catch { }
        return 0;
    }

    private class VehicleEntry
    {
        public int Index { get; set; }
        public string VehicleType { get; set; } = string.Empty;
        public string? Reference { get; set; }
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

    // ── Detected from the worksheet header — surface these in the UI so
    //    the admin can confirm the right file/period/district was picked
    //    up before trusting the import.
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