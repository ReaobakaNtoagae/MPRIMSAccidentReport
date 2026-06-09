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

    // Non-vehicle types
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

    public async Task<ImportResult> ImportAsync(Stream stream, string fileName, string province = "MP")
    {
        var result = new ImportResult { FileName = fileName };

        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();

        // Find the actual data start row
        int headerRow = FindHeaderRow(ws);
        if (headerRow == -1)
        {
            result.AddError("Could not find header row with SAPS/AR NO/CAS columns");
            return result;
        }

        // Get all rows after header
        var allRows = ws.RowsUsed().Skip(headerRow).ToList();

        var dataRows = new List<IXLRow>();
        var summaryRows = new List<IXLRow>();
        bool inSummary = false;

        foreach (var row in allRows)
        {
            var saps = row.Cell(1).GetString().Trim();

            // Skip empty rows or rows with "TOTAL" in SAPS column
            if (string.IsNullOrWhiteSpace(saps) || saps.Equals("TOTAL", StringComparison.OrdinalIgnoreCase))
            {
                inSummary = true;
                continue;
            }

            // Check for summary section indicators
            var col7 = row.Cell(8).GetString().Trim();
            var col0 = row.Cell(1).GetString().Trim();

            if (col7.Equals("GRAND TOTAL", StringComparison.OrdinalIgnoreCase) ||
                col0.StartsWith("TOTAL:", StringComparison.OrdinalIgnoreCase))
            {
                inSummary = true;
                continue;
            }

            if (!inSummary)
            {
                // Skip rows that are part of the summary section
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

        // Parse summary section
        result.Demographics = ParseDemographics(summaryRows);

        // Create or get default vehicle for the foreign key constraint
        var defaultVehicle = await GetOrCreateDefaultVehicle();
        var defaultVehicleId = defaultVehicle.VehicleId;

        // Import data rows
        var existingCrNos = await _context.Crashes
            .Select(c => c.CrNo)
            .Where(c => c != null)
            .ToHashSetAsync();

        foreach (var row in dataRows)
        {
            result.TotalRows++;
            try
            {
                var crash = ParseDataRow(row, province, defaultVehicleId);
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

    private int FindHeaderRow(IXLWorksheet ws)
    {
        foreach (var row in ws.RowsUsed())
        {
            var cell1 = row.Cell(1).GetString().Trim();
            var cell2 = row.Cell(2).GetString().Trim();
            var cell3 = row.Cell(3).GetString().Trim();

            if (cell1 == "SAPS" && cell2 == "AR NO" && cell3 == "CAS")
                return row.RowNumber();
        }
        return -1;
    }

    private Crash? ParseDataRow(IXLRow row, string province, int defaultVehicleId)
    {
        var saps = row.Cell(1).GetString().Trim().ToUpper();
        var arNo = row.Cell(2).GetString().Trim();
        var casNo = row.Cell(3).GetString().Trim();
        var dateRaw = row.Cell(4).GetString().Trim();
        var day = row.Cell(5).GetString().Trim();
        var timeRaw = row.Cell(6).GetString().Trim();
        var route = row.Cell(7).GetString().Trim().ToUpper();
        var location = row.Cell(8).GetString().Trim();

        System.Diagnostics.Debug.WriteLine($"Row {row.RowNumber()}: ROUTE='{route}', LOCATION='{location}'");


        var crashType = row.Cell(9).GetString().Trim().ToUpper();

        var vehicles = row.Cell(24).GetString().Trim();
        var vehicleEntries = ParseVehicleEntries(vehicles);
        var vehicleCount = vehicleEntries.Count;

        if (string.IsNullOrEmpty(saps)) return null;

        // Parse date
        DateOnly crashDate = DateOnly.FromDateTime(DateTime.Today);
        if (!string.IsNullOrEmpty(dateRaw))
        {
            var parts = dateRaw.Replace('-', '/').Split('/');
            if (parts.Length >= 2)
            {
                if (int.TryParse(parts[0], out var dd) && int.TryParse(parts[1], out var mm))
                {
                    var year = DateTime.Today.Year;
                    if (parts.Length >= 3 && int.TryParse(parts[2], out var yyyy))
                        year = yyyy;

                    dd = Math.Min(dd, DateTime.DaysInMonth(year, mm));
                    crashDate = new DateOnly(year, mm, dd);
                }
            }
        }

        // Parse time
        TimeOnly? crashTime = null;
        if (!string.IsNullOrEmpty(timeRaw))
        {
            var norm = timeRaw.ToUpper().Replace("H", ":");
            if (TimeOnly.TryParse(norm, out var t)) crashTime = t;
        }

        // Injury counts
        int fatalD = IntCell(row, 10);
        int fatalP = IntCell(row, 11);
        int fatalPD = IntCell(row, 12);
        int fatalC = IntCell(row, 13);
        int fatalM = IntCell(row, 14);
        int fatalF = IntCell(row, 15);

        int serD = IntCell(row, 16);
        int serP = IntCell(row, 17);
        int serPD = IntCell(row, 18);
        int serC = IntCell(row, 19);

        int sliD = IntCell(row, 20);
        int sliP = IntCell(row, 21);
        int sliPD = IntCell(row, 22);
        int sliC = IntCell(row, 23);

        // Build CrNo
        var crNo = string.IsNullOrEmpty(arNo) ? saps : $"{saps}-{arNo}";

        // Create Crash object
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

        // Create Crash Location Record
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

        // ── CREATE CRASH VEHICLE RECORDS ─────────────────────────
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

        // Create Crash Condition Record
        if (!string.IsNullOrEmpty(crashType))
        {
            crash.CrashConditions.Add(new CrashCondition
            {
                CrashType = crashType
            });
        }

        // Create Crash People (Victims)
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

        // Special case: pedestrian only (no vehicles)
        if (s.Equals("P/D", StringComparison.OrdinalIgnoreCase))
            return entries;

        // Split on '/' to get individual entries
        var parts = s.Split('/')
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        int vehicleIndex = 1;
        foreach (var part in parts)
        {
            // Skip non-vehicle entries
            if (NonVehicleTypes.Contains(part))
                continue;

            // Clean up the vehicle type
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

            // Assign gender for fatal victims when gender data is available
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

            // For drivers, associate with the first vehicle if available
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
        }; // handle later

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

    private ImportDemographics ParseDemographics(List<IXLRow> summaryRows)
    {
        var demo = new ImportDemographics();
        for (int i = 0; i < summaryRows.Count; i++)
        {
            var row = summaryRows[i];
            var label = row.Cell(1).GetString().Trim().ToUpper();
            if (label == "AGE")
            {
                for (int j = i + 1; j < summaryRows.Count; j++)
                {
                    var valueRow = summaryRows[j];
                    var age0_7 = IntCell(valueRow, 2);
                    var age8_12 = IntCell(valueRow, 3);
                    var age13_18 = IntCell(valueRow, 4);
                    var age19_35 = IntCell(valueRow, 5);
                    var age36Plus = IntCell(valueRow, 6);
                    if (age0_7 + age8_12 + age13_18 + age19_35 + age36Plus > 0)
                    {
                        demo.Age0to7 = age0_7;
                        demo.Age8to12 = age8_12;
                        demo.Age13to18 = age13_18;
                        demo.Age19to35 = age19_35;
                        demo.Age36Plus = age36Plus;
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
            else if (label == "CYLIST" || label == "CYCLIST")
            {
                demo.CyclistMale = IntCell(row, 2);
                demo.CyclistFemale = IntCell(row, 3);
            }
            else if (label == "RACE")
            {
                for (int j = i + 1; j < summaryRows.Count; j++)
                {
                    var valueRow = summaryRows[j];
                    var black = IntCell(valueRow, 2);
                    var coloured = IntCell(valueRow, 3);
                    var white = IntCell(valueRow, 4);
                    var indian = IntCell(valueRow, 5);
                    var other = IntCell(valueRow, 6);
                    if (black + coloured + white + indian + other > 0)
                    {
                        demo.RaceBlack = black;
                        demo.RaceColoured = coloured;
                        demo.RaceWhite = white;
                        demo.RaceIndian = indian;
                        demo.RaceOther = other;
                        break;
                    }
                }
            }
        }

        // Log demographic totals
        System.Diagnostics.Debug.WriteLine("=== ParseDemographics Results ===");
        System.Diagnostics.Debug.WriteLine($"  Age      : 0-7={demo.Age0to7}, 8-12={demo.Age8to12}, 13-18={demo.Age13to18}, 19-35={demo.Age19to35}, 36+={demo.Age36Plus}  (total={(demo.Age0to7 + demo.Age8to12 + demo.Age13to18 + demo.Age19to35 + demo.Age36Plus)})");
        System.Diagnostics.Debug.WriteLine($"  Driver   : M={demo.DriverMale}, F={demo.DriverFemale}  (total={(demo.DriverMale + demo.DriverFemale)})");
        System.Diagnostics.Debug.WriteLine($"  Passenger: M={demo.PassengerMale}, F={demo.PassengerFemale}  (total={(demo.PassengerMale + demo.PassengerFemale)})");
        System.Diagnostics.Debug.WriteLine($"  Pedestrian: M={demo.PedestrianMale}, F={demo.PedestrianFemale}  (total={(demo.PedestrianMale + demo.PedestrianFemale)})");
        System.Diagnostics.Debug.WriteLine($"  Cyclist  : M={demo.CyclistMale}, F={demo.CyclistFemale}  (total={(demo.CyclistMale + demo.CyclistFemale)})");
        System.Diagnostics.Debug.WriteLine($"  Race     : Black={demo.RaceBlack}, Coloured={demo.RaceColoured}, White={demo.RaceWhite}, Indian={demo.RaceIndian}, Other={demo.RaceOther}  (total={(demo.RaceBlack + demo.RaceColoured + demo.RaceWhite + demo.RaceIndian + demo.RaceOther)})");
        System.Diagnostics.Debug.WriteLine("=================================");

        return demo;
    }
    private static int IntCell(IXLRow row, int col)
    {
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

// Result classes remain the same
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