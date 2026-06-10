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


    public async Task<ImportResult> ImportAsync(Stream stream, string fileName, string province = "MP")
    {
        var result = new ImportResult { FileName = fileName };

        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();

          DebugFindRaceTable(ws);
       
        int headerRow = FindHeaderRow(ws);
        if (headerRow == -1)
        {
            result.AddError("Could not find header row with SAPS/AR NO/CAS columns");
            return result;
        }

        
        var allRows = ws.RowsUsed().Skip(headerRow).ToList();

        var dataRows = new List<IXLRow>();
        var summaryRows = new List<IXLRow>();
        bool inSummary = false;

        foreach (var row in allRows)
        {
            var saps = row.Cell(1).GetString().Trim();


            if (string.IsNullOrWhiteSpace(saps) || saps.Equals("TOTAL", StringComparison.OrdinalIgnoreCase))
            {
                if (inSummary)
                    summaryRows.Add(row); 
                else
                    inSummary = true;
                continue;
            }


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


        await SaveDemographicsAsync(result.Demographics, fileName, province);




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

        
        TimeOnly? crashTime = null;
        if (!string.IsNullOrEmpty(timeRaw))
        {
            var norm = timeRaw.ToUpper().Replace("H", ":");
            if (TimeOnly.TryParse(norm, out var t)) crashTime = t;
        }

        
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
        DebugRaceNumbers(data);

        _logger.LogInformation("Parsing demographics from {RowCount} summary rows", data.Count);

        _logger.LogError("=== SUMMARY ROW LABELS ===");
        for (int di = 0; di < data.Count; di++)
        {
            _logger.LogError("Row {Index}: Col0='{Col0}' Col1='{Col1}' Col2='{Col2}' Col3='{Col3}'",
                di, data[di][0], data[di][1], data[di][2], data[di][3]);
        }
        _logger.LogError("=== END SUMMARY LABELS ===");


        for (int i = 0; i < data.Count; i++)
        {
            if (data[i].Length == 0) continue;

            var label = data[i][0].ToUpper();

            switch (label)
            {
                case "DRIVER":
                    int.TryParse(data[i][1], out int driverMale);
                    demo.DriverMale = driverMale;
                    _logger.LogInformation("Driver Male: {Male}", driverMale);
                    break;

                case "PASSENGER":
                    int.TryParse(data[i][1], out int passengerMale);
                    int.TryParse(data[i][2], out int passengerFemale);
                    demo.PassengerMale = passengerMale;
                    demo.PassengerFemale = passengerFemale;
                    _logger.LogInformation("Passenger M/F: {Male}/{Female}", passengerMale, passengerFemale);
                    break;

                case "PEDESTRIAN":
                    int.TryParse(data[i][1], out int pedestrianMale);
                    int.TryParse(data[i][2], out int pedestrianFemale);
                    demo.PedestrianMale = pedestrianMale;
                    demo.PedestrianFemale = pedestrianFemale;
                    _logger.LogInformation("Pedestrian M/F: {Male}/{Female}", pedestrianMale, pedestrianFemale);
                    break;

                case "CYLIST":
                case "CYCLIST":
                    int.TryParse(data[i][1], out int cyclistMale);
                    int.TryParse(data[i][2], out int cyclistFemale);
                    demo.CyclistMale = cyclistMale;
                    demo.CyclistFemale = cyclistFemale;
                    _logger.LogInformation("Cyclist M/F: {Male}/{Female}", cyclistMale, cyclistFemale);
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
                        _logger.LogInformation("Age: 0-7={A1}, 8-12={A2}, 13-18={A3}, 19-35={A4}, 36+={A5}",
                            demo.Age0to7, demo.Age8to12, demo.Age13to18, demo.Age19to35, demo.Age36Plus);
                    }
                    break;

                case "RACE":
                    _logger.LogInformation("Found RACE row at index {RowIndex}", i);

                    // The race labels are in this row (B, C, W, I, O)
                    // The actual numbers are likely in the NEXT row (i + 1)
                    if (i + 1 < data.Count)
                    {
                        var raceDataRow = data[i + 1];

                        // Try to parse race numbers from the next row
                        int black = 0, coloured = 0, white = 0, indian = 0, other = 0;

                        // Numbers should be in columns 1-5 of the next row
                        int.TryParse(raceDataRow[1], out black);
                        int.TryParse(raceDataRow[2], out coloured);
                        int.TryParse(raceDataRow[3], out white);
                        int.TryParse(raceDataRow[4], out indian);
                        int.TryParse(raceDataRow[5], out other);

                        // If no numbers in next row, maybe numbers are in the same row after labels
                        if (black == 0 && coloured == 0 && white == 0 && indian == 0 && other == 0)
                        {
                            // Try same row, but skip the label columns
                            int.TryParse(data[i][6], out black);  // Column G might have numbers
                            int.TryParse(data[i][7], out coloured);
                            int.TryParse(data[i][8], out white);
                        }

                        demo.RaceBlack = black;
                        demo.RaceColoured = coloured;
                        demo.RaceWhite = white;
                        demo.RaceIndian = indian;
                        demo.RaceOther = other;

                        _logger.LogInformation("Race data parsed - B:{B}, C:{C}, W:{W}, I:{I}, O:{O}",
                            demo.RaceBlack, demo.RaceColoured, demo.RaceWhite, demo.RaceIndian, demo.RaceOther);
                    }
                    else
                    {
                        _logger.LogWarning("No data row found after RACE header");
                    }
                    break;

            }
        }

        _logger.LogInformation("Demographics Complete - Age Total: {AgeTotal}, Gender Total: {GenderTotal}, Race Total: {RaceTotal}",
            demo.AgeTotal, demo.GenderTotal, demo.RaceTotal);

        return demo;
    }

    private void ParseRaceDataFromWorksheet(IXLWorksheet ws, ImportDemographics demo)
    {
        _logger.LogInformation("Searching entire worksheet for race data...");

        
        var allRows = ws.RowsUsed().ToList();

        for (int rowIdx = 0; rowIdx < allRows.Count; rowIdx++)
        {
            var row = allRows[rowIdx];

            
            for (int col = 1; col <= 20; col++) 
            {
                var cellValue = row.Cell(col).GetString().Trim().ToUpper();

                if (cellValue == "RACE")
                {
                    _logger.LogInformation($"Found RACE at Excel Row {row.RowNumber()}, Column {col}");

                    
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
                            _logger.LogInformation($"Race B (Black): {black} at column {raceBCol}");
                        }

                        if (raceCCol != -1)
                        {
                            int.TryParse(dataRow.Cell(raceCCol).GetString().Trim(), out int coloured);
                            demo.RaceColoured = coloured;
                            _logger.LogInformation($"Race C (Coloured): {coloured} at column {raceCCol}");
                        }

                        if (raceWCol != -1)
                        {
                            int.TryParse(dataRow.Cell(raceWCol).GetString().Trim(), out int white);
                            demo.RaceWhite = white;
                            _logger.LogInformation($"Race W (White): {white} at column {raceWCol}");
                        }

                        if (raceICol != -1)
                        {
                            int.TryParse(dataRow.Cell(raceICol).GetString().Trim(), out int indian);
                            demo.RaceIndian = indian;
                            _logger.LogInformation($"Race I (Indian): {indian} at column {raceICol}");
                        }

                        if (raceOCol != -1)
                        {
                            int.TryParse(dataRow.Cell(raceOCol).GetString().Trim(), out int other);
                            demo.RaceOther = other;
                            _logger.LogInformation($"Race O (Other): {other} at column {raceOCol}");
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
        _logger.LogError("=== SEARCHING FOR RACE TABLE ===");

        var allRows = ws.RowsUsed().ToList();

        for (int rowIdx = 0; rowIdx < Math.Min(allRows.Count, 50); rowIdx++)
        {
            var row = allRows[rowIdx];

            for (int col = 1; col <= 60; col++)
            {
                var cellValue = row.Cell(col).GetString().Trim().ToUpper();

                if (cellValue == "RACE")
                {
                    _logger.LogError($"Found 'RACE' at Row {row.RowNumber()}, Col {col}");

                    
                    _logger.LogError($"Row {row.RowNumber()} (RACE row):");
                    for (int c = col; c <= col + 10; c++)
                    {
                        var val = row.Cell(c).GetString().Trim();
                        if (!string.IsNullOrEmpty(val))
                        {
                            _logger.LogError($"  Col{c}: '{val}'");
                        }
                    }

                 
                    if (rowIdx + 1 < allRows.Count)
                    {
                        var nextRow = allRows[rowIdx + 1];
                        _logger.LogError($"Row {nextRow.RowNumber()} (data row):");
                        for (int c = col; c <= col + 10; c++)
                        {
                            var val = nextRow.Cell(c).GetString().Trim();
                            if (!string.IsNullOrEmpty(val))
                            {
                                _logger.LogError($"  Col{c}: '{val}'");
                            }
                        }
                    }
                }
            }
        }

        _logger.LogError("=== END SEARCH ===");
    }


    private void DebugRaceNumbers(List<string[]> data)
    {
        _logger.LogError("=== FINDING RACE NUMBERS ===");

        // First, find the RACE row
        int raceRowIndex = -1;
        for (int i = 0; i < data.Count; i++)
        {
            if (data[i][0].Equals("RACE", StringComparison.OrdinalIgnoreCase))
            {
                raceRowIndex = i;
                _logger.LogError($"RACE row found at index {raceRowIndex}");
                break;
            }
        }

        if (raceRowIndex != -1)
        {
            // Log the RACE row and surrounding rows
            for (int offset = -2; offset <= 3; offset++)
            {
                int rowIndex = raceRowIndex + offset;
                if (rowIndex >= 0 && rowIndex < data.Count)
                {
                    _logger.LogError($"Row {rowIndex} (offset {offset}):");
                    for (int col = 0; col < data[rowIndex].Length; col++)
                    {
                        if (!string.IsNullOrEmpty(data[rowIndex][col]))
                        {
                            _logger.LogError($"  Col{col}: '{data[rowIndex][col]}'");
                        }
                    }
                }
            }
        }

        // Also search for any numbers near the race labels
        _logger.LogError("Searching for numbers near race labels:");
        for (int i = 0; i < data.Count; i++)
        {
            for (int col = 0; col < data[i].Length; col++)
            {
                string value = data[i][col];
                if (!string.IsNullOrEmpty(value) && int.TryParse(value, out int num))
                {
                    // Check if this is near a race label
                    bool nearRaceLabel = false;

                    // Check same row for race labels
                    if (data[i][0] == "B" || data[i][0] == "C" || data[i][0] == "W" ||
                        data[i][0] == "I" || data[i][0] == "O")
                    {
                        nearRaceLabel = true;
                    }

                    // Check previous row for RACE header
                    if (i > 0 && data[i - 1][0] == "RACE")
                    {
                        nearRaceLabel = true;
                    }

                    // Check next row for race labels
                    if (i + 1 < data.Count && (data[i + 1][0] == "B" || data[i + 1][0] == "C"))
                    {
                        nearRaceLabel = true;
                    }

                    if (nearRaceLabel)
                    {
                        _logger.LogError($"Found number {num} at Row {i}, Col {col} - near race data");
                    }
                }
            }
        }

        _logger.LogError("=== END RACE SEARCH ===");
    }

    private (DateOnly From, DateOnly To) ExtractPeriodFromFileName(string fileName, string province)
    {
        // Try to extract date from filename like "EHL Acc Data 18.02.2025.xlsx"
        var match = Regex.Match(fileName, @"(\d{1,2})\.(\d{1,2})\.(\d{4})");

        if (match.Success)
        {
            int day = int.Parse(match.Groups[1].Value);
            int month = int.Parse(match.Groups[2].Value);
            int year = int.Parse(match.Groups[3].Value);

            var fromDate = new DateOnly(year, month, 1);
            var toDate = fromDate.AddMonths(1).AddDays(-1);

            _logger.LogInformation("Extracted period from filename: {From} to {To}", fromDate, toDate);
            return (fromDate, toDate);
        }

        // Default to current month if no date found
        var today = DateOnly.FromDateTime(DateTime.Now);
        var defaultFrom = new DateOnly(today.Year, today.Month, 1);
        var defaultTo = defaultFrom.AddMonths(1).AddDays(-1);

        _logger.LogWarning("No date found in filename, using current period: {From} to {To}", defaultFrom, defaultTo);
        return (defaultFrom, defaultTo);
    }

    private async Task SaveDemographicsAsync(ImportDemographics demographics, string fileName, string province)
    {
        if (!demographics.HasAgeData && !demographics.HasGenderData && !demographics.HasRaceData)
        {
            _logger.LogInformation("No demographics data to save");
            return;
        }

        var (periodFrom, periodTo) = ExtractPeriodFromFileName(fileName, province);

        // Check if record already exists for this period and province
        var existingRecord = await _context.CrashDemographics
            .FirstOrDefaultAsync(d => d.PeriodFrom == periodFrom &&
                                      d.PeriodTo == periodTo &&
                                      d.ProvinceCode == province);

        if (existingRecord != null)
        {
            // Update existing record
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
            // Create new record
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