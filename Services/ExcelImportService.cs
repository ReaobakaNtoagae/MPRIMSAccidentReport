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

    // ── Cells in col A (SAPS) that signal end of data ─────────
    private static readonly HashSet<string> StopWords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "TOTAL", "GRAND TOTAL", "SUBTOTAL"
        };

    public ExcelImportService(AppDbContext context,
        ILogger<ExcelImportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ImportResult> ImportAsync(
        Stream stream, string fileName, string province = "MP")
    {
        var result = new ImportResult { FileName = fileName };

        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();

        // ── Load ALL rows into memory once ────────────────────
        var rows = new List<IXLRow>();
        foreach (var row in ws.RowsUsed())
            rows.Add(row);

        // ── Split into data section and summary section ───────
        var dataRows = new List<IXLRow>();
        var summaryRows = new List<IXLRow>();
        bool inSummary = false;

        foreach (var row in rows)
        {
            var rowNum = row.RowNumber();
            if (rowNum < 7) continue;  // skip header rows 1-6

            var col0 = row.Cell(1).GetString().Trim();  // SAPS column
            var col8 = row.Cell(9).GetString().Trim();  // ACCIDENT TYPE column
            var col7 = row.Cell(8).GetString().Trim();  // LOCATION column

            // ── Detect end of data / start of summary ─────────
            // Row 128 is blank, Row 129 col9="TOTAL", Row 130 col1 starts with "TOTAL:"
            if (!inSummary)
            {
                if (col8.Equals("TOTAL", StringComparison.OrdinalIgnoreCase) ||
                    col7.Equals("GRAND TOTAL", StringComparison.OrdinalIgnoreCase) ||
                    col0.StartsWith("TOTAL", StringComparison.OrdinalIgnoreCase))
                {
                    inSummary = true;
                    continue;
                }

                // Skip blank rows inside data section
                if (string.IsNullOrWhiteSpace(col0)) continue;

                dataRows.Add(row);
            }
            else
            {
                summaryRows.Add(row);
            }
        }

        // ── Parse summary section ─────────────────────────────
        result.Demographics = ParseDemographics(summaryRows);

        // ── Import data rows ──────────────────────────────────
        // Load lookup for duplicate checking
        var existingCrNos = await _context.Crashes
            .Select(c => c.CrNo)
            .Where(c => c != null)
            .ToHashSetAsync();

        foreach (var row in dataRows)
        {
            result.TotalRows++;
            try
            {
                var crash = ParseDataRow(row, province);
                if (crash == null)
                {
                    result.Skipped++;
                    result.AddWarning($"Row {row.RowNumber()}: could not parse — skipped.");
                    continue;
                }

                // Duplicate check on CrNo
                if (crash.CrNo != null && existingCrNos.Contains(crash.CrNo))
                {
                    result.Skipped++;
                    result.AddWarning($"Row {row.RowNumber()}: duplicate CrNo '{crash.CrNo}' — skipped.");
                    continue;
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

    // ─────────────────────────────────────────────────────────
    // Parse a single data row → Crash entity
    // ─────────────────────────────────────────────────────────
    private static Crash? ParseDataRow(IXLRow row, string province)
    {

        var saps = row.Cell(1).GetString().Trim().ToUpper();
        var arNo = row.Cell(2).GetString().Trim();
        var casNo = row.Cell(3).GetString().Trim();
        var dateRaw = row.Cell(4).GetString().Trim();
        var timeRaw = row.Cell(6).GetString().Trim();
        var route = row.Cell(7).GetString().Trim().ToUpper();
        var location = row.Cell(8).GetString().Trim();
        var crashType = row.Cell(9).GetString().Trim().ToUpper();
        var vehicles = row.Cell(24).GetString().Trim();
        var vehicleCount = CountVehicles(vehicles);

        if (string.IsNullOrEmpty(saps)) return null;

        
        DateOnly crashDate = DateOnly.FromDateTime(DateTime.Today);
        if (!string.IsNullOrEmpty(dateRaw))
        {
            var parts = dateRaw.Replace('-', '/').Split('/');
            if (parts.Length >= 2 &&
                int.TryParse(parts[0], out var dd) &&
                int.TryParse(parts[1], out var mm))
            {
                var year = DateTime.Today.Year;

                dd = Math.Min(dd, DateTime.DaysInMonth(year, mm));
                crashDate = new DateOnly(year, mm, dd);
            }
        }


        TimeOnly? crashTime = null;
        if (!string.IsNullOrEmpty(timeRaw))
        {
            var norm = timeRaw.ToUpper().Replace("H", ":");
            if (TimeOnly.TryParse(norm, out var t)) crashTime = t;
        }

        // ── Injury counts (for creating CrashPerson placeholders) ─
        int fatalD = IntCell(row, 10), fatalP = IntCell(row, 11),
            fatalPD = IntCell(row, 12), fatalC = IntCell(row, 13);
        int fatalM = IntCell(row, 14), fatalF = IntCell(row, 15);
        int serD = IntCell(row, 16), serP = IntCell(row, 17),
            serPD = IntCell(row, 18), serC = IntCell(row, 19);
        int sliD = IntCell(row, 20), sliP = IntCell(row, 21),
            sliPD = IntCell(row, 22), sliC = IntCell(row, 23);

        // ── Build CrNo ─────────────────────────────────────────
        var crNo = string.IsNullOrEmpty(arNo)
            ? saps : $"{saps}-{arNo}";

        var crash = new Crash
        {
            CrNo = crNo,
            CasNo = string.IsNullOrEmpty(casNo) ? null : casNo,
            ProvinceCode = province,
            CrashDate = crashDate,
            CrashTime = crashTime,
            RoadNumber = string.IsNullOrEmpty(route) ? null : route,
            NoOfVehiclesInvolved = (byte)Math.Min(vehicleCount, 255),  
            CreatedAt = DateTime.UtcNow
        };

 
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


    private static void AddPersons(Crash crash,
        string role, string severity, int count,
        int maleTotal, int femaleTotal, int totalFatal)
    {
        if (count <= 0) return;

        for (int i = 0; i < count; i++)
        {
            // Assign gender from M/F totals when this is a fatal row
            // and we have gender data. Assign Male first, then Female.
            string? gender = null;
            if (severity == "Fatal" && totalFatal > 0 &&
                (maleTotal > 0 || femaleTotal > 0))
            {
                // Simple allocation: fill males first
                var assigned = crash.CrashPeople
                    .Count(p => p.SeverityOfInjury == "Fatal" && p.Person?.Gender != null);
                if (assigned < maleTotal)
                    gender = "Male";
                else if (assigned < maleTotal + femaleTotal)
                    gender = "Female";
            }

            var person = new Person
            {
                Surname = "IMPORTED",
                FullNames = "RECORD",
                Gender = gender,
                IdType = "UNKNOWN"
            };

            crash.CrashPeople.Add(new CrashPerson
            {
                Person = person,
                Role = role,
                SeverityOfInjury = severity
            });
        }
    }

 
    private static ImportDemographics ParseDemographics(List<IXLRow> summaryRows)
    {
        var demo = new ImportDemographics();

        foreach (var row in summaryRows)
        {
            var label = row.Cell(1).GetString().Trim().ToUpper();

            // ── AGE distribution ──────────────────────────────
            // Row 134: headers — AGE | 0-7 | 08-12 | 13-18 | 19-35 | 36+
            // Row 135: values  — (blank) | val | val | val | val | val
            if (label == "AGE")
            {
                // The next non-blank row holds the values
                var idx = summaryRows.IndexOf(row);
                for (int j = idx + 1; j < summaryRows.Count; j++)
                {
                    var vRow = summaryRows[j];
                    var vLabel = vRow.Cell(1).GetString().Trim().ToUpper();
                    if (vLabel == "TOTAL" || vLabel == "GRAND TOTAL") break;

                    // Values are in cols B-F (ClosedXML 2-6)
                    var a = IntCell(vRow, 2);  // 0-7
                    var b = IntCell(vRow, 3);  // 08-12
                    var c = IntCell(vRow, 4);  // 13-18
                    var d = IntCell(vRow, 5);  // 19-35
                    var e = IntCell(vRow, 6);  // 36+

                    if (a + b + c + d + e > 0)
                    {
                        demo.Age0to7 = a;
                        demo.Age8to12 = b;
                        demo.Age13to18 = c;
                        demo.Age19to35 = d;
                        demo.Age36Plus = e;
                        break;
                    }
                }
            }

            
            else if (label == "DRIVER")
            {
                demo.DriverMale = IntCell(row, 2);
                demo.DriverFemale = IntCell(row, 3);
            }
            else if (label == "PASSENGER")
            {
                demo.PassengerMale = IntCell(row, 2);
                demo.PassengerFemale = IntCell(row, 3);
            }
            else if (label == "PEDESTRIAN")
            {
                demo.PedestrianMale = IntCell(row, 2);
                demo.PedestrianFemale = IntCell(row, 3);
            }
            else if (label is "CYCLIST" or "CYLIST")
            {
                demo.CyclistMale = IntCell(row, 2);
                demo.CyclistFemale = IntCell(row, 3);
            }

            // ── Race distribution ─────────────────────────────
            // Row 147: RACE | B | C | W | I | O
            // Row 148: vals in cols B-F
            else if (label == "RACE")
            {
                var idx = summaryRows.IndexOf(row);
                if (idx + 1 < summaryRows.Count)
                {
                    var vRow = summaryRows[idx + 1];
                    demo.RaceBlack = IntCell(vRow, 2);  // B
                    demo.RaceColoured = IntCell(vRow, 3);  // C
                    demo.RaceWhite = IntCell(vRow, 4);  // W
                    demo.RaceIndian = IntCell(vRow, 5);  // I
                    demo.RaceOther = IntCell(vRow, 6);  // O
                }
            }
        }

        return demo;
    }


    private static int CountVehicles(string vehiclesStr)
    {
        if (string.IsNullOrWhiteSpace(vehiclesStr))
            return 0;

        var s = vehiclesStr.Trim().TrimEnd('`').Trim();

        // Special case: if it's just "P/D" (pedestrian only), no vehicles
        if (s.Equals("P/D", StringComparison.OrdinalIgnoreCase))
            return 0;

        // Split on '/' to get individual vehicle entries
        var parts = s.Split('/')
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        // Define what counts as a VEHICLE (not a person/cyclist)
        var nonVehicleTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "P/D",      // Pedestrian
        "B/C",      // Bicycle/Cyclist
        "HIT N RUN" // This is an event, not a vehicle
    };

        // Count only the parts that are actual vehicles
        var vehicleCount = parts.Count(p => !nonVehicleTypes.Contains(p));

        // If we found zero vehicles but there were parts, check if all were vehicles
        // (e.g., if someone put "M/C" which IS a vehicle)
        if (vehicleCount == 0 && parts.Any())
        {
            // Motorcycles (M/C) ARE vehicles - count them
            vehicleCount = parts.Count(p => p.Equals("M/C", StringComparison.OrdinalIgnoreCase));
        }

        // If still zero but there were parts, something's wrong - log and default to 1
        if (vehicleCount == 0 && parts.Any())
        {
            // This might be a vehicle type we don't recognize
            // You could log this for investigation
            return parts.Count; // Count everything as vehicles as fallback
        }

        return vehicleCount;
    }
    private static int IntCell(IXLRow row, int col)
    {
        try
        {
            var cell = row.Cell(col);
            if (cell.IsEmpty()) return 0;
            if (cell.TryGetValue(out int i)) return i;
            if (int.TryParse(cell.GetString().Trim(), out var p)) return p;
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

    public void AddWarning(string msg) { if (Warnings.Count < 50) Warnings.Add(msg); }
    public void AddError(string msg) { if (ErrorMessages.Count < 50) ErrorMessages.Add(msg); }
}

public class ImportDemographics
{
    // ── Age distribution ──────────────────────────────────────
    public int Age0to7 { get; set; }
    public int Age8to12 { get; set; }
    public int Age13to18 { get; set; }
    public int Age19to35 { get; set; }
    public int Age36Plus { get; set; }
    public int AgeTotal => Age0to7 + Age8to12 + Age13to18 + Age19to35 + Age36Plus;

    // ── Gender per victim type (fatal victims only) ───────────
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

    // ── Race (totals only — no per-crash link in source data) ──
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