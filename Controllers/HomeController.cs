using CrashReport.Data;
using CrashReport.Models;
using CrashReport.Security;
using CrashReport.Services;
using CrashReport.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CrashReport.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _context;
    private readonly MonthlyMemoDataService _memoData;
    private readonly IWebHostEnvironment _env;

    private readonly ICrashFormValidationService _validation;

    public HomeController(AppDbContext context, MonthlyMemoDataService memoData, ICrashFormValidationService validation, IWebHostEnvironment env)
    {
        _context = context;
        _memoData = memoData;
        _validation = validation;
        _env = env;
    }

    public async Task<IActionResult> Index()
    {
        var now = DateTime.Today;
        var monthStart = new DateOnly(now.Year, now.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var allTime = await _memoData.LoadAsync(new DateOnly(2020, 1, 1), monthEnd);

        bool Has(string privilege) => User.HasClaim(Privileges.ClaimType, privilege);

        var canView = Has(Privileges.Crashes.View);
        var canCreateFull = Has(Privileges.Crashes.Create);
        var canQuickAdd = Has(Privileges.Crashes.CreateSummary);
        var canEdit = Has(Privileges.Crashes.Edit);
        var canDelete = Has(Privileges.Crashes.Delete);
        var canImport = Has(Privileges.Import.Excel);
        var canStandby = Has(Privileges.Reports.Standby);
        var canMonthly = Has(Privileges.Reports.Monthly);
        var canFiveYear = Has(Privileges.Reports.FiveYear);
        var canQuarterly = Has(Privileges.Reports.Quarterly);
        var canAnyReport = canStandby || canMonthly || canFiveYear || canQuarterly;
        var canAdminister = Has(Privileges.Admin.Users) || Has(Privileges.Admin.Roles) || Has(Privileges.Admin.Lookups);

        ViewBag.CanView = canView;
        ViewBag.CanCreateFull = canCreateFull;
        ViewBag.CanQuickAdd = canQuickAdd;
        ViewBag.CanEdit = canEdit;
        ViewBag.CanDelete = canDelete;
        ViewBag.CanImport = canImport;
        ViewBag.CanStandby = canStandby;
        ViewBag.CanMonthly = canMonthly;
        ViewBag.CanFiveYear = canFiveYear;
        ViewBag.CanQuarterly = canQuarterly;
        ViewBag.CanAnyReport = canAnyReport;
        ViewBag.CanAdminister = canAdminister;

        // Role label — display only, drives which dashboard layout/copy renders.
        // Precedence: System Administrator > Provincial Staff > Regional Staff >
        // Cost Centre Administrator > SAPS Officer.
        var roleLabel =
            User.IsInRole("System Administrator") ? "System Administrator" :
            User.IsInRole("Provincial Staff") ? "Provincial Staff" :
            User.IsInRole("Regional Staff") ? "Regional Staff" :
            User.IsInRole("Cost Centre Administrator") ? "Cost Centre Administrator" :
            "SAPS Officer";
        ViewBag.RoleLabel = roleLabel;

        // Scope claims (District/Station), added by AppUserClaimsPrincipalFactory.
        var userDistrict = User.FindFirst("District")?.Value;
        var userStation = User.FindFirst("Station")?.Value;
        ViewBag.UserDistrict = userDistrict;
        ViewBag.UserStation = userStation;

        ViewBag.DashboardMode =
            (roleLabel == "System Administrator" || roleLabel == "Provincial Staff") ? "analytics" :
            roleLabel == "Regional Staff" ? "review" :
            "capture"; // Cost Centre Administrator, SAPS Officer

        // ── Real scoping, not just a banner label. Station-scoped roles
        // (SAPS Officer, Cost Centre Administrator) see only their own
        // station; Regional Staff sees their own district; analytics
        // mode (System Administrator, Provincial Staff) stays unscoped
        // -- that's correct for those roles, not an oversight. Filters
        // the already-loaded Row list directly, since Row.Station and
        // Row.District are already resolved by LoadAsync -- no need for
        // a second query or a separate lookup service injected here. ──
        var scopedAllTime = allTime;
        if (roleLabel == "SAPS Officer" || roleLabel == "Cost Centre Administrator")
        {
            if (!string.IsNullOrEmpty(userStation))
                scopedAllTime = allTime
                    .Where(r => string.Equals(r.Station, userStation, StringComparison.OrdinalIgnoreCase))
                    .ToList();
        }
        else if (roleLabel == "Regional Staff")
        {
            if (!string.IsNullOrEmpty(userDistrict))
                scopedAllTime = allTime
                    .Where(r => string.Equals(r.District, userDistrict, StringComparison.OrdinalIgnoreCase))
                    .ToList();
        }

        var scopedThisMonth = scopedAllTime.Where(r => r.Date >= monthStart && r.Date <= monthEnd).ToList();

        // Also fixes a second real bug: the previous version counted only
        // _context.Crashes / _context.CrashPeople directly, which are
        // manually-captured records ONLY -- Quick Add and imported
        // CrashSummaries were silently excluded from every KPI. LoadAsync
        // already merges both sources correctly, so switching to it here
        // fixes the undercount at the same time as the scoping.
        ViewBag.TotalCrashes = scopedAllTime.Count;
        ViewBag.FatalCount = scopedAllTime.Sum(r => r.Fatalities);
        ViewBag.SeriousCount = scopedAllTime.Sum(r => r.Serious);
        ViewBag.SlightCount = scopedAllTime.Sum(r => r.Slight);

        // Previously referenced by the view but never set -- the
        // computed "thisMonth" value was discarded without ever
        // reaching ViewBag, so this line always rendered blank.
        ViewBag.ThisMonthCrashes = scopedThisMonth.Count;
        ViewBag.ThisMonthFatal = scopedThisMonth.Sum(r => r.Fatalities);
        ViewBag.CurrentMonth = now.ToString("MMMM");

        // For capture/review modes, hand the scoped recent-activity rows
        // straight to the view -- no separate AJAX call needed, and no
        // risk of the client re-fetching an unscoped list. Analytics mode
        // keeps its existing separate /Crashes/Grid call (large page
        // size, unscoped, used for the district/severity charts too).
        if (roleLabel != "System Administrator" && roleLabel != "Provincial Staff")
        {
            var recent = scopedAllTime
                .OrderByDescending(r => r.Date)
                .ThenByDescending(r => r.Time)
                .Take(8)
                .Select(r => new
                {
                    r.CrashId,
                    r.SummaryId,
                    r.CrNo,
                    r.District,
                    r.Station,
                    r.Fatalities,
                    r.Serious,
                    r.Source
                });
            ViewBag.ScopedRecentActivityJson = JsonSerializer.Serialize(recent);
        }

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

        // Validated by deserializing formJson into the ViewModel SEPARATELY
        // from the raw JsonDocument parsing the save logic below still
        // uses -- the ViewModel is a narrower slice of the real posted
        // data (it doesn't cover every field the raw parse does), so it's
        // used for validation only, never as the save path itself. This
        // is the piece that was previously missing entirely: ValidateForm
        // existed, fully written, but nothing in this action ever called it.
        var vmForValidation = System.Text.Json.JsonSerializer.Deserialize<CrashReportFormViewModel>(
            formJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

        var validationErrors = await _validation.ValidateAsync(vmForValidation, existingCrashId: null);
        if (validationErrors.Count > 0)
        {
            TempData["ValidationErrors"] = validationErrors;
            TempData["FormJson"] = formJson;
            return RedirectToAction(nameof(CreateWithErrors));
        }

        // Start a transaction to guarantee all-or-nothing
        await using var transaction = await _context.Database.BeginTransactionAsync();

        // Tracks whether this CrNo already exists as a Quick Add / imported
        // CrashSummary. NOT used to block the save -- see the check itself,
        // set inside the CRASH INFO section below once crash.CrNo is known.
        var existsAsSummary = false;

        try
        {
            using var document = JsonDocument.Parse(formJson);
            var root = document.RootElement;

            var crash = new Crash();
            _context.Crashes.Add(crash);

            // ========== 1. CRASH INFO ==========
            if (root.TryGetProperty("CrashInfo", out var crashInfo))
            {
                crash.CasNo = GetString(crashInfo, "CasNo");
                crash.CrNo = GetString(crashInfo, "CrNo");
                crash.CapturingNumber = GetString(crashInfo, "CapturingNumber");
                crash.IncidentReportNo = GetString(crashInfo, "IncidentReportNo");
                crash.RoadNumber = GetString(crashInfo, "RoadNumber");
                crash.KmMarker = GetString(crashInfo, "KmMarker");
                crash.BriefDescription = GetString(crashInfo, "BriefDescription");
                crash.ProvinceCode = GetString(crashInfo, "ProvinceCode");
                crash.SpeedLimitKmh = GetShort(crashInfo, "SpeedLimitKmh");
                crash.NoOfAppendices = GetByte(crashInfo, "NoOfAppendices", 0);

                if (crashInfo.TryGetProperty("CrashDate", out var dateEl) && dateEl.ValueKind == JsonValueKind.String)
                    crash.CrashDate = DateOnly.TryParse(dateEl.GetString(), out var d) ? d : DateOnly.FromDateTime(DateTime.Today);

                if (crashInfo.TryGetProperty("CrashTime", out var timeEl) && timeEl.ValueKind == JsonValueKind.String)
                    crash.CrashTime = TimeOnly.TryParse(timeEl.GetString(), out var t) ? t : null;

                // VehicleString will be auto-generated later; keep if sent by UI.
                crash.VehicleString = GetString(crashInfo, "VehicleString");

                // Allow-and-warn, not block -- symmetric with the same check
                // in CreateSummaryController.CreateSummary. The same
                // physical CR1 form can legitimately reach the system
                // through both paths (a Cost Centre Quick Adds a summary
                // before an officer gets around to digitizing the full
                // form for the same crash), so blocking here would reject
                // the richer, more authoritative record just because a
                // thinner one arrived first. MonthlyMemoDataService.LoadAsync
                // already deduplicates by CrNo across both tables before
                // counting anything in a report, so this is safe from a
                // reporting standpoint -- flagging it is for reconciliation
                // visibility, not correctness.
                if (!string.IsNullOrWhiteSpace(crash.CrNo))
                    existsAsSummary = await _context.CrashSummaries.AnyAsync(s => s.CrNo == crash.CrNo);
            }

            // ========== 2. LOCATION ==========
            if (root.TryGetProperty("Location", out var location))
            {
                var crashLocation = new CrashLocation
                {
                    Crash = crash,
                    BuiltUpArea = GetBool(location, "BuiltUpArea"),
                    AreaType = GetString(location, "AreaType"),
                    StreetRoadName = GetString(location, "StreetRoadName"),
                    GpsXCoordinate = GetDecimal(location, "GpsXCoordinate"),
                    GpsYCoordinate = GetDecimal(location, "GpsYCoordinate"),
                    IntersectionStreet = GetString(location, "IntersectionStreet"),
                    IntersectionRoadNo = GetString(location, "IntersectionRoadNo"),
                    BetweenFrom = GetString(location, "BetweenFrom"),
                    BetweenTo = GetString(location, "BetweenTo"),
                    Suburb = GetString(location, "Suburb"),
                    CityTown = GetString(location, "CityTown"),
                    DistanceKm = GetDecimal(location, "DistanceKm"),
                    CompassDirection = GetString(location, "CompassDirection"),
                    FromPoint = GetString(location, "FromPoint"),
                    KmMarkerInfo = GetString(location, "KmMarkerInfo"),
                    NextCityTown = GetString(location, "NextCityTown"),
                    RoadFunctionalClassification = GetString(location, "RoadFunctionalClassification"),
                    JunctionType = GetString(location, "JunctionType"),
                    RoadLayout = GetString(location, "RoadLayout"),
                    RoadSurfaceType = GetString(location, "RoadSurfaceType"),
                    RoadSurfaceQuality = GetString(location, "RoadSurfaceQuality"),
                    RoadSurfaceCondition = GetString(location, "RoadSurfaceCondition")
                };
                _context.CrashLocations.Add(crashLocation);
            }

            // ========== 3. CONDITIONS ==========
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
                    RoadMarkingVisibility = GetString(conditions, "RoadMarkingVisibility"),
                    OvertakingControl = GetString(conditions, "OvertakingControl"),
                    TyreBurstObserved = GetString(conditions, "TyreBurstObserved"),
                    VehicleLightsCondition = GetString(conditions, "VehicleLightsCondition"),
                    OtherObservations = GetString(conditions, "OtherObservations"),
                };
                _context.CrashConditions.Add(crashCondition);

                // Weather
                if (conditions.TryGetProperty("WeatherConditions", out var weatherEl) && weatherEl.ValueKind == JsonValueKind.Array)
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

            // ========== 4. VEHICLES ==========
            var vehicleMakes = new List<string>();
            if (root.TryGetProperty("Vehicles", out var vehicles) && vehicles.ValueKind == JsonValueKind.Array)
            {
                foreach (var ve in vehicles.EnumerateArray())
                {
                    // ---- 4a. Vehicle (static) ----
                    var vehicle = new Vehicle
                    {
                        CountryOfRegistration = GetString(ve, "CountryOfRegistration") ?? "ZA",
                        LicenceDiscNumber = GetString(ve, "LicenceDiscNumber"),
                        Colour = GetString(ve, "Colour"),
                        Make = GetString(ve, "Make"),
                        Model = GetString(ve, "Model"),
                        VinNumber = GetString(ve, "VinNumber"),
                        VehicleType = GetString(ve, "VehicleType"),
                        TrailerLicenceNumber = GetString(ve, "TrailerLicenceNumber"),
                        VehicleCategory = GetString(ve, "VehicleCategory"),
                        VehicleTypeCode = GetString(ve, "VehicleTypeCode"),
                        SpecialFunction = GetString(ve, "SpecialFunction"),
                        PrivateOrBusiness = GetString(ve, "PrivateOrBusiness"),
                        LicenceTypeFitting = GetString(ve, "LicenceTypeFitting"),
                        CreatedAt = DateTime.Now
                    };
                    _context.Vehicles.Add(vehicle);
                    await _context.SaveChangesAsync();

                    // Collect makes for VehicleString
                    if (!string.IsNullOrEmpty(vehicle.Make))
                        vehicleMakes.Add(vehicle.Make);

                    // ---- 4b. CrashVehicle (link + crash-specific data) ----
                    var crashVehicle = new CrashVehicle
                    {
                        Crash = crash,
                        Vehicle = vehicle,
                        VehicleReference = GetString(ve, "VehicleReference"),
                        VehicleManoeuvre = GetString(ve, "VehicleManoeuvre"),
                        SeatbeltUsed = GetString(ve, "SeatbeltHelmetUsed"),  // <-- mapped from frontend
                        AlcoholSuspected = GetString(ve, "AlcoholSuspected"),
                        AlcoholTestResult = GetString(ve, "AlcoholTestResult"),
                        DrugSuspected = GetString(ve, "DrugSuspected"),
                        DrugTestResult = GetString(ve, "DrugTestResult"),
                        PositionBeforeCrash = GetString(ve, "PositionBeforeCrash"),
                        VehicleType = GetString(ve, "VehicleType"),
                        PassengersForReward = GetString(ve, "PassengersForReward"),
                        BreakdownCompany = GetString(ve, "BreakdownCompany"),

                    };
                    _context.CrashVehicles.Add(crashVehicle);
                    await _context.SaveChangesAsync(); // needed to get CrashVehicleId for damages

                    // ---- 4c. VehicleDamages ----
                    if (ve.TryGetProperty("VehicleDamages", out var damages) && damages.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var damage in damages.EnumerateArray())
                        {
                            if (damage.ValueKind == JsonValueKind.String)
                            {
                                _context.VehicleDamages.Add(new VehicleDamage
                                {
                                    CrashVehicleId = crashVehicle.CrashVehicleId,
                                    DamagePoint = damage.GetString()!
                                });
                            }
                        }
                    }

                    // ---- 4d. DangerousGoods ----
                    if (!string.IsNullOrWhiteSpace(GetString(ve, "GoodsCarried")) ||
                        !string.IsNullOrWhiteSpace(GetString(ve, "UnNumber")) ||
                        !string.IsNullOrWhiteSpace(GetString(ve, "CompanyName")))
                    {
                        _context.DangerousGoods.Add(new DangerousGood
                        {
                            Crash = crash,
                            VehicleReference = crashVehicle.VehicleReference,
                            GoodsCarried = GetString(ve, "GoodsCarried"),
                            SpillageObserved = GetString(ve, "SpillageObserved"),
                            VapourGasEmission = GetString(ve, "VapourGasEmission"),
                            PlacardDisplayed = GetString(ve, "PlacardDisplayed"),
                            UnNumber = GetString(ve, "UnNumber"),
                            CompanyName = GetString(ve, "CompanyName"),
                            EmergencyServicesActivated = GetString(ve, "EmergencyServicesActivated")
                        });
                    }

                    // ---- 4e. Driver (Person) ----
                    var driverSurname = GetString(ve, "DriverSurname");
                    if (!string.IsNullOrEmpty(driverSurname))
                    {
                        var driver = new Person
                        {
                            IdType = GetString(ve, "DriverIdType") ?? "RSA_ID",
                            IdNumber = GetString(ve, "DriverIdNumber"),
                            Age = GetByte(ve, "DriverAge"),
                            Surname = driverSurname,
                            FullNames = GetString(ve, "DriverFullNames") ?? "",
                            CountryOfOrigin = GetString(ve, "DriverCountryOfOrigin"),
                            Nationality = GetString(ve, "DriverNationality"),
                            PopulationGroup = GetString(ve, "DriverPopulationGroup"),
                            Gender = GetString(ve, "DriverGender"),
                            HomeAddress = GetString(ve, "DriverHomeAddress"),
                            CellPhone = GetString(ve, "DriverCellPhone"),
                            OtherPhone = GetString(ve, "DriverOtherPhone"),
                            WorkContactAddress = GetString(ve, "DriverWorkAddress"),
                            CreatedAt = DateTime.Now
                        };
                        _context.Persons.Add(driver);
                        await _context.SaveChangesAsync();

                        crashVehicle.DriverPersonId = driver.PersonId;

                        // ---- Driver Licence ----
                        if (!string.IsNullOrEmpty(GetString(ve, "LicenceCode")))
                        {
                            _context.DriversLicences.Add(new DriversLicence
                            {
                                PersonId = driver.PersonId,
                                LicenceType = GetString(ve, "LicenceType"),
                                LicenceNumber = GetString(ve, "LicenceNumber"),
                                LicenceCode = GetString(ve, "LicenceCode"),
                                DateOfIssue = GetDateOnly(ve, "DateOfIssue"),
                                PrdpCode = GetString(ve, "PrdpCode")
                            });
                        }

                        // ---- CrashPerson (Driver) ----
                        _context.CrashPeople.Add(new CrashPerson
                        {
                            Crash = crash,
                            Person = driver,
                            CrashVehicle = crashVehicle,
                            Role = "Driver",
                            VehicleReference = GetString(ve, "VehicleReference"),
                            SeverityOfInjury = GetString(ve, "SeverityOfInjury"),
                            PersonReference = GetString(ve, "PersonReference"),
                            PassengerNumber = GetByte(ve, "PassengerNumber"),
                            ChildRestraintUsed = GetString(ve, "ChildRestraintUsed"),
                            LiquorDrugSuspected = GetString(ve, "LiquorDrugSuspected"),
                            LiquorDrugTestDone = GetString(ve, "LiquorDrugTestDone"),
                            AmbulanceServiceRef = GetString(ve, "AmbulanceServiceRef"),
                            Hospital = GetString(ve, "Hospital"),

                        });
                    }

                    await _context.SaveChangesAsync();
                }
            }


            crash.NoOfVehiclesInvolved = (byte)(root.TryGetProperty("Vehicles", out var vArr) ? vArr.GetArrayLength() : 0);


            if (string.IsNullOrEmpty(crash.VehicleString) && vehicleMakes.Any())
                crash.VehicleString = string.Join(", ", vehicleMakes.Distinct());

            if (root.TryGetProperty("Persons", out var persons) && persons.ValueKind == JsonValueKind.Array)
            {
                foreach (var pe in persons.EnumerateArray())
                {
                    var surname = GetString(pe, "Surname");
                    if (string.IsNullOrEmpty(surname)) continue;

                    var person = new Person
                    {
                        IdType = GetString(pe, "DriverIdType") ?? "RSA_ID",
                        IdNumber = GetString(pe, "DriverIdNumber"),
                        Age = GetByte(pe, "DriverAge"),
                        Surname = surname,
                        FullNames = GetString(pe, "DriverFullNames") ?? "",
                        CountryOfOrigin = GetString(pe, "CountryOfOrigin"),
                        Nationality = GetString(pe, "Nationality"),
                        PopulationGroup = GetString(pe, "PopulationGroup"),
                        Gender = GetString(pe, "Gender"),
                        HomeAddress = GetString(pe, "HomeAddress"),
                        CellPhone = GetString(pe, "DriverCellPhone"),
                        OtherPhone = GetString(pe, "OtherPhone"),
                        WorkContactAddress = GetString(pe, "WorkContactAddress"),
                        CreatedAt = DateTime.Now
                    };
                    _context.Persons.Add(person);
                    await _context.SaveChangesAsync();

                    int? crashVehicleId = null;
                    var vehicleRef = GetString(pe, "VehicleReference");
                    if (!string.IsNullOrEmpty(vehicleRef))
                    {
                        var cv = await _context.CrashVehicles
                            .FirstOrDefaultAsync(cv => cv.CrashId == crash.CrashId &&
                                                       cv.VehicleReference == vehicleRef);
                        if (cv != null) crashVehicleId = cv.CrashVehicleId;
                    }

                    var crashPerson = new CrashPerson
                    {
                        Crash = crash,
                        Person = person,
                        CrashVehicleId = crashVehicleId,
                        Role = GetString(pe, "Role") ?? "Passenger",
                        VehicleReference = vehicleRef,
                        SeatingPosition = GetString(pe, "SeatingPosition"),
                        SeverityOfInjury = GetString(pe, "SeverityOfInjury"),
                        SeatbeltHelmetUsed = GetString(pe, "SeatbeltHelmet"),
                        Hospital = GetString(pe, "Hospital"),
                        PersonReference = GetString(pe, "PersonReference"),
                        PassengerNumber = GetByte(pe, "PassengerNumber"),
                        ChildRestraintUsed = GetString(pe, "ChildRestraint"),
                        LiquorDrugSuspected = GetString(pe, "LiquorDrugSuspected"),
                        LiquorDrugTestDone = GetString(pe, "LiquorDrugTestDone"),
                        AmbulanceServiceRef = GetString(pe, "AmbulanceReference"),
                    };

                    _context.CrashPeople.Add(crashPerson);
                    await _context.SaveChangesAsync();


                    var role = GetString(pe, "Role");
                    if (role == "Pedestrian" || role == "Bicyclist")
                    {
                        var detail = new PedestrianBicyclistDetail
                        {
                            CrashPersonId = crashPerson.CrashPersonId,
                            PositionOnRoad = GetString(pe, "PositionOnRoad"),
                            LocationReCrossing = GetString(pe, "LocationReCrossing"),
                            Manoeuvre = GetString(pe, "Manoeuvre"),
                            PedestrianAction = GetString(pe, "PedestrianAction"),
                            ClothingColour = GetString(pe, "ClothingColour")
                        };
                        _context.PedestrianBicyclistDetails.Add(detail);

                    }
                }
            }


            // Passengers visibly not injured -- a separate, lighter
            // section on the physical form (Page 3), deliberately not
            // routed through the same fields as injured persons above.
            // Reuses Person/CrashPerson directly (no new table) --
            // SeverityOfInjury = "No injury" is the same fourth severity
            // code the paper form already lists (Fatal/Serious/Slight/No
            // injury), just reached through a five-field fast path
            // instead of the full injured-person form.
            if (root.TryGetProperty("UninjuredPassengers", out var uninjured) && uninjured.ValueKind == JsonValueKind.Array)
            {
                foreach (var up in uninjured.EnumerateArray())
                {
                    var uSurname = GetString(up, "Surname");
                    if (string.IsNullOrEmpty(uSurname)) continue;

                    var uPerson = new Person
                    {
                        IdType = "RSA_ID",
                        IdNumber = GetString(up, "IdNumber"),
                        Surname = uSurname,
                        FullNames = "",
                        CellPhone = GetString(up, "CellPhone"),
                        CreatedAt = DateTime.Now
                    };
                    _context.Persons.Add(uPerson);
                    await _context.SaveChangesAsync();

                    int? uCrashVehicleId = null;
                    var uVehicleRef = GetString(up, "VehicleReference");
                    if (!string.IsNullOrEmpty(uVehicleRef))
                    {
                        var uCv = await _context.CrashVehicles
                            .FirstOrDefaultAsync(cv => cv.CrashId == crash.CrashId &&
                                                       cv.VehicleReference == uVehicleRef);
                        if (uCv != null) uCrashVehicleId = uCv.CrashVehicleId;
                    }

                    _context.CrashPeople.Add(new CrashPerson
                    {
                        Crash = crash,
                        Person = uPerson,
                        CrashVehicleId = uCrashVehicleId,
                        Role = "Passenger",
                        VehicleReference = uVehicleRef,
                        SeverityOfInjury = "No injury"
                    });
                    await _context.SaveChangesAsync();
                }
            }


            // 6. CONTRIBUTORY FACTORS
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

            // ========== 7. WITNESSES (NEW) ==========
            if (root.TryGetProperty("Witnesses", out var witnesses) && witnesses.ValueKind == JsonValueKind.Array)
            {
                foreach (var w in witnesses.EnumerateArray())
                {
                    var surname = GetString(w, "Surname");
                    if (string.IsNullOrEmpty(surname)) continue;

                    _context.Witnesses.Add(new Witness
                    {
                        Crash = crash,
                        SurnameInitials = surname,
                        IdNumber = GetString(w, "IdNumber"),
                        CellPhone = GetString(w, "CellPhone"),
                        OtherPhone = GetString(w, "OtherPhone"),
                        WorkContactAddress = GetString(w, "WorkContactAddress")
                    });
                }
            }

            // ========== OFFICIAL USE ==========
            if (root.TryGetProperty("OfficialUse", out var officialUse))
            {
                var official = new OfficialUse
                {
                    Crash = crash,
                    OfficeWhereOccurred = GetString(officialUse, "OfficeWhereOccurred"),
                    OccurrenceBookNo = GetString(officialUse, "OccurrenceBookNo"),
                    AccidentRegisterNo = GetString(officialUse, "AccidentRegisterNo"),
                    SapsCasNo = GetString(officialUse, "SapsCasNo"),
                    DepartmentNameOccurred = GetString(officialUse, "DepartmentNameOccurred"),
                    InspectedByInitials = GetString(officialUse, "InspectedByInitials"),
                    InspectedByRank = GetString(officialUse, "InspectedByRank"),
                    InspectedBySurname = GetString(officialUse, "InspectedBySurname"),
                    InspectedByServiceNumber = GetString(officialUse, "InspectedByServiceNumber"),
                    InspectedBySignature = GetString(officialUse, "InspectedBySignature"),
                    OfficeWhereReported = GetString(officialUse, "OfficeWhereReported"),
                    DepartmentNameReported = GetString(officialUse, "DepartmentNameReported"),
                    CompletedBy = GetString(officialUse, "CompletedBy"),
                    CompletedInitials = GetString(officialUse, "CompletedInitials"),
                    CompletedRank = GetString(officialUse, "CompletedRank"),
                    CompletedSurname = GetString(officialUse, "CompletedSurname"),
                    CompletedServiceNumber = GetString(officialUse, "CompletedServiceNumber"),
                    CompletedSignature = GetString(officialUse, "CompletedSignature"),
                    CapturingNumber = GetString(officialUse, "CapturingNumber"),
                    Comments = GetString(officialUse, "Comments")
                };

                // Handle dates
                if (officialUse.TryGetProperty("DateStamp", out var dateStampEl) && dateStampEl.ValueKind == JsonValueKind.String)
                    official.DateStamp = DateOnly.TryParse(dateStampEl.GetString(), out var ds) ? ds : null;

                if (officialUse.TryGetProperty("CompletedDate", out var compDateEl) && compDateEl.ValueKind == JsonValueKind.String)
                    official.CompletedDate = DateOnly.TryParse(compDateEl.GetString(), out var cd) ? cd : null;

                if (officialUse.TryGetProperty("CompletedTime", out var compTimeEl) && compTimeEl.ValueKind == JsonValueKind.String)
                    official.CompletedTime = TimeOnly.TryParse(compTimeEl.GetString(), out var ct) ? ct : null;

                _context.OfficialUses.Add(official);
            }

            if (root.TryGetProperty("Attachments", out var attachments) && attachments.ValueKind == JsonValueKind.Array)
            {
                var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "crashes", crash.CrashId.ToString());
                Directory.CreateDirectory(uploadDir);

                var attachmentNotes = GetString(root, "AttachmentNotes");

                foreach (var a in attachments.EnumerateArray())
                {
                    var fileName = GetString(a, "FileName");
                    var fileData = GetString(a, "FileData");
                    if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(fileData)) continue;

                    var safeName = $"{Guid.NewGuid()}_{Path.GetFileName(fileName)}";
                    var fullPath = Path.Combine(uploadDir, safeName);
                    await System.IO.File.WriteAllBytesAsync(fullPath, Convert.FromBase64String(fileData));

                    _context.CrashSketches.Add(new CrashSketch
                    {
                        Crash = crash,
                        SketchType = "attachment",
                        FilePath = $"/uploads/crashes/{crash.CrashId}/{safeName}",
                        Notes = attachmentNotes,
                        CreatedAt = DateTime.Now
                    });
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var successMessage = $"Crash report #{crash.CrashId} saved successfully.";
            if (existsAsSummary)
                successMessage += $" Note: CR number '{crash.CrNo}' also exists as a Quick Add / imported record — " +
                                   "both are saved; reports won't double-count them, but you may want to reconcile the two records.";

            TempData["SuccessMessage"] = successMessage;
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            TempData["ErrorMessage"] = $"An error occurred: {ex.Message} | Inner: {ex.InnerException?.Message}";
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

    private static DateOnly? GetDateOnly(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) &&
            prop.ValueKind == JsonValueKind.String &&
            DateOnly.TryParse(prop.GetString(), out var date))
        {
            return date;
        }

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
                IdType = "RSA_ID",
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

    public async Task<IActionResult> Insights()
    {
        var roleLabel =
            User.IsInRole("System Administrator") ? "System Administrator" :
            User.IsInRole("Provincial Staff") ? "Provincial Staff" :
            User.IsInRole("Regional Staff") ? "Regional Staff" :
            User.IsInRole("Cost Centre Administrator") ? "Cost Centre Administrator" :
            "SAPS Officer";

        // Insights only exists for roles operating at a scale where a
        // pattern is worth showing -- a single station's handful of
        // records wouldn't have one. SAPS Officer and Cost Centre
        // Administrator never had a nav link to this page, but the
        // action itself is also blocked directly, not just hidden.
        if (roleLabel != "System Administrator" && roleLabel != "Provincial Staff" && roleLabel != "Regional Staff")
            return Forbid();

        var userDistrict = User.FindFirst("District")?.Value;

        var to = DateOnly.FromDateTime(DateTime.Today);
        var from = new DateOnly(to.Year, 1, 1);

        var scopeDistrict = roleLabel == "Regional Staff" ? userDistrict : null;

        var vm = await _memoData.BuildInsightsAsync(from, to, scopeDistrict);

        ViewBag.RoleLabel = roleLabel;
        ViewBag.UserDistrict = userDistrict;
        ViewBag.DashboardMode =
            (roleLabel == "System Administrator" || roleLabel == "Provincial Staff") ? "analytics" : "review";

        return View(vm);
    }
}