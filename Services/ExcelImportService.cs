using CrashReport.Data;
using CrashReport.Models;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Services;

public class ImportResult
{
    public bool Success { get; set; }
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }
    public int DuplicateCount { get; set; }
    public string SheetName { get; set; } = string.Empty;
    public string ReportPeriod { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class ExcelImportService
{
    private readonly AppDbContext _context;

    // ── Column positions (0-based, matching the EHL workbook) ─
    // Row 6: SAPS | AR NO | CAS | DATE | DAY | TIME | ROUTE | LOCATION | TYPE
    //          0      1      2     3      4     5       6        7         8
    // FATAL:   D=9  P=10  PD=11  C=12
    // GENDER:  M=13 F=14
    // SERIOUS: D=15 P=16  PD=17  C=18
    // SLIGHT:  D=19 P=20  PD=21  C=22
    // VEHICLES: 23
    private const int COL_SAPS = 0;
    private const int COL_AR_NO = 1;
    private const int COL_CAS = 2;
    private const int COL_DATE = 3;
    private const int COL_DAY = 4;
    private const int COL_TIME = 5;
    private const int COL_ROUTE = 6;
    private const int COL_LOCATION = 7;
    private const int COL_CRASH_TYPE = 8;
    private const int COL_FATAL_D = 9;
    private const int COL_FATAL_P = 10;
    private const int COL_FATAL_PD = 11;
    private const int COL_FATAL_C = 12;
    private const int COL_GENDER_M = 13;
    private const int COL_GENDER_F = 14;
    private const int COL_SERIOUS_D = 15;
    private const int COL_SERIOUS_P = 16;
    private const int COL_SERIOUS_PD = 17;
    private const int COL_SERIOUS_C = 18;
    private const int COL_SLIGHT_D = 19;
    private const int COL_SLIGHT_P = 20;
    private const int COL_SLIGHT_PD = 21;
    private const int COL_SLIGHT_C = 22;
    private const int COL_VEHICLES = 23;

    private const int DATA_START_ROW = 7; // Row 7 is the first data row

    // ── Stop words that mark end of data ─────────────────────
    private static readonly HashSet<string> StopWords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "TOTAL", "GRAND TOTAL", "RACE", "VICTIMS",
            "AGE", "VICTIM GENDER", "DRIVER", "PASSENGER",
            "PEDESTRIAN", "CYCLIST", "CYLIST"
        };

    public ExcelImportService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ImportResult> ImportAsync(
        Stream fileStream,
        string fileName,
        string defaultProvince = "MP")
    {
        var result = new ImportResult();

        try
        {
            using var wb = new XLWorkbook(fileStream);

            // ── Pick the first data sheet ─────────────────────
            IXLWorksheet? ws = null;
            foreach (var sheet in wb.Worksheets)
            {
                var cell1 = sheet.Cell(1, 1).GetString().Trim().ToUpper();
                if (!cell1.Contains("GRAND") && !cell1.Contains("SUMMARY"))
                {
                    ws = sheet;
                    break;
                }
            }

            if (ws == null)
            {
                result.Success = false;
                result.Errors.Add("No valid data sheet found in the workbook.");
                return result;
            }

            result.SheetName = ws.Name;

            // ── Extract report period from row 1 col F ────────
            var periodText = ws.Cell(1, 6).GetString().Trim();
            result.ReportPeriod = periodText.Contains("ACCIDENT REPORT")
                ? periodText.Replace("ACCIDENT REPORT:", "").Trim()
                : ws.Name;

            // ── Ensure a placeholder "Unknown" person exists ──
            // Imported injury summaries need a PersonId FK.
            // We create one generic person per import session.
            var unknownPerson = await GetOrCreateUnknownPersonAsync();

            // ── Process rows ──────────────────────────────────
            int rowNum = DATA_START_ROW;

            while (true)
            {
                var row = ws.Row(rowNum);

                // Read col A — stop on summary/total rows
                var firstVal = row.Cell(COL_SAPS + 1).GetString().Trim();
                if (StopWords.Contains(firstVal))
                    break;

                // Stop on truly empty rows (col A AND col D both empty)
                var dateVal = row.Cell(COL_DATE + 1).GetString().Trim();
                if (string.IsNullOrEmpty(firstVal) && string.IsNullOrEmpty(dateVal))
                {
                    rowNum++;
                    // Allow up to 3 consecutive blank rows before stopping
                    if (rowNum > DATA_START_ROW + 200) break;
                    continue;
                }

                // Skip if no SAPS station
                if (string.IsNullOrEmpty(firstVal)) { rowNum++; continue; }

                try
                {
                    var parsed = ParseRow(row, defaultProvince);
                    if (parsed == null) { result.SkippedCount++; rowNum++; continue; }

                    // ── Duplicate check: SAPS + AR No + Date ──
                    // Use CrNo (SAPS-ARNO) + date as the unique key.
                    // CAS is often null so don't rely on it alone.
                    var isDuplicate = await _context.Crashes.AnyAsync(c =>
                        c.CrNo == parsed.Crash.CrNo &&
                        c.CrashDate == parsed.Crash.CrashDate);

                    if (isDuplicate)
                    {
                        result.DuplicateCount++;
                        result.Warnings.Add(
                            $"Row {rowNum}: Duplicate skipped — " +
                            $"{parsed.Crash.CrNo} on {parsed.Crash.CrashDate}");
                        rowNum++;
                        continue;
                    }

                    // ── Save inside a transaction so a person ──
                    // summary failure doesn't leave an orphan crash
                    using var tx = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        // 1. Crash
                        _context.Crashes.Add(parsed.Crash);
                        await _context.SaveChangesAsync();

                        // 2. Location
                        parsed.Location.CrashId = parsed.Crash.CrashId;
                        _context.CrashLocations.Add(parsed.Location);

                        // 3. Conditions
                        parsed.Condition.CrashId = parsed.Crash.CrashId;
                        _context.CrashConditions.Add(parsed.Condition);

                        // 4. Person summaries
                        // Use the unknownPerson placeholder for the required FK.
                        foreach (var cp in parsed.PersonSummaries)
                        {
                            cp.CrashId = parsed.Crash.CrashId;
                            cp.PersonId = unknownPerson.PersonId;
                            _context.CrashPeople.Add(cp);
                        }

                        await _context.SaveChangesAsync();
                        await tx.CommitAsync();

                        result.ImportedCount++;
                    }
                    catch (Exception innerEx)
                    {
                        await tx.RollbackAsync();
                        result.Warnings.Add(
                            $"Row {rowNum}: Rolled back — {innerEx.InnerException?.Message ?? innerEx.Message}");
                        result.SkippedCount++;
                    }
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"Row {rowNum}: Parse error — {ex.Message}");
                    result.SkippedCount++;
                }

                rowNum++;
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add($"Failed to open workbook: {ex.Message}");
        }

        return result;
    }

    // ── Parse one data row ────────────────────────────────────
    private ParsedRow? ParseRow(IXLRow row, string defaultProvince)
    {
        var saps = row.Cell(COL_SAPS + 1).GetString().Trim();
        var arNoRaw = row.Cell(COL_AR_NO + 1).GetString().Trim();
        var cas = row.Cell(COL_CAS + 1).GetString().Trim();
        var dateRaw = row.Cell(COL_DATE + 1).GetString().Trim();
        var day = row.Cell(COL_DAY + 1).GetString().Trim();
        var timeRaw = row.Cell(COL_TIME + 1).GetString().Trim();
        var route = row.Cell(COL_ROUTE + 1).GetString().Trim();
        var location = row.Cell(COL_LOCATION + 1).GetString().Trim();
        var crashType = row.Cell(COL_CRASH_TYPE + 1).GetString().Trim();
        var vehicles = row.Cell(COL_VEHICLES + 1).GetString().Trim();

        if (string.IsNullOrEmpty(saps)) return null;

        // ── Parse date ────────────────────────────────────────
        // Workbook uses dd/MM (e.g. "01/02") or dd/MM/yyyy
        var crashDate = DateOnly.FromDateTime(DateTime.Today);
        if (!string.IsNullOrEmpty(dateRaw))
        {
            if (!DateOnly.TryParseExact(dateRaw, "dd/MM/yyyy",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out crashDate))
            {
                if (DateOnly.TryParseExact(dateRaw, "dd/MM",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var partial))
                {
                    // Infer year from the sheet name or today
                    crashDate = new DateOnly(DateTime.Today.Year, partial.Month, partial.Day);
                }
            }
        }

        // ── Parse time (format: "20H00") ──────────────────────
        TimeOnly? crashTime = null;
        if (!string.IsNullOrEmpty(timeRaw))
        {
            var norm = timeRaw.ToUpper().Replace("H", ":");
            if (TimeOnly.TryParse(norm, out var t)) crashTime = t;
        }

        // ── AR number — keep as-is (can be numeric or string)
        var arNo = string.IsNullOrEmpty(arNoRaw) ? null : arNoRaw;

        // ── Build Crash record ────────────────────────────────
        var crash = new Crash
        {
            CasNo = string.IsNullOrEmpty(cas) ? null : cas,
            CrNo = arNo != null ? $"{saps}-{arNo}" : saps,
            CrashDate = crashDate,
            CrashTime = crashTime,
            ProvinceCode = defaultProvince,    // MP = Mpumalanga
            RoadNumber = string.IsNullOrEmpty(route) ? null : route,
            NoOfVehiclesInvolved = (byte)CountVehicles(vehicles),
            BriefDescription = BuildDescription(day, vehicles)
        };

        // ── Location ──────────────────────────────────────────
        var loc = new CrashLocation
        {
            StreetRoadName = string.IsNullOrEmpty(location) ? null : location,
            RoadFunctionalClassification = string.IsNullOrEmpty(route) ? null : route
        };

        // ── Conditions ────────────────────────────────────────
        var cond = new CrashCondition
        {
            CrashType = NormaliseCrashType(crashType)
        };

        // ── Person summaries (one row per injured person) ─────
        // Columns map: Fatal D/P/PD/C, Serious D/P/PD/C, Slight D/P/PD/C
        var persons = new List<CrashPerson>();

        AddSummaries(persons, GetInt(row, COL_FATAL_D), "Driver", "Fatal");
        AddSummaries(persons, GetInt(row, COL_FATAL_P), "Passenger", "Fatal");
        AddSummaries(persons, GetInt(row, COL_FATAL_PD), "Pedestrian", "Fatal");
        AddSummaries(persons, GetInt(row, COL_FATAL_C), "Bicyclist", "Fatal");
        AddSummaries(persons, GetInt(row, COL_SERIOUS_D), "Driver", "Serious");
        AddSummaries(persons, GetInt(row, COL_SERIOUS_P), "Passenger", "Serious");
        AddSummaries(persons, GetInt(row, COL_SERIOUS_PD), "Pedestrian", "Serious");
        AddSummaries(persons, GetInt(row, COL_SERIOUS_C), "Bicyclist", "Serious");
        AddSummaries(persons, GetInt(row, COL_SLIGHT_D), "Driver", "Slight");
        AddSummaries(persons, GetInt(row, COL_SLIGHT_P), "Passenger", "Slight");
        AddSummaries(persons, GetInt(row, COL_SLIGHT_PD), "Pedestrian", "Slight");
        AddSummaries(persons, GetInt(row, COL_SLIGHT_C), "Bicyclist", "Slight");

        return new ParsedRow
        {
            Crash = crash,
            Location = loc,
            Condition = cond,
            PersonSummaries = persons
        };
    }

    // ── Create or reuse a single "Unknown (Imported)" person ─
    private async Task<Person> GetOrCreateUnknownPersonAsync()
    {
        const string unknownSurname = "UNKNOWN";
        const string unknownFullNames = "IMPORTED RECORD";

        var existing = await _context.Persons
            .FirstOrDefaultAsync(p =>
                p.Surname == unknownSurname &&
                p.FullNames == unknownFullNames);

        if (existing != null) return existing;

        var person = new Person
        {
            IdType = "Other",
            Surname = unknownSurname,
            FullNames = unknownFullNames
        };
        _context.Persons.Add(person);
        await _context.SaveChangesAsync();
        return person;
    }

    // ── Helpers ───────────────────────────────────────────────
    private static void AddSummaries(
        List<CrashPerson> list, int count, string role, string severity)
    {
        for (int i = 0; i < count; i++)
            list.Add(new CrashPerson { Role = role, SeverityOfInjury = severity });
    }

    private static int GetInt(IXLRow row, int colIndex)
    {
        var cell = row.Cell(colIndex + 1);
        if (cell.IsEmpty()) return 0;
        if (cell.TryGetValue<int>(out var vi)) return vi;
        if (cell.TryGetValue<double>(out var vd)) return (int)vd;
        return 0;
    }

    private static int CountVehicles(string vehicleStr)
    {
        if (string.IsNullOrEmpty(vehicleStr)) return 1;
        return vehicleStr.Split('/').Length;
    }

    private static string BuildDescription(string day, string vehicles)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(day)) parts.Add($"Day: {day}");
        if (!string.IsNullOrEmpty(vehicles)) parts.Add($"Vehicles: {vehicles}");
        return string.Join(". ", parts);
    }

    private static string? NormaliseCrashType(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        return raw.Trim().ToUpper() switch
        {
            "HEAD ON" => "Head on",
            "HEAD REAR" => "Head/rear end",
            "REAR END" => "Head/rear end",
            "SIDE SWIPE" => "Sideswipe - same direction",
            "PEDESTRIAN" => "Crash with pedestrian",
            "OVERTURNED" => "Overturned",
            "LOST CONTROL" => "Single vehicle left road",
            "ROLLOVER" => "Overturned",
            _ => raw.Trim()
        };
    }

    // ── Internal DTO ─────────────────────────────────────────
    private class ParsedRow
    {
        public Crash Crash { get; set; } = null!;
        public CrashLocation Location { get; set; } = null!;
        public CrashCondition Condition { get; set; } = null!;
        public List<CrashPerson> PersonSummaries { get; set; } = new();
    }
}