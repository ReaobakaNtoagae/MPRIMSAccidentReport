using System.Text.Json;
using CrashReport.Data;
using CrashReport.Models;
using CrashReport.Services;
using CrashReport.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CrashReport.ViewModels;

namespace CrashReport.Controllers;

public class CrashesController : Controller
{
    private readonly AppDbContext _context;
    private readonly MonthlyMemoDataService _memoData;
    public CrashesController(AppDbContext context, MonthlyMemoDataService memoData)
    {
        _context = context;
        _memoData = memoData;
    }



    public IActionResult Index() => View();


    [HttpGet]
    public async Task<IActionResult> Search(
        string? keyword = null,
        string? arNo = null,
        string? casNo = null,
        string? sapsStation = null,
        string? route = null,
        string? crashType = null,
        string? severity = null,
        string? province = null,
        string? dateFrom = null,
        string? dateTo = null)
    {
        var query = _context.Crashes
            .Include(c => c.CrashLocations)
            .Include(c => c.CrashConditions)
            .Include(c => c.CrashPeople)
            .Include(c => c.CrashVehicles)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(c =>
                (c.CrNo != null && c.CrNo.ToLower().Contains(kw)) ||
                (c.CasNo != null && c.CasNo.ToLower().Contains(kw)) ||
                (c.RoadNumber != null && c.RoadNumber.ToLower().Contains(kw)) ||
                c.CrashLocations.Any(l =>
                    (l.StreetRoadName != null && l.StreetRoadName.ToLower().Contains(kw)) ||
                    (l.CityTown != null && l.CityTown.ToLower().Contains(kw)) ||
                    (l.Suburb != null && l.Suburb.ToLower().Contains(kw)))
            );
        }

        if (!string.IsNullOrWhiteSpace(arNo))
            query = query.Where(c => c.CrNo != null &&
                c.CrNo.ToLower().Contains(arNo.Trim().ToLower()));

        if (!string.IsNullOrWhiteSpace(casNo))
            query = query.Where(c => c.CasNo != null &&
                c.CasNo.ToLower().Contains(casNo.Trim().ToLower()));

        if (!string.IsNullOrWhiteSpace(sapsStation))
            query = query.Where(c => c.CrNo != null &&
                c.CrNo.ToLower().StartsWith(sapsStation.Trim().ToLower()));

        if (!string.IsNullOrWhiteSpace(route))
            query = query.Where(c => c.RoadNumber != null &&
                c.RoadNumber.ToLower().Contains(route.Trim().ToLower()));

        if (!string.IsNullOrWhiteSpace(crashType))
            query = query.Where(c =>
                c.CrashConditions.Any(cc => cc.CrashType != null &&
                    cc.CrashType.ToLower().Contains(crashType.Trim().ToLower())));

        if (!string.IsNullOrWhiteSpace(province))
            query = query.Where(c => c.ProvinceCode == province);

        if (!string.IsNullOrWhiteSpace(severity))
            query = query.Where(c =>
                c.CrashPeople.Any(p => p.SeverityOfInjury == severity));

        if (DateOnly.TryParse(dateFrom, out var dFrom))
            query = query.Where(c => c.CrashDate >= dFrom);

        if (DateOnly.TryParse(dateTo, out var dTo))
            query = query.Where(c => c.CrashDate <= dTo);

        var data = await query
            .OrderByDescending(c => c.CrashDate)
            .ThenByDescending(c => c.CrashTime)
            .Select(c => new
            {
                c.CrashId,
                c.CrNo,
                c.CasNo,
                c.CrashDate,
                c.CrashTime,
                c.ProvinceCode,
                c.RoadNumber,
                Location = c.CrashLocations
                                 .Select(l => l.CityTown ?? l.StreetRoadName)
                                 .FirstOrDefault(),
                CrashType = c.CrashConditions
                                 .Select(cc => cc.CrashType)
                                 .FirstOrDefault(),
                VehicleCount = c.CrashVehicles.Count,
                PersonCount = c.CrashPeople.Count,
                FatalCount = c.CrashPeople.Count(p => p.SeverityOfInjury == "Fatal"),
                SeriousCount = c.CrashPeople.Count(p => p.SeverityOfInjury == "Serious"),
                SlightCount = c.CrashPeople.Count(p => p.SeverityOfInjury == "Slight")
            })
            .ToListAsync();

