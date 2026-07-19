using CrashReport.Data;
using CrashReport.Models;
using CrashReport.Security;
using CrashReport.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CrashReport.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _context;

    private static readonly HashSet<short> ValidSpeedLimits = new() { 30, 40, 50, 60, 80, 100, 120 };

    private static readonly HashSet<string> PedestrianCrashTypes =
        new(StringComparer.OrdinalIgnoreCase) { "PEDESTRIAN", "PED" };

    public HomeController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.TotalCrashes = await _context.Crashes.CountAsync();
        ViewBag.FatalCount = await _context.CrashPeople
                                   .CountAsync(cp => cp.SeverityOfInjury == "Fatal");
        ViewBag.SeriousCount = await _context.CrashPeople
                                   .CountAsync(cp => cp.SeverityOfInjury == "Serious");
        ViewBag.SlightCount = await _context.CrashPeople
                                   .CountAsync(cp => cp.SeverityOfInjury == "Slight");

        var now = DateTime.Today;
        var monthStart = new DateOnly(now.Year, now.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        ViewBag.ThisMonthCrashes = await _context.Crashes
            .CountAsync(c => c.CrashDate >= monthStart && c.CrashDate <= monthEnd);
        ViewBag.ThisMonthFatal = await _context.CrashPeople
            .CountAsync(cp => cp.SeverityOfInjury == "Fatal" &&
                cp.Crash.CrashDate >= monthStart && cp.Crash.CrashDate <= monthEnd);
        ViewBag.CurrentMonth = now.ToString("MMMM yyyy");

        return View();
    }

    public IActionResult Create() => View();

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var crash = await _context.Crashes.FindAsync(id);
        if (crash == null) return NotFound();
        return View("~/Views/Crashes/Edit.cshtml", crash);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Privileges.Crashes.Edit)]
    public async Task<IActionResult> Edit(int id,
        [Bind("CrashId,CasNo,CrNo,IncidentReportNo,CapturingNumber,CrashDate,CrashTime," +
              "NoOfAppendices,NoOfVehiclesInvolved,ProvinceCode,SpeedLimitKmh," +
              "RoadNumber,KmMarker,BriefDescription")] Crash crash)
    {
        if (id != crash.CrashId) return NotFound();
        if (ModelState.IsValid)
        {
            try { _context.Update(crash); await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Crashes.Any(c => c.CrashId == id)) return NotFound();
                throw;
            }
            TempData["SuccessMessage"] = $"Crash report #{id} updated successfully.";
            return RedirectToAction(nameof(Index));
        }
        return View("~/Views/Crashes/Edit.cshtml", crash);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Privileges.Crashes.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var crash = await _context.Crashes
            .Include(c => c.CrashLocations)
            .Include(c => c.CrashConditions)
            .Include(c => c.CrashWeathers)
            .Include(c => c.CrashVehicles).ThenInclude(cv => cv.VehicleDamages)
            .Include(c => c.CrashVehicles).ThenInclude(cv => cv.CrashPeople)
            .Include(c => c.CrashPeople).ThenInclude(cp => cp.PedestrianBicyclistDetails)
            .Include(c => c.ContributoryFactors)
            .Include(c => c.DangerousGoods)
            .Include(c => c.Witnesses)
            .Include(c => c.OfficialUses)
            .Include(c => c.CrashSketches)
            .FirstOrDefaultAsync(c => c.CrashId == id);

        if (crash == null)
        {
            TempData["ErrorMessage"] = $"Crash report #{id} not found.";
            return RedirectToAction(nameof(Index));
        }

        _context.CrashSketches.RemoveRange(crash.CrashSketches);
        _context.OfficialUses.RemoveRange(crash.OfficialUses);
        _context.Witnesses.RemoveRange(crash.Witnesses);
        _context.DangerousGoods.RemoveRange(crash.DangerousGoods);
        _context.ContributoryFactors.RemoveRange(crash.ContributoryFactors);
        _context.CrashWeathers.RemoveRange(crash.CrashWeathers);
        _context.CrashConditions.RemoveRange(crash.CrashConditions);
        _context.CrashLocations.RemoveRange(crash.CrashLocations);

        foreach (var cv in crash.CrashVehicles)
        {
            _context.VehicleDamages.RemoveRange(cv.VehicleDamages);
            _context.CrashPeople.RemoveRange(cv.CrashPeople);
        }
        _context.CrashVehicles.RemoveRange(crash.CrashVehicles);

        foreach (var cp in crash.CrashPeople)
            _context.PedestrianBicyclistDetails.RemoveRange(cp.PedestrianBicyclistDetails);
        _context.CrashPeople.RemoveRange(crash.CrashPeople);

        _context.Crashes.Remove(crash);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Crash report #{id} deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Privileges.Crashes.Create)]
    public async Task<IActionResult> Submit([FromForm] string formJson)
    {
        if (string.IsNullOrEmpty(formJson))
        {
            TempData["ErrorMessage"] = "No form data received.";
            return RedirectToAction(nameof(Create));
        }

        try
        {
            
            using var document = JsonDocument.Parse(formJson);
            var root = document.RootElement;

            
            var crash = new Crash();

           
            if (root.TryGetProperty("CrashInfo", out var crashInfo))
            {
                crash.CasNo = GetString(crashInfo, "CasNo");
                crash.CrNo = GetString(crashInfo, "CrNo");
                crash.CapturingNumber = GetString(crashInfo, "CapturingNumber");

                if (crashInfo.TryGetProperty("CrashDate", out var dateEl) && dateEl.ValueKind == JsonValueKind.String)
                {
                    crash.CrashDate = DateOnly.TryParse(dateEl.GetString(), out var d) ? d : DateOnly.FromDateTime(DateTime.Today);
                }

                if (crashInfo.TryGetProperty("CrashTime", out var timeEl) && timeEl.ValueKind == JsonValueKind.String)
                {
                    crash.CrashTime = TimeOnly.TryParse(timeEl.GetString(), out var t) ? t : null;
                }

                crash.ProvinceCode = GetString(crashInfo, "ProvinceCode");

                
                crash.SpeedLimitKmh = GetShort(crashInfo, "SpeedLimitKmh");

                crash.IncidentReportNo = GetString(crashInfo, "IncidentReportNo");


                crash.RoadNumber = GetString(crashInfo, "RoadNumber");
                crash.KmMarker = GetString(crashInfo, "KmMarker");

                
                crash.NoOfVehiclesInvolved = GetByte(crashInfo, "NoOfVehiclesInvolved", 1);
                crash.NoOfAppendices = GetByte(crashInfo, "NoOfAppendices", 0);

                crash.BriefDescription = GetString(crashInfo, "BriefDescription");
            }

            
            if (root.TryGetProperty("Location", out var location))
            {
                var crashLocation = new CrashLocation
                {
                    Crash = crash,
                    StreetRoadName = GetString(location, "StreetRoadName"),
                    AreaType = GetString(location, "AreaType"),
                    Suburb = GetString(location, "Suburb"),
                    CityTown = GetString(location, "CityTown"),
                    IntersectionStreet = GetString(location, "IntersectionStreet"),
                    GpsXCoordinate = GetDecimal(location, "GpsXCoordinate"),
                    GpsYCoordinate = GetDecimal(location, "GpsYCoordinate"),
                    RoadFunctionalClassification = GetString(location, "RoadFunctionalClassification"),
                    JunctionType = GetString(location, "JunctionType"),
                    RoadLayout = GetString(location, "RoadLayout"),
                    RoadSurfaceType = GetString(location, "RoadSurfaceType"),
                    RoadSurfaceCondition = GetString(location, "RoadSurfaceCondition")
                };
                _context.CrashLocations.Add(crashLocation);
            }

           
            if (root.TryGetProperty("Conditions", out var conditions))
            {
                var crashCondition = new CrashCondition
                {
                    Crash = crash,
                    LightCondition = GetString(conditions, "LightCondition"),
                    TrafficControlType = GetString(conditions, "TrafficControlType"),
                    CrashType = GetString(conditions, "CrashType"),
                    HitAndRun = GetBool(conditions, "HitAndRun"),
                    RoadSegmentGrade = GetString(conditions, "RoadSegmentGrade"),
                    ObstructionType = GetString(conditions, "ObstructionType"),
                    RoadSignsCondition = GetString(conditions, "RoadSignsCondition"),
                    RoadMarkingVisibility = GetString(conditions, "RoadMarkingVisibility")
                };
                _context.CrashConditions.Add(crashCondition);

               
                if (conditions.TryGetProperty("WeatherConditions", out var weatherEl) &&
                    weatherEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var w in weatherEl.EnumerateArray())
                    {
                        if (w.ValueKind == JsonValueKind.String)
                        {
                            _context.CrashWeathers.Add(new CrashWeather
                            {
                                Crash = crash,
                                WeatherCondition = w.GetString()
                            });
                        }
                    }
                }
            }

            // Extract Vehicles
            if (root.TryGetProperty("Vehicles", out var vehicles) && vehicles.ValueKind == JsonValueKind.Array)
            {
                foreach (var ve in vehicles.EnumerateArray())
                {
                    // Create Vehicle entity
                    var vehicle = new Vehicle
                    {
                        LicenceDiscNumber = GetString(ve, "LicenceDiscNumber"),
                        Make = GetString(ve, "Make"),
                        Model = GetString(ve, "Model"),
                        Colour = GetString(ve, "Colour"),
                        VehicleCategory = GetString(ve, "VehicleCategory"),
                        SpecialFunction = GetString(ve, "SpecialFunction"),
                        VinNumber = GetString(ve, "VinNumber")
                    };
                    _context.Vehicles.Add(vehicle);
                    await _context.SaveChangesAsync();

                    // Create CrashVehicle
                    var crashVehicle = new CrashVehicle
                    {
                        Crash = crash,
                        Vehicle = vehicle,
                        VehicleReference = GetString(ve, "VehicleReference"),
                        VehicleManoeuvre = GetString(ve, "VehicleManoeuvre"),
                        SeatbeltUsed = GetString(ve, "SeatbeltUsed"),
                        AlcoholSuspected = GetString(ve, "AlcoholSuspected"),
                        AlcoholTestResult = GetString(ve, "AlcoholTestResult"),
                        DrugSuspected = GetString(ve, "DrugSuspected"),
                        PositionBeforeCrash = GetString(ve, "PositionBeforeCrash")
                    };
                    _context.CrashVehicles.Add(crashVehicle);

                    // Handle driver if present
                    var driverSurname = GetString(ve, "DriverSurname");
                    if (!string.IsNullOrEmpty(driverSurname))
                    {
                        var driver = new Person
                        {
                            IdType = "RSA_ID",
                            IdNumber = GetString(ve, "DriverIdNumber"),
                            Surname = driverSurname,
                            FullNames = GetString(ve, "DriverFullNames") ?? string.Empty,
                            CellPhone = GetString(ve, "DriverCellPhone"),
                            Gender = GetString(ve, "Gender")
                        };
                        _context.Persons.Add(driver);
                        await _context.SaveChangesAsync();

                        crashVehicle.DriverPersonId = driver.PersonId;

                        if (!string.IsNullOrEmpty(GetString(ve, "LicenceCode")))
                        {
                            _context.DriversLicences.Add(new DriversLicence
                            {
                                PersonId = driver.PersonId,
                                LicenceCode = GetString(ve, "LicenceCode")
                            });
                        }

                        _context.CrashPeople.Add(new CrashPerson
                        {
                            Crash = crash,
                            Person = driver,
                            CrashVehicle = crashVehicle,
                            Role = "Driver",
                            VehicleReference = GetString(ve, "VehicleReference"),
                            SeverityOfInjury = GetString(ve, "SeverityOfInjury")
                        });
                    }

                    await _context.SaveChangesAsync();
                }
            }

            // Extract Persons
            if (root.TryGetProperty("Persons", out var persons) && persons.ValueKind == JsonValueKind.Array)
            {
                foreach (var pe in persons.EnumerateArray())
                {
                    var surname = GetString(pe, "Surname");
                    if (string.IsNullOrEmpty(surname)) continue;

                    var person = new Person
                    {
                        IdType = "RSA_ID",
                        IdNumber = GetString(pe, "IdNumber"),
                        Surname = surname,
                        FullNames = GetString(pe, "FullNames") ?? string.Empty,
                        Gender = GetString(pe, "Gender")
                    };
                    _context.Persons.Add(person);
                    await _context.SaveChangesAsync();

                    // Find the vehicle reference if provided
                    int? crashVehicleId = null;
                    var vehicleRef = GetString(pe, "VehicleReference");
                    if (!string.IsNullOrEmpty(vehicleRef))
                    {
                        var cv = await _context.CrashVehicles
                            .FirstOrDefaultAsync(cv => cv.CrashId == crash.CrashId &&
                                                       cv.VehicleReference == vehicleRef);
                        if (cv != null) crashVehicleId = cv.CrashVehicleId;
                    }

                    _context.CrashPeople.Add(new CrashPerson
                    {
                        Crash = crash,
                        Person = person,
                        CrashVehicleId = crashVehicleId,
                        Role = GetString(pe, "Role") ?? "Passenger",
                        VehicleReference = vehicleRef,
                        SeatingPosition = GetString(pe, "SeatingPosition"),
                        SeverityOfInjury = GetString(pe, "SeverityOfInjury"),
                        SeatbeltHelmetUsed = GetString(pe, "SeatbeltHelmet"),
                        Hospital = GetString(pe, "Hospital")
                    });

                    await _context.SaveChangesAsync();
                }
            }

            // Extract Factors
            if (root.TryGetProperty("Factors", out var factors) && factors.ValueKind == JsonValueKind.Array)
            {
                foreach (var f in factors.EnumerateArray())
                {
                    var description = GetString(f, "FactorDescription");
                    if (string.IsNullOrEmpty(description)) continue;

                    _context.ContributoryFactors.Add(new ContributoryFactor
                    {
                        Crash = crash,
                        FactorCategory = GetString(f, "FactorCategory"),
                        FactorDescription = description,
                        IsMajorFactor = GetBool(f, "IsMajorFactor", false)
                    });
                }
            }

            _context.Crashes.Add(crash);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Crash report #{crash.CrashId} saved successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
            return RedirectToAction(nameof(Create));
        }
    }

    

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();
        return null;
    }

    private static short GetShort(JsonElement element, string propertyName, short defaultValue = 0)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number)
            {
                try
                {
                    return prop.GetInt16();
                }
                catch
                {
                    // If it's a larger number, try to convert
                    if (prop.TryGetInt32(out var intVal))
                    {
                        return Convert.ToInt16(Math.Min(intVal, short.MaxValue));
                    }
                    return defaultValue;
                }
            }
            if (prop.ValueKind == JsonValueKind.String)
            {
                if (short.TryParse(prop.GetString(), out var result))
                    return result;
            }
        }
        return defaultValue;
    }

    private static byte GetByte(JsonElement element, string propertyName, byte defaultValue = 0)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number)
            {
                try
                {
                    return prop.GetByte();
                }
                catch
                {
                    // If it's a larger number, try to convert
                    if (prop.TryGetInt32(out var intVal))
                    {
                        return Convert.ToByte(Math.Min(intVal, byte.MaxValue));
                    }
                    return defaultValue;
                }
            }
            if (prop.ValueKind == JsonValueKind.String)
            {
                if (byte.TryParse(prop.GetString(), out var result))
                    return result;
            }
        }
        return defaultValue;
    }

    private static decimal? GetDecimal(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number)
            return prop.GetDecimal();
        return null;
    }

    private static bool GetBool(JsonElement element, string propertyName, bool defaultValue = false)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.True || prop.ValueKind == JsonValueKind.False)
                return prop.GetBoolean();
            if (prop.ValueKind == JsonValueKind.String)
            {
                var str = prop.GetString()?.ToLower();
                return str == "true" || str == "yes" || str == "1";
            }
            if (prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetInt32() != 0;
            }
        }
        return defaultValue;
    }

    public IActionResult CreateWithErrors()
    {
        if (TempData["ValidationErrors"] is List<string> errors)
        {
            ViewBag.ValidationErrors = errors;
        }
        if (TempData["FormJson"] is string formJson)
        {
            ViewBag.FormJson = formJson;
        }
        return View("Create");
    }

    private async Task<List<string>> ValidateForm(
        CrashReportFormViewModel vm, int? existingCrashId)
    {
        var errors = new List<string>();
        var ci = vm.CrashInfo;
        var today = DateOnly.FromDateTime(DateTime.Today);

        // ── Step 1: Crash Info ────────────────────────────────

        if (string.IsNullOrWhiteSpace(ci.CrNo) && string.IsNullOrWhiteSpace(ci.CasNo))
            errors.Add("At least one of CR No. or CAS No. is required.");

        if (!DateOnly.TryParse(ci.CrashDate, out var crashDate))
        {
            errors.Add("Crash date is required and must be a valid date.");
        }
        else
        {
            if (crashDate > today)
                errors.Add("Crash date cannot be in the future.");

            if (crashDate < today.AddYears(-5))
                errors.Add("Crash date cannot be more than 5 years in the past.");
        }

        // Duplicate CR No. (skip own record when editing)
        if (!string.IsNullOrWhiteSpace(ci.CrNo))
        {
            var duplicate = await _context.Crashes
                .AnyAsync(c => c.CrNo == ci.CrNo.Trim() &&
                               (existingCrashId == null || c.CrashId != existingCrashId));
            if (duplicate)
                errors.Add($"CR No. '{ci.CrNo.Trim()}' already exists in the database.");
        }

        // Speed limit must be a legal SA value if provided
        if (ci.SpeedLimitKmh.HasValue && !ValidSpeedLimits.Contains(ci.SpeedLimitKmh.Value))
            errors.Add($"Speed limit {ci.SpeedLimitKmh} km/h is not a valid South African " +
                       $"speed limit. Must be one of: {string.Join(", ", ValidSpeedLimits.Order())}.");

        // ── Step 2: Location ──────────────────────────────────

        var loc = vm.Location;
        bool hasLocation = !string.IsNullOrWhiteSpace(loc?.StreetRoadName) ||
                           !string.IsNullOrWhiteSpace(loc?.Suburb) ||
                           !string.IsNullOrWhiteSpace(loc?.CityTown);
        if (!hasLocation)
            errors.Add("At least one location field is required (Street/Road Name, Suburb, or City/Town).");

        // GPS: if one coordinate entered, both are required
        bool hasLat = loc?.GpsXCoordinate.HasValue == true;
        bool hasLon = loc?.GpsYCoordinate.HasValue == true;
        if (hasLat != hasLon)
        {
            errors.Add("Both GPS latitude and longitude are required if either is entered.");
        }
        else if (hasLat && hasLon)
        {
            var lat = loc!.GpsXCoordinate!.Value;
            var lon = loc!.GpsYCoordinate!.Value;
            if (lat < -35.0m || lat > -22.0m)
                errors.Add($"GPS latitude {lat} is outside South Africa (valid range: -35.0 to -22.0).");
            if (lon < 16.0m || lon > 33.0m)
                errors.Add($"GPS longitude {lon} is outside South Africa (valid range: 16.0 to 33.0).");
        }

        // ── Step 3: Conditions ────────────────────────────────

        if (string.IsNullOrWhiteSpace(vm.Conditions?.CrashType))
            errors.Add("Crash type is required.");

        if (string.IsNullOrWhiteSpace(vm.Conditions?.LightCondition))
            errors.Add("Light condition is required.");

        if (vm.Conditions?.WeatherConditions == null ||
            vm.Conditions.WeatherConditions.Count == 0)
            errors.Add("At least one weather condition must be selected.");

        // ── Step 4: Vehicles ──────────────────────────────────

        if (vm.Vehicles == null || vm.Vehicles.Count == 0)
            errors.Add("At least one vehicle must be entered.");
        else
        {
            // Vehicle count must match the declared count on Step 1
            if (vm.Vehicles.Count != ci.NoOfVehiclesInvolved)
                errors.Add($"Number of vehicles entered ({vm.Vehicles.Count}) does not match " +
                           $"'No. of Vehicles Involved' ({ci.NoOfVehiclesInvolved}) on Step 1.");

            // Vehicle references must be unique
            var refs = vm.Vehicles.Select(v => v.VehicleReference?.Trim().ToUpper()).ToList();
            if (refs.Distinct().Count() != refs.Count)
                errors.Add("Each vehicle must have a unique Vehicle Reference (A, B, C…).");

            foreach (var ve in vm.Vehicles)
            {
                var vRef = ve.VehicleReference?.Trim().ToUpper() ?? "(unknown)";

                // VehicleReference required
                if (string.IsNullOrWhiteSpace(ve.VehicleReference))
                    errors.Add($"Vehicle reference is required for each vehicle.");

                // If driver name entered, ID number and licence code are required
                if (!string.IsNullOrWhiteSpace(ve.DriverSurname))
                {
                    if (string.IsNullOrWhiteSpace(ve.DriverIdNumber))
                        errors.Add($"Vehicle {vRef}: driver ID number is required when a driver surname is entered.");
                    else if (!IsValidSaId(ve.DriverIdNumber))
                        errors.Add($"Vehicle {vRef}: driver ID number '{ve.DriverIdNumber}' is not a valid South African ID number.");

                    if (string.IsNullOrWhiteSpace(ve.LicenceCode))
                        errors.Add($"Vehicle {vRef}: licence code is required when a driver is named.");
                }

                // Alcohol/drug test result only if suspected
                if (!string.IsNullOrWhiteSpace(ve.AlcoholTestResult) &&
                    !string.Equals(ve.AlcoholSuspected, "Yes", StringComparison.OrdinalIgnoreCase))
                    errors.Add($"Vehicle {vRef}: alcohol test result entered but 'Alcohol Suspected' is not set to Yes.");
            }
        }

        // ── Step 5: Persons ───────────────────────────────────

        var vehicleRefs = (vm.Vehicles ?? new())
            .Select(v => v.VehicleReference?.Trim().ToUpper())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToHashSet()!;

        // Track which vehicles already have a driver assigned from Step 4
        var driverVehicleRefs = (vm.Vehicles ?? new())
            .Where(v => !string.IsNullOrWhiteSpace(v.DriverSurname) &&
                        !string.IsNullOrWhiteSpace(v.VehicleReference))
            .Select(v => v.VehicleReference!.Trim().ToUpper())
            .ToHashSet();

        foreach (var pe in vm.Persons ?? new())
        {
            if (string.IsNullOrWhiteSpace(pe.Surname)) continue;

            if (string.IsNullOrWhiteSpace(pe.Role))
                errors.Add($"Person '{pe.Surname}': role is required (Driver/Passenger/Pedestrian/Bicyclist).");

            if (string.IsNullOrWhiteSpace(pe.SeverityOfInjury))
                errors.Add($"Person '{pe.Surname}': severity of injury is required.");

            var role = pe.Role?.Trim();

            // Driver or Passenger must have a vehicle reference
            if ((string.Equals(role, "Driver", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(role, "Passenger", StringComparison.OrdinalIgnoreCase)) &&
                string.IsNullOrWhiteSpace(pe.VehicleReference))
            {
                errors.Add($"Person '{pe.Surname}': vehicle reference is required for {role} role.");
            }

            // Vehicle reference must match a vehicle entered in Step 4
            if (!string.IsNullOrWhiteSpace(pe.VehicleReference))
            {
                var pRef = pe.VehicleReference.Trim().ToUpper();
                if (!vehicleRefs.Contains(pRef))
                    errors.Add($"Person '{pe.Surname}': vehicle reference '{pe.VehicleReference}' does not match any vehicle entered in Step 4.");
            }

            // Cannot add a second Driver for the same vehicle
            if (string.Equals(role, "Driver", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(pe.VehicleReference))
            {
                var pRef = pe.VehicleReference.Trim().ToUpper();
                if (driverVehicleRefs.Contains(pRef))
                    errors.Add($"Person '{pe.Surname}': vehicle {pe.VehicleReference} already has a driver assigned in Step 4.");
            }

            // SA ID validation if provided
            if (!string.IsNullOrWhiteSpace(pe.IdNumber) && !IsValidSaId(pe.IdNumber))
                errors.Add($"Person '{pe.Surname}': ID number '{pe.IdNumber}' is not a valid South African ID number.");

            // Fatal persons should have gender recorded for demographic reports
            if (string.Equals(pe.SeverityOfInjury, "Fatal", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(pe.Gender))
                errors.Add($"Person '{pe.Surname}': gender is required for fatal victims (needed for demographic reports).");
        }

        // ── Cross-step rules ──────────────────────────────────

        // Pedestrian crash type requires at least one pedestrian or bicyclist victim
        var crashType = vm.Conditions?.CrashType;
        if (!string.IsNullOrWhiteSpace(crashType) &&
            PedestrianCrashTypes.Contains(crashType))
        {
            var allPersonRoles = (vm.Persons ?? new())
                .Select(pe => pe.Role?.Trim())
                .ToList();

            bool hasPedestrian = allPersonRoles.Any(r =>
                string.Equals(r, "Pedestrian", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r, "Bicyclist", StringComparison.OrdinalIgnoreCase));

            if (!hasPedestrian)
                errors.Add($"Crash type '{crashType}' requires at least one Pedestrian or Bicyclist victim in Step 5.");
        }

        return errors;
    }

    private static bool IsValidSaId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        // Must be exactly 13 digits
        id = id.Trim();
        if (id.Length != 13 || !id.All(char.IsDigit)) return false;

        // Month and day must be plausible
        if (!int.TryParse(id.Substring(2, 2), out var month) || month < 1 || month > 12)
            return false;
        if (!int.TryParse(id.Substring(4, 2), out var day) || day < 1 || day > 31)
            return false;

        // Luhn algorithm
        var digits = id.Select(c => c - '0').ToArray();
        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            if (i % 2 == 0)
            {
                sum += digits[i];
            }
            else
            {
                var d = digits[i] * 2;
                sum += d > 9 ? d - 9 : d;
            }
        }
        var checkDigit = (10 - (sum % 10)) % 10;
        return checkDigit == digits[12];
    }

    private static Crash BuildCrash(CrashReportFormViewModel vm)
    {
        var ci = vm.CrashInfo;
        return new Crash
        {
            CasNo = ci.CasNo,
            CrNo = ci.CrNo,
            IncidentReportNo = ci.IncidentReportNo,
            CapturingNumber = ci.CapturingNumber,
            CrashDate = DateOnly.TryParse(ci.CrashDate, out var d)
                                       ? d : DateOnly.FromDateTime(DateTime.Today),
            CrashTime = TimeOnly.TryParse(ci.CrashTime, out var t) ? t : null,
            ProvinceCode = ci.ProvinceCode,
            SpeedLimitKmh = ci.SpeedLimitKmh,
            RoadNumber = ci.RoadNumber,
            KmMarker = ci.KmMarker,
            NoOfVehiclesInvolved = ci.NoOfVehiclesInvolved,
            NoOfAppendices = ci.NoOfAppendices,
            BriefDescription = ci.BriefDescription
        };
    }

    private async Task SaveRelatedEntities(Crash crash, CrashReportFormViewModel vm)
    {
        var loc = vm.Location;
        if (loc != null)
        {
            _context.CrashLocations.Add(new CrashLocation
            {
                CrashId = crash.CrashId,
                StreetRoadName = loc.StreetRoadName,
                AreaType = loc.AreaType,
                BuiltUpArea = loc.BuiltUpArea,
                GpsXCoordinate = loc.GpsXCoordinate,
                GpsYCoordinate = loc.GpsYCoordinate,
                IntersectionStreet = loc.IntersectionStreet,
                Suburb = loc.Suburb,
                CityTown = loc.CityTown,
                RoadFunctionalClassification = loc.RoadFunctionalClassification,
                JunctionType = loc.JunctionType,
                RoadLayout = loc.RoadLayout,
                RoadSurfaceType = loc.RoadSurfaceType,
                RoadSurfaceCondition = loc.RoadSurfaceCondition
            });
        }

        var cond = vm.Conditions;
        if (cond != null)
        {
            _context.CrashConditions.Add(new CrashCondition
            {
                CrashId = crash.CrashId,
                LightCondition = cond.LightCondition,
                TrafficControlType = cond.TrafficControlType,
                CrashType = cond.CrashType,
                HitAndRun = cond.HitAndRun,
                RoadSegmentGrade = cond.RoadSegmentGrade,
                ObstructionType = cond.ObstructionType,
                RoadSignsCondition = cond.RoadSignsCondition,
                RoadMarkingVisibility = cond.RoadMarkingVisibility
            });

            foreach (var w in cond.WeatherConditions ?? new())
                _context.CrashWeathers.Add(new CrashWeather
                { CrashId = crash.CrashId, WeatherCondition = w });
        }

        foreach (var ve in vm.Vehicles ?? new())
        {
            Person? driver = null;
            if (!string.IsNullOrEmpty(ve.DriverSurname))
            {
                driver = new Person
                {
                    IdType = ve.DriverIdType ?? "RSA_ID",
                    IdNumber = ve.DriverIdNumber,
                    Surname = ve.DriverSurname,
                    FullNames = ve.DriverFullNames ?? string.Empty,
                    CellPhone = ve.DriverCellPhone,
                };
                _context.Persons.Add(driver);
                await _context.SaveChangesAsync();

                if (!string.IsNullOrEmpty(ve.LicenceCode))
                    _context.DriversLicences.Add(new DriversLicence
                    { PersonId = driver.PersonId, LicenceCode = ve.LicenceCode });
            }

            var vehicle = new Vehicle
            {
                LicenceDiscNumber = ve.LicenceDiscNumber,
                Make = ve.Make,
                Model = ve.Model,
                Colour = ve.Colour,
                VehicleCategory = ve.VehicleCategory,
                SpecialFunction = ve.SpecialFunction,
                VinNumber = ve.VinNumber
            };
            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync();

            var cv = new CrashVehicle
            {
                CrashId = crash.CrashId,
                VehicleId = vehicle.VehicleId,
                DriverPersonId = driver?.PersonId,
                VehicleReference = ve.VehicleReference,
                VehicleManoeuvre = ve.VehicleManoeuvre,
                SeatbeltUsed = ve.SeatbeltUsed,
                AlcoholSuspected = ve.AlcoholSuspected,
                AlcoholTestResult = ve.AlcoholTestResult,
                DrugSuspected = ve.DrugSuspected,
                PositionBeforeCrash = ve.PositionBeforeCrash
            };
            _context.CrashVehicles.Add(cv);
            await _context.SaveChangesAsync();

            if (driver != null)
                _context.CrashPeople.Add(new CrashPerson
                {
                    CrashId = crash.CrashId,
                    PersonId = driver.PersonId,
                    CrashVehicleId = cv.CrashVehicleId,
                    Role = "Driver",
                    VehicleReference = ve.VehicleReference,
                    SeverityOfInjury = ve.SeverityOfInjury
                });
        }

        foreach (var pe in vm.Persons ?? new())
        {
            if (string.IsNullOrEmpty(pe.Surname)) continue;

            var person = new Person
            {
                IdType = "RSA_ID",//ID shouldn't be default to sa id
                IdNumber = pe.IdNumber,
                Surname = pe.Surname,
                FullNames = pe.FullNames ?? string.Empty,
                Gender = pe.Gender
            };
            _context.Persons.Add(person);
            await _context.SaveChangesAsync();

            _context.CrashPeople.Add(new CrashPerson
            {
                CrashId = crash.CrashId,
                PersonId = person.PersonId,
                Role = pe.Role ?? "Passenger",
                VehicleReference = pe.VehicleReference,
                SeatingPosition = pe.SeatingPosition,
                SeverityOfInjury = pe.SeverityOfInjury,
                SeatbeltHelmetUsed = pe.SeatbeltHelmet,
                Hospital = pe.Hospital
            });
        }

        foreach (var f in vm.Factors ?? new())
        {
            if (string.IsNullOrEmpty(f.FactorDescription)) continue;
            _context.ContributoryFactors.Add(new ContributoryFactor
            {
                CrashId = crash.CrashId,
                FactorCategory = f.FactorCategory,
                FactorDescription = f.FactorDescription,
                IsMajorFactor = f.IsMajorFactor
            });
        }
    }
}