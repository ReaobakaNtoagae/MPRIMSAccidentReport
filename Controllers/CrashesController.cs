using System.Text.Json;
using CrashReport.Data;
using CrashReport.Models;
using CrashReport.Models.Dtos;
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
    private readonly ICrashSummaryValidationService _summaryValidation;
    private readonly ICrashCaptureService _capture;
    private readonly ICrashGridService _grid;
    public CrashesController(AppDbContext context, MonthlyMemoDataService memoData,
        ICrashSummaryValidationService summaryValidation, ICrashCaptureService capture, ICrashGridService grid)
    {
        _context = context;
        _memoData = memoData;
        _summaryValidation = summaryValidation;
        _capture = capture;
        _grid = grid;
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
        if (ModelState.IsValid)
        {
            var (success, duplicateError, crashId) = await _capture.CreateCoreOnlyAsync(crash);
            if (success) return RedirectToAction(nameof(Details), new { id = crashId });
            ModelState.AddModelError(nameof(crash.CrNo), duplicateError!);
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
                if (!await _capture.UpdateCoreFieldsAsync(id, crash)) return NotFound();
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
        // Previously a bare _context.Crashes.Remove(crash) with no cascade cleanup —
        // found while extracting ICrashCaptureService: Crash's dependent tables use
        // the same NO ACTION (not CASCADE) FK pattern documented above for
        // CrashSummary (multiple-cascade-paths avoidance), so this would throw an
        // FK-violation exception for any crash with related vehicles/people/etc. —
        // i.e. any real submitted crash. Now shares the same cascade delete
        // HomeController.Delete already did correctly.
        await _capture.DeleteCrashAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Grid([FromQuery] CrashGridFilter filter)
    {
        var result = await _grid.BuildAsync(filter);
        return Json(new { total = result.Total, page = result.Page, pageSize = result.PageSize, rows = result.Rows });
    }


    // ═══════════════════════════════════════════════════════════════
    // EditSummary — now covers vehicles (instance-based) and casualties
    // across all three severities, matching CreateSummaryController.
    //
    // Every CrashSummary — manually quick-captured or imported — is edited
    // through Quick Capture's own page now. There used to be a second,
    // separate "detailed" editor (Views/Crashes/EditSummary.cshtml) gated
    // on SourceFile != "Quick add (manual entry)", kept only for imports.
    // The data this branch builds (station/district lookup, arSequence,
    // protectedVehicles, people) was already source-agnostic — it read
    // straight off whatever summary/vehicles/injuries were passed in — so
    // the gate was the only thing keeping two editors alive for one record
    // type, and the unused one had already drifted out of date. Full
    // Capture (Crashes.Edit / Home.Create) is a different flow entirely
    // and is untouched by this.
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
                Make = v.Make,
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

        var isQuickCapture = summary.SourceFile == "Quick add (manual entry)";
        if (isQuickCapture && (!model.CostCentreId.HasValue ||
            !await _context.LookupCostCentres.AnyAsync(c => c.CostCentreId == model.CostCentreId.Value)))
            return Json(new { success = false, message = "Select a valid cost centre." });

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

        var vehicleError = _summaryValidation.ValidateVehicles(vehicles, out var vehicleNumbers);
        if (vehicleError != null)
            return Json(new { success = false, message = vehicleError });

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
        var injuryError = _summaryValidation.ValidateInjuries(injuries, vehicles, vehicleNumbers);
        if (injuryError != null)
            return Json(new { success = false, message = injuryError });

        // Recompute all 12 counts server-side, same as CreateSummary — never trust
        // submitted counts directly, the injuries list is the real source of truth.
        var roleBasedEditor = Request.Form.ContainsKey("RoleBasedInjuryEditor");
        var totalsError = _summaryValidation.ValidateInjuryTotalsAgreement(
            injuries, model, roleBasedEditor, enforceLegacyNonRoleTotalsCheck: false);
        if (totalsError != null)
            return Json(new { success = false, message = "Injury totals must match the road-user details supplied." });

        summary.Station = model.Station;
        if (isQuickCapture)
        {
            summary.CostCentreId = model.CostCentreId;
            summary.NoInjuriesTotal = model.NoInjuriesTotal;
        }
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

        // Recomputes VehicleCount, FatalitiesTotal, all 12 role×severity counts, and
        // the fatal-only demographic rollup — zeroes the demographic fields first,
        // which is required here (an edit must replace old totals, not add to them)
        // and a no-op for CreateSummary's freshly-constructed CrashSummary.
        _summaryValidation.ApplyInjuryRollup(summary, injuries, vehicles.Count);

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Injuries deleted BEFORE vehicles — injuries reference vehicles by FK
            // (NO ACTION, not CASCADE), so vehicles can't be removed while injuries
            // still point at them.
            await _summaryValidation.DeleteVehiclesAndInjuriesAsync(summary.SummaryId);

            var vehicleNumberToId = await _summaryValidation.SaveVehiclesAsync(summary.SummaryId, vehicles);
            await _summaryValidation.SaveInjuriesAsync(summary.SummaryId, injuries, vehicleNumberToId);

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
            await _summaryValidation.DeleteVehiclesAndInjuriesAsync(id);

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

    // Sort(...) used to live here -- moved into ICrashGridService.BuildAsync along
    // with the rest of Grid's filter/page pipeline, so CrashesApiController.Grid
    // can share it instead of re-implementing the same column-sort switch.

    private bool CrashExists(int id) =>
        _context.Crashes.Any(c => c.CrashId == id);
}