        return Json(data);
    }



    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var to = DateOnly.FromDateTime(DateTime.Today);
        var from = to.AddMonths(-3);

        var rows = await _memoData.LoadAsync(from, to);
        var flat = rows
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.Time)
            .Select(r => new
            {
                CrashId = r.Source == "Manual" ? r.CrashId : (int?)null,
                r.CrNo,
                r.Station,
                r.District,
                Date = r.Date.ToString("yyyy-MM-dd"),
                r.Route,
                r.CrashType,
                r.VehicleCount,
                r.Fatalities,
                r.Serious,
                r.Source
            });

        return Json(flat);
    }



    [HttpGet]
    public async Task<IActionResult> FilterOptions()
    {
        var stations = await _context.Crashes
            .Where(c => c.CrNo != null && c.CrNo.Contains("-"))
            .Select(c => c.CrNo!.Substring(0, c.CrNo.IndexOf("-")))
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync();

        var routes = await _context.Crashes
            .Where(c => c.RoadNumber != null)
            .Select(c => c.RoadNumber!)
            .Distinct()
            .OrderBy(r => r)
            .ToListAsync();

        var crashTypes = await _context.CrashConditions
            .Where(cc => cc.CrashType != null)
            .Select(cc => cc.CrashType!)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync();

        return Json(new { stations, routes, crashTypes });
    }



    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var crash = await _context.Crashes
            .Include(c => c.CrashLocations)
            .Include(c => c.CrashConditions)
            .Include(c => c.CrashWeathers)
            .Include(c => c.CrashVehicles)
                .ThenInclude(cv => cv.Vehicle)
            .Include(c => c.CrashVehicles)
                .ThenInclude(cv => cv.DriverPerson)
            .Include(c => c.CrashVehicles)
                .ThenInclude(cv => cv.VehicleDamages)
            .Include(c => c.CrashPeople)
                .ThenInclude(cp => cp.Person)
            .Include(c => c.CrashPeople)
                .ThenInclude(cp => cp.PedestrianBicyclistDetails)
            .Include(c => c.ContributoryFactors)
            .Include(c => c.DangerousGoods)
            .Include(c => c.Witnesses)
            .Include(c => c.OfficialUses)
            .FirstOrDefaultAsync(c => c.CrashId == id);

        if (crash == null) return NotFound();

        Console.WriteLine($"Loaded crash {crash.CrashId}: {crash.CrashVehicles?.Count ?? 0} vehicles, {crash.CrashPeople?.Count ?? 0} people");

        return View(crash);
    }


    public IActionResult Create() =>
        View(new Crash { CrashDate = DateOnly.FromDateTime(DateTime.Today) });



    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("CasNo,CrNo,IncidentReportNo,CapturingNumber,CrashDate,CrashTime," +
              "NoOfAppendices,NoOfVehiclesInvolved,ProvinceCode,SpeedLimitKmh," +
              "RoadNumber,KmMarker,BriefDescription")] Crash crash)
    {
        // Full reports are authoritative and may coexist with an earlier quick/imported
        // summary. Only another full report is a genuine duplicate here.
        if (!string.IsNullOrWhiteSpace(crash.CrNo) &&
            await _context.Crashes.AnyAsync(c => c.CrNo == crash.CrNo))
            ModelState.AddModelError(nameof(crash.CrNo), $"A full accident report with AR/CR number '{crash.CrNo}' already exists.");
        if (ModelState.IsValid)
        {
            _context.Add(crash);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = crash.CrashId });
        }
        return View(crash);
    }



    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var crash = await _context.Crashes.FindAsync(id);
        if (crash == null) return NotFound();
        return View(crash);
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
            try
            {
                _context.Update(crash);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Crashes.Any(c => c.CrashId == id)) return NotFound();
                throw;
            }
            return RedirectToAction(nameof(Details), new { id = crash.CrashId });
        }
        return View(crash);
    }



    [HttpGet]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var crash = await _context.Crashes
            .Include(c => c.CrashLocations)
            .FirstOrDefaultAsync(c => c.CrashId == id);
        if (crash == null) return NotFound();
        return View(crash);
    }


    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Privileges.Crashes.Delete)]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var crash = await _context.Crashes.FindAsync(id);
        if (crash != null) _context.Crashes.Remove(crash);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Grid([FromQuery] CrashGridFilter filter)
    {
        var rows = await _memoData.LoadAsync(filter.From, filter.To);

        if (!string.IsNullOrWhiteSpace(filter.District))
            rows = rows.Where(r => string.Equals(r.District, filter.District, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(filter.Severity))
            rows = rows.Where(r => string.Equals(r.OverallSeverity, filter.Severity, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(filter.Source))
            rows = rows.Where(r => string.Equals(r.Source, filter.Source, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(filter.Station))
            rows = rows.Where(r => r.Station.Contains(filter.Station.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(filter.Route))
            rows = rows.Where(r => r.Route.Contains(filter.Route.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(filter.CrashType))
            rows = rows.Where(r => r.CrashType.Contains(filter.CrashType.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            rows = rows.Where(r =>
                r.CrNo.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.CasNo.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.ArNo.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.Route.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.Station.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.Location.Contains(term, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        rows = Sort(rows, filter.SortBy, filter.SortDesc);

        var total = rows.Count;
        var page = Math.Max(filter.Page, 1);
        var pageSize = filter.PageSize <= 0 ? 50 : filter.PageSize;

        var paged = rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.CrashId,
                r.SummaryId,
                r.CrNo,
                r.CasNo,
                r.ArNo,
                r.Station,
                r.District,
                Date = r.Date.ToString("yyyy-MM-dd"),
                Time = r.Time?.ToString("HH:mm"),
                r.Route,
                r.Location,
                r.CrashType,
                r.VehicleCount,
                r.Fatalities,
                r.Serious,
                r.Slight,
                Severity = r.OverallSeverity,
                r.Source
            });

        return Json(new { total, page, pageSize, rows = paged });
    }


    // ═══════════════════════════════════════════════════════════════
    // EditSummary — now covers vehicles (instance-based) and casualties
    // across all three severities, matching CreateSummaryController.
    // ═══════════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> EditSummary(int? id)
    {
        if (id == null) return NotFound();

        var summary = await _context.CrashSummaries.FindAsync(id);
        if (summary == null) return NotFound();

        var vehicles = await _context.CrashSummaryVehicles
            .Where(v => v.SummaryId == id)
            .OrderBy(v => v.VehicleNumber)
            .Select(v => new VehicleEntryInput
            {
                VehicleNumber = v.VehicleNumber,
                VehicleTypeCode = v.VehicleTypeCode,
                VehicleTypeName = v.VehicleTypeName,
                Registration = v.Registration
            })
            .ToListAsync();

        var injuries = await _context.CrashSummaryInjuries
            .Where(i => i.SummaryId == id)
            .Include(i => i.Vehicle)
            .Select(i => new InjuryEntryInput
            {
                Severity = i.Severity,
                Role = i.Role,
                VehicleNumber = i.Vehicle != null ? i.Vehicle.VehicleNumber : (byte?)null,
                Age = i.Age,
                AgeGroupCode = i.AgeGroupCode,
                Gender = i.Gender,
                Race = i.Race
            })
            .ToListAsync();

        // Only manually quick-captured summaries use this editor. Imported summaries
        // retain their detailed editor; Full Capture has its own separate edit action.
        if (summary.SourceFile != "Quick add (manual entry)")
            return View(new EditSummaryViewModel { Summary = summary, Vehicles = vehicles, Injuries = injuries });

        var station = await _context.SapsStations.AsNoTracking().Include(s => s.DistrictLookup)
            .FirstOrDefaultAsync(s => s.StationName == summary.Station);
        // Both quick actions share one view, including the existing lookup selections.
        ViewData["QuickCaptureInitial"] = new { data = summary, vehicles,
            district = station?.DistrictLookup?.DistrictName ?? station?.District,
            arSequence = QuickCaptureIdentifier.ExtractArSequence(summary.CrNo),
            protectedVehicles = injuries.Where(i => i.Severity != "Fatal" && i.VehicleNumber.HasValue)
                .Select(i => i.VehicleNumber).Distinct().ToArray(),
            // Quick Capture now edits all severities. Serious and slight entries
            // expose their road-user role; fatal entries also expose demographics.
            people = injuries.ToArray() };
        ViewData["QuickCaptureEdit"] = true;
        return View("~/Views/CreateSummary/QuickCapture.cshtml", summary);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Privileges.Crashes.Edit)]
    public async Task<IActionResult> EditSummary(
            CrashSummary model, string? vehiclesJson, string? injuriesJson)
    {
        var summary = await _context.CrashSummaries.FindAsync(model.SummaryId);
        if (summary == null) return NotFound();

        try
        {
            model.CrNo = QuickCaptureIdentifier.FormatAr(model.CrNo, model.CrashDate);
            model.CasNo = QuickCaptureIdentifier.FormatCas(model.CasNo, model.CrashDate);
        }
        catch (ArgumentException ex) { return Json(new { success = false, message = ex.Message }); }
        if (await _context.CrashSummaries.AnyAsync(s => s.SummaryId != model.SummaryId && s.Station == model.Station && s.CrNo == model.CrNo))
            return Json(new { success = false, message = $"AR number '{model.CrNo}' already exists for {model.Station}." });
        if (model.CasNo is not null && await _context.CrashSummaries.AnyAsync(s => s.SummaryId != model.SummaryId && s.Station == model.Station && s.CasNo == model.CasNo))
            return Json(new { success = false, message = $"CAS number '{model.CasNo}' already exists for {model.Station}." });
        if (!ModelState.IsValid)
            return Json(new { success = false, message = "Check the values entered before saving." });

        if (string.IsNullOrWhiteSpace(model.Station))
            return Json(new { success = false, message = "Station is required." });

        if (string.IsNullOrWhiteSpace(model.CrNo))
            return Json(new { success = false, message = "CR number is required." });

        var vehicles = new List<VehicleEntryInput>();
        if (!string.IsNullOrWhiteSpace(vehiclesJson))
        {
            try
            {
                vehicles = JsonSerializer.Deserialize<List<VehicleEntryInput>>(vehiclesJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }
            catch
            {
                return Json(new { success = false, message = "Vehicle details could not be read. Please try again." });
            }
        }

        foreach (var v in vehicles)
        {
            if (string.IsNullOrWhiteSpace(v.VehicleTypeCode))
                return Json(new { success = false, message = $"Vehicle {v.VehicleNumber}: vehicle type is required." });
        }

        var vehicleNumbers = vehicles.Select(v => v.VehicleNumber).ToList();
        if (vehicleNumbers.Distinct().Count() != vehicleNumbers.Count)
            return Json(new { success = false, message = "Each vehicle must have a unique vehicle number." });

        var injuries = new List<InjuryEntryInput>();
        if (!string.IsNullOrWhiteSpace(injuriesJson))
        {
            try
            {
                injuries = JsonSerializer.Deserialize<List<InjuryEntryInput>>(injuriesJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }
            catch
            {
                return Json(new { success = false, message = "Casualty details could not be read. Please try again." });
            }
        }

        // Compatibility for the previous totals-only Quick Capture screen. The new
        // role-based screen sends every severity, so it does not use this marker.
        if (Request.Form.ContainsKey("TotalsOnlyEditor"))
        {
            var historical = await _context.CrashSummaryInjuries.AsNoTracking()
                .Where(i => i.SummaryId == model.SummaryId && i.Severity != "Fatal")
                .Select(i => new InjuryEntryInput { Severity = i.Severity, Role = i.Role,
                    Age = i.Age, AgeGroupCode = i.AgeGroupCode, Gender = i.Gender, Race = i.Race,
                    VehicleNumber = i.Vehicle != null ? i.Vehicle.VehicleNumber : (byte?)null }).ToListAsync();
            if ((historical.Any(i => i.Severity == "Serious") && model.SeriousInjuriesTotal != historical.Count(i => i.Severity == "Serious")) ||
                (historical.Any(i => i.Severity == "Slight") && model.SlightInjuriesTotal != historical.Count(i => i.Severity == "Slight")))
                return Json(new { success = false, message = "These totals have existing casualty details. Reconcile that breakdown before changing the totals." });
            injuries.AddRange(historical);
        }
        if (injuries.Count > 255 || vehicles.Count > 255)
            return Json(new { success = false, message = "Too many casualty or vehicle entries for a quick record." });
        var validSeverities = new[] { "Fatal", "Serious", "Slight" };
        var validRoles = new[] { "Driver", "Passenger", "Pedestrian", "Cyclist" };

        foreach (var inj in injuries)
        {
            if (inj.AgeGroupCode != null && !new[] { "0-7", "8-12", "13-18", "19-35", "36+" }.Contains(inj.AgeGroupCode))
                return Json(new { success = false, message = "Select a valid age group." });
            if (!validSeverities.Contains(inj.Severity))
                return Json(new { success = false, message = $"Each casualty needs a valid severity (Fatal, Serious, or Slight) — got '{inj.Severity}'." });

            if (inj.Severity != "Fatal" && !validRoles.Contains(inj.Role))
                return Json(new { success = false, message = $"Each casualty needs a valid role (Driver, Passenger, Pedestrian, or Cyclist) — got '{inj.Role}'." });
            if (inj.Role is not null && !validRoles.Contains(inj.Role))
                return Json(new { success = false, message = "Select a valid road user when fatality details are supplied." });

            if (inj.Age.HasValue && (inj.Age.Value < 0 || inj.Age.Value > 120))
                return Json(new { success = false, message = $"Age {inj.Age} is out of range (0–120)." });

            if (!string.IsNullOrEmpty(inj.Gender) && inj.Gender != "M" && inj.Gender != "F")
                return Json(new { success = false, message = "Gender must be M or F if provided." });

            if (!string.IsNullOrEmpty(inj.Race) && !new[] { "B", "C", "I", "W", "O" }.Contains(inj.Race))
                return Json(new { success = false, message = "Race must be B, C, I, W, or O if provided." });

            if (inj.Role == "Driver" || inj.Role == "Passenger")
            {
                // Simplified Quick Capture can leave the occupied vehicle unknown.
                if (inj.VehicleNumber.HasValue && !vehicleNumbers.Contains(inj.VehicleNumber.Value))
                    return Json(new { success = false, message = $"A {inj.Role.ToLower()} casualty must reference one of the vehicles entered above." });
            }
            else if (inj.VehicleNumber != null)
            {
                return Json(new { success = false, message = $"A {inj.Role.ToLower()} casualty cannot be linked to a vehicle." });
            }
        }

        // ── Recompute all 12 counts server-side, same as CreateSummary —
        // never trust submitted counts directly, the injuries list is the
        // real source of truth. ─────────────────────────────────────────
        int Count(string severity, string role) =>
            injuries.Count(i => i.Severity == severity && i.Role == role);

        var roleBasedEditor = Request.Form.ContainsKey("RoleBasedInjuryEditor");
        var seriousRows = injuries.Count(i => i.Severity == "Serious");
        var slightRows = injuries.Count(i => i.Severity == "Slight");
        if (roleBasedEditor &&
            (model.SeriousInjuriesTotal != seriousRows || model.SlightInjuriesTotal != slightRows))
            return Json(new { success = false, message = "Injury totals must match the road-user details supplied." });

        summary.Station = model.Station;
        if (Request.Form.ContainsKey("TotalsOnlyEditor") || roleBasedEditor)
        {
            summary.SeriousInjuriesTotal = model.SeriousInjuriesTotal;
            summary.SlightInjuriesTotal = model.SlightInjuriesTotal;
        }
        summary.CasNo = model.CasNo;
        summary.CrNo = model.CrNo;
        summary.CrashDate = model.CrashDate;
        summary.CrashTime = model.CrashTime;
        summary.Route = model.Route;
        summary.Location = model.Location;
        summary.CrashType = model.CrashType;
        summary.VehicleCount = (byte)vehicles.Count;
        summary.FatalitiesTotal = injuries.Count(i => i.Severity == "Fatal");

        summary.FatalDrivers = (byte)Count("Fatal", "Driver");
        summary.FatalPassengers = (byte)Count("Fatal", "Passenger");
        summary.FatalPedestrians = (byte)Count("Fatal", "Pedestrian");
        summary.FatalCyclists = (byte)Count("Fatal", "Cyclist");

        summary.SeriousDrivers = (byte)Count("Serious", "Driver");
        summary.SeriousPassengers = (byte)Count("Serious", "Passenger");
        summary.SeriousPedestrians = (byte)Count("Serious", "Pedestrian");
        summary.SeriousCyclists = (byte)Count("Serious", "Cyclist");

        summary.SlightDrivers = (byte)Count("Slight", "Driver");
        summary.SlightPassengers = (byte)Count("Slight", "Passenger");
        summary.SlightPedestrians = (byte)Count("Slight", "Pedestrian");
        summary.SlightCyclists = (byte)Count("Slight", "Cyclist");

        // Rebuilt from scratch, not incremented — an edit must replace
        // the old totals, not add to them.
        summary.FatalMale = 0; summary.FatalFemale = 0;
        summary.FatalAge0to7 = 0; summary.FatalAge8to12 = 0; summary.FatalAge13to18 = 0;
        summary.FatalAge19to35 = 0; summary.FatalAge36Plus = 0;
        summary.FatalAfrican = 0; summary.FatalColoured = 0; summary.FatalIndian = 0;
        summary.FatalWhite = 0; summary.FatalOtherRace = 0;

        foreach (var inj in injuries.Where(i => i.Severity == "Fatal"))
        {
            // Keep grouped ages captured by the simplified form when editing a record.
            if (!inj.Age.HasValue)
            {
                switch (inj.AgeGroupCode)
                {
                    case "0-7": summary.FatalAge0to7++; break;
                    case "8-12": summary.FatalAge8to12++; break;
                    case "13-18": summary.FatalAge13to18++; break;
                    case "19-35": summary.FatalAge19to35++; break;
                    case "36+": summary.FatalAge36Plus++; break;
                }
            }
            if (inj.Age.HasValue)
            {
                var age = inj.Age.Value;
                if (age <= 7) summary.FatalAge0to7++;
                else if (age <= 12) summary.FatalAge8to12++;
                else if (age <= 18) summary.FatalAge13to18++;
                else if (age <= 35) summary.FatalAge19to35++;
                else summary.FatalAge36Plus++;
            }

            if (inj.Gender == "M") summary.FatalMale++;
            else if (inj.Gender == "F") summary.FatalFemale++;

            switch (inj.Race)
            {
                case "B": summary.FatalAfrican++; break;
                case "C": summary.FatalColoured++; break;
                case "I": summary.FatalIndian++; break;
                case "W": summary.FatalWhite++; break;
                case "O": summary.FatalOtherRace++; break;
            }
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Injuries deleted BEFORE vehicles — injuries reference
            // vehicles by FK (NO ACTION, not CASCADE), so vehicles can't
            // be removed while injuries still point at them.
            var existingInjuries = _context.CrashSummaryInjuries.Where(i => i.SummaryId == summary.SummaryId);
            _context.CrashSummaryInjuries.RemoveRange(existingInjuries);
            await _context.SaveChangesAsync();

            var existingVehicles = _context.CrashSummaryVehicles.Where(v => v.SummaryId == summary.SummaryId);
            _context.CrashSummaryVehicles.RemoveRange(existingVehicles);
            await _context.SaveChangesAsync();

            var vehicleNumberToId = new Dictionary<byte, int>();
            foreach (var v in vehicles)
            {
                var entity = new CrashSummaryVehicle
                {
                    SummaryId = summary.SummaryId,
                    VehicleNumber = v.VehicleNumber,
                    VehicleTypeCode = v.VehicleTypeCode,
                    VehicleTypeName = v.VehicleTypeName,
                    Registration = string.IsNullOrWhiteSpace(v.Registration) ? null : v.Registration
                };
                _context.CrashSummaryVehicles.Add(entity);
                await _context.SaveChangesAsync();
                vehicleNumberToId[v.VehicleNumber] = entity.VehicleId;
            }

            foreach (var inj in injuries)
            {
                _context.CrashSummaryInjuries.Add(new CrashSummaryInjury
                {
                    SummaryId = summary.SummaryId,
                    VehicleId = inj.VehicleNumber.HasValue ? vehicleNumberToId[inj.VehicleNumber.Value] : null,
                    Severity = inj.Severity,
                    Role = inj.Role,
                    Age = (byte?)inj.Age,
                    AgeGroupCode = inj.AgeGroupCode,
                    Gender = string.IsNullOrEmpty(inj.Gender) ? null : inj.Gender,
                    Race = string.IsNullOrEmpty(inj.Race) ? null : inj.Race
                });
            }
            if (injuries.Count > 0)
                await _context.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return Json(new
            {
                success = false,
                message = $"Something went wrong while saving: {ex.Message} | Inner: {ex.InnerException?.Message}"
            });
        }

        return Json(new { success = true, message = $"Crash record '{summary.CrNo}' has been updated." });
    }

    // Deletes in dependency order — injuries, then vehicles, then the
    // summary itself. This is REQUIRED now, not a style choice: the FKs
    // from crash_summary_injuries and crash_summary_vehicles back to
    // crash_summaries are NO ACTION (not CASCADE) — that was the fix for
    // the "multiple cascade paths" SQL Server error when the schema was
    // first built. A bare Remove(summary) will now throw an FK violation
    // if any vehicles or injuries still reference it.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Privileges.Crashes.Delete)]
    public async Task<IActionResult> DeleteSummary(int id)
    {
        var summary = await _context.CrashSummaries.FindAsync(id);
        if (summary == null) return NotFound();

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var injuries = _context.CrashSummaryInjuries.Where(i => i.SummaryId == id);
            _context.CrashSummaryInjuries.RemoveRange(injuries);
            await _context.SaveChangesAsync();

            var vehicles = _context.CrashSummaryVehicles.Where(v => v.SummaryId == id);
            _context.CrashSummaryVehicles.RemoveRange(vehicles);
            await _context.SaveChangesAsync();

            _context.CrashSummaries.Remove(summary);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }

        return RedirectToAction(nameof(Index));
    }

    private static List<Row> Sort(List<Row> rows, string sortBy, bool desc)
    {
        Func<Row, object> key = sortBy switch
        {
            "CrNo" => r => r.CrNo,
            "District" => r => r.District,
            "Station" => r => r.Station,
            "Severity" => r => r.OverallSeverity,
            "Source" => r => r.Source,
            _ => r => r.Date
        };

        return desc
            ? rows.OrderByDescending(key).ToList()
            : rows.OrderBy(key).ToList();
    }


    private bool CrashExists(int id) =>
        _context.Crashes.Any(c => c.CrashId == id);
}
