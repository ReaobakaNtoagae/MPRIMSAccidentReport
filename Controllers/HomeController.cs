using CrashReport.Data;
using CrashReport.Models;
using CrashReport.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CrashReport.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _context;

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

        // This month stats
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
    public async Task<IActionResult> Delete(int id)
    {
        var crash = await _context.Crashes
            .Include(c => c.CrashLocations)
            .Include(c => c.CrashConditions)
            .Include(c => c.CrashWeathers)
            .Include(c => c.CrashVehicles)
                .ThenInclude(cv => cv.VehicleDamages)
            .Include(c => c.CrashVehicles)
                .ThenInclude(cv => cv.CrashPeople)
            .Include(c => c.CrashPeople)
                .ThenInclude(cp => cp.PedestrianBicyclistDetails)
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
    public async Task<IActionResult> Submit([FromForm] string formJson)
    {
        if (string.IsNullOrEmpty(formJson))
            return BadRequest("No form data received.");

        var vm = JsonSerializer.Deserialize<CrashReportFormViewModel>(formJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (vm == null) return BadRequest("Invalid form data.");

        var crash = new Crash
        {
            CasNo = vm.CrashInfo.CasNo,
            CrNo = vm.CrashInfo.CrNo,
            IncidentReportNo = vm.CrashInfo.IncidentReportNo,
            CapturingNumber = vm.CrashInfo.CapturingNumber,
            CrashDate = DateOnly.TryParse(vm.CrashInfo.CrashDate, out var d)
                                       ? d : DateOnly.FromDateTime(DateTime.Today),
            CrashTime = TimeOnly.TryParse(vm.CrashInfo.CrashTime, out var t)
                                       ? t : null,
            ProvinceCode = vm.CrashInfo.ProvinceCode,
            SpeedLimitKmh = vm.CrashInfo.SpeedLimitKmh,
            RoadNumber = vm.CrashInfo.RoadNumber,
            KmMarker = vm.CrashInfo.KmMarker,
            NoOfVehiclesInvolved = vm.CrashInfo.NoOfVehiclesInvolved,
            NoOfAppendices = vm.CrashInfo.NoOfAppendices,
            BriefDescription = vm.CrashInfo.BriefDescription
        };
        _context.Crashes.Add(crash);
        await _context.SaveChangesAsync();

        var loc = vm.Location;
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

        var cond = vm.Conditions;
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

        foreach (var w in cond.WeatherConditions)
            _context.CrashWeathers.Add(new CrashWeather
            { CrashId = crash.CrashId, WeatherCondition = w });

        foreach (var ve in vm.Vehicles)
        {
            Person? driver = null;
            if (!string.IsNullOrEmpty(ve.DriverSurname))
            {
                driver = new Person
                {
                    IdType = ve.DriverIdType ?? "RSA_ID",
                    IdNumber = ve.DriverIdNumber,
                    Surname = ve.DriverSurname,
                    FullNames = ve.DriverFullNames ?? "",
                    CellPhone = ve.DriverCellPhone
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

        foreach (var pe in vm.Persons)
        {
            if (string.IsNullOrEmpty(pe.Surname)) continue;

            var person = new Person
            {
                IdType = "RSA_ID",
                IdNumber = pe.IdNumber,
                Surname = pe.Surname,
                FullNames = pe.FullNames ?? "",
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

        foreach (var f in vm.Factors)
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

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Crash report #{crash.CrashId} saved successfully.";
        return RedirectToAction(nameof(Index));
    }
}