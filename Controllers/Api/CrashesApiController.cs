using CrashReport.Data;
using CrashReport.Models;
using CrashReport.Models.Dtos;
using CrashReport.Security;
using CrashReport.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Controllers.Api;


[Route("api/crashes")]
[ApiController]
public class CrashesApiController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly MonthlyMemoDataService _memoData;
    private readonly ICrashSummaryValidationService _summaryValidation;
    private readonly ICrashCaptureService _capture;
    private readonly ICrashGridService _grid;

    public CrashesApiController(AppDbContext context, MonthlyMemoDataService memoData,
        ICrashSummaryValidationService summaryValidation, ICrashCaptureService capture, ICrashGridService grid)
    {
        _context = context;
        _memoData = memoData;
        _summaryValidation = summaryValidation;
        _capture = capture;
        _grid = grid;
    }

    [HttpGet]
    [Authorize(Policy = Privileges.Crashes.View)]
    public async Task<IActionResult> Search(
        string? keyword = null, string? arNo = null, string? casNo = null, string? sapsStation = null,
        string? route = null, string? crashType = null, string? severity = null, string? province = null,
        string? dateFrom = null, string? dateTo = null)
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
            query = query.Where(c => c.CrNo != null && c.CrNo.ToLower().Contains(arNo.Trim().ToLower()));

        if (!string.IsNullOrWhiteSpace(casNo))
            query = query.Where(c => c.CasNo != null && c.CasNo.ToLower().Contains(casNo.Trim().ToLower()));

        if (!string.IsNullOrWhiteSpace(sapsStation))
            query = query.Where(c => c.CrNo != null && c.CrNo.ToLower().StartsWith(sapsStation.Trim().ToLower()));

        if (!string.IsNullOrWhiteSpace(route))
            query = query.Where(c => c.RoadNumber != null && c.RoadNumber.ToLower().Contains(route.Trim().ToLower()));

        if (!string.IsNullOrWhiteSpace(crashType))
            query = query.Where(c => c.CrashConditions.Any(cc => cc.CrashType != null && cc.CrashType.ToLower().Contains(crashType.Trim().ToLower())));

        if (!string.IsNullOrWhiteSpace(province))
            query = query.Where(c => c.ProvinceCode == province);

        if (!string.IsNullOrWhiteSpace(severity))
            query = query.Where(c => c.CrashPeople.Any(p => p.SeverityOfInjury == severity));

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
                Location = c.CrashLocations.Select(l => l.CityTown ?? l.StreetRoadName).FirstOrDefault(),
                CrashType = c.CrashConditions.Select(cc => cc.CrashType).FirstOrDefault(),
                VehicleCount = c.CrashVehicles.Count,
                PersonCount = c.CrashPeople.Count,
                FatalCount = c.CrashPeople.Count(p => p.SeverityOfInjury == "Fatal"),
                SeriousCount = c.CrashPeople.Count(p => p.SeverityOfInjury == "Serious"),
                SlightCount = c.CrashPeople.Count(p => p.SeverityOfInjury == "Slight")
            })
            .ToListAsync();

        return Ok(data);
    }

    [HttpGet("recent")]
    [Authorize(Policy = Privileges.Crashes.View)]
    public async Task<IActionResult> Recent()
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

        return Ok(flat);
    }

    [HttpGet("filter-options")]
    [Authorize(Policy = Privileges.Crashes.View)]
    public async Task<IActionResult> FilterOptions()
    {
        var stations = await _context.Crashes
            .Where(c => c.CrNo != null && c.CrNo.Contains("-"))
            .Select(c => c.CrNo!.Substring(0, c.CrNo.IndexOf("-")))
            .Distinct().OrderBy(s => s).ToListAsync();

        var routes = await _context.Crashes
            .Where(c => c.RoadNumber != null)
            .Select(c => c.RoadNumber!)
            .Distinct().OrderBy(r => r).ToListAsync();

        var crashTypes = await _context.CrashConditions
            .Where(cc => cc.CrashType != null)
            .Select(cc => cc.CrashType!)
            .Distinct().OrderBy(t => t).ToListAsync();

        return Ok(new { stations, routes, crashTypes });
    }

    [HttpGet("grid")]
    [Authorize(Policy = Privileges.Crashes.View)]
    public async Task<IActionResult> Grid([FromQuery] CrashGridFilter filter)
    {
        var result = await _grid.BuildAsync(filter);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = Privileges.Crashes.View)]
    public async Task<IActionResult> Details(int id)
    {
        var crash = await _context.Crashes
            .Include(c => c.CrashLocations)
            .Include(c => c.CrashConditions)
            .Include(c => c.CrashWeathers)
            .Include(c => c.CrashVehicles).ThenInclude(cv => cv.Vehicle)
            .Include(c => c.CrashVehicles).ThenInclude(cv => cv.DriverPerson)
            .Include(c => c.CrashVehicles).ThenInclude(cv => cv.VehicleDamages)
            .Include(c => c.CrashPeople).ThenInclude(cp => cp.Person)
            .Include(c => c.CrashPeople).ThenInclude(cp => cp.PedestrianBicyclistDetails)
            .Include(c => c.ContributoryFactors)
            .Include(c => c.DangerousGoods)
            .Include(c => c.Witnesses)
            .Include(c => c.OfficialUses)
            .FirstOrDefaultAsync(c => c.CrashId == id);

        return crash == null ? NotFound() : Ok(crash);
    }

    
    [HttpPost]
    [Authorize(Policy = Privileges.Crashes.Create)]
    public async Task<IActionResult> Create([FromBody] CrashCoreFieldsUpdateRequest request)
    {
        var crash = ToEntity(0, request);
        var (success, duplicateError, crashId) = await _capture.CreateCoreOnlyAsync(crash);
        return success
            ? CreatedAtAction(nameof(Details), new { id = crashId }, new { crashId })
            : Conflict(new { message = duplicateError });
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Privileges.Crashes.Edit)]
    public async Task<IActionResult> Update(int id, [FromBody] CrashCoreFieldsUpdateRequest request)
    {
        var updated = await _capture.UpdateCoreFieldsAsync(id, ToEntity(id, request));
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Privileges.Crashes.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _capture.DeleteCrashAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    private static Crash ToEntity(int id, CrashCoreFieldsUpdateRequest request) => new()
    {
        CrashId = id,
        CasNo = request.CasNo,
        CrNo = request.CrNo,
        IncidentReportNo = request.IncidentReportNo,
        CapturingNumber = request.CapturingNumber,
        CrashDate = request.CrashDate ?? default,
        CrashTime = request.CrashTime,
        NoOfAppendices = request.NoOfAppendices,
        NoOfVehiclesInvolved = request.NoOfVehiclesInvolved,
        ProvinceCode = request.ProvinceCode,
        SpeedLimitKmh = request.SpeedLimitKmh,
        RoadNumber = request.RoadNumber,
        KmMarker = request.KmMarker,
        BriefDescription = request.BriefDescription
    };



    [HttpGet("summaries/{id:int}")]
    [Authorize(Policy = Privileges.Crashes.View)]
    public async Task<IActionResult> GetSummary(int id)
    {
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

        var isQuickCapture = summary.SourceFile == "Quick add (manual entry)";
        string? district = null;
        if (isQuickCapture)
        {
            var station = await _context.SapsStations.AsNoTracking().Include(s => s.DistrictLookup)
                .FirstOrDefaultAsync(s => s.StationName == summary.Station);
            district = station?.DistrictLookup?.DistrictName ?? station?.District;
        }

        return Ok(new
        {
            summary,
            vehicles,
            injuries,
            isQuickCapture,
            district,
            arSequence = QuickCaptureIdentifier.ExtractArSequence(summary.CrNo),
            protectedVehicles = injuries.Where(i => i.Severity != "Fatal" && i.VehicleNumber.HasValue)
                .Select(i => i.VehicleNumber).Distinct().ToArray()
        });
    }

    [HttpPut("summaries/{id:int}")]
    [Authorize(Policy = Privileges.Crashes.Edit)]
    public async Task<IActionResult> UpdateSummary(int id, [FromBody] QuickCaptureSummaryRequest request)
    {
        var model = request.Summary;
        model.SummaryId = id;

        var summary = await _context.CrashSummaries.FindAsync(id);
        if (summary == null) return NotFound();

        var isQuickCapture = summary.SourceFile == "Quick add (manual entry)";
        if (isQuickCapture && (!model.CostCentreId.HasValue ||
            !await _context.LookupCostCentres.AnyAsync(c => c.CostCentreId == model.CostCentreId.Value)))
            return BadRequest(new { message = "Select a valid cost centre." });

        try
        {
            model.CrNo = QuickCaptureIdentifier.FormatAr(model.CrNo, model.CrashDate);
            model.CasNo = QuickCaptureIdentifier.FormatCas(model.CasNo, model.CrashDate);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }

        if (await _context.CrashSummaries.AnyAsync(s => s.SummaryId != id && s.Station == model.Station && s.CrNo == model.CrNo))
            return Conflict(new { message = $"AR number '{model.CrNo}' already exists for {model.Station}." });
        if (model.CasNo is not null && await _context.CrashSummaries.AnyAsync(s => s.SummaryId != id && s.Station == model.Station && s.CasNo == model.CasNo))
            return Conflict(new { message = $"CAS number '{model.CasNo}' already exists for {model.Station}." });

        if (string.IsNullOrWhiteSpace(model.Station)) return BadRequest(new { message = "Station is required." });
        if (string.IsNullOrWhiteSpace(model.CrNo)) return BadRequest(new { message = "CR number is required." });

        var vehicles = request.Vehicles;
        var vehicleError = _summaryValidation.ValidateVehicles(vehicles, out var vehicleNumbers);
        if (vehicleError != null) return BadRequest(new { message = vehicleError });

        var injuries = request.Injuries;
        if (request.TotalsOnlyEditor)
        {
            var historical = await _context.CrashSummaryInjuries.AsNoTracking()
                .Where(i => i.SummaryId == id && i.Severity != "Fatal")
                .Select(i => new InjuryEntryInput
                {
                    Severity = i.Severity,
                    Role = i.Role,
                    Age = i.Age,
                    AgeGroupCode = i.AgeGroupCode,
                    Gender = i.Gender,
                    Race = i.Race,
                    VehicleNumber = i.Vehicle != null ? i.Vehicle.VehicleNumber : (byte?)null
                }).ToListAsync();
            if ((historical.Any(i => i.Severity == "Serious") && model.SeriousInjuriesTotal != historical.Count(i => i.Severity == "Serious")) ||
                (historical.Any(i => i.Severity == "Slight") && model.SlightInjuriesTotal != historical.Count(i => i.Severity == "Slight")))
                return BadRequest(new { message = "These totals have existing casualty details. Reconcile that breakdown before changing the totals." });
            injuries = injuries.Concat(historical).ToList();
        }

        var injuryError = _summaryValidation.ValidateInjuries(injuries, vehicles, vehicleNumbers);
        if (injuryError != null) return BadRequest(new { message = injuryError });

        var totalsError = _summaryValidation.ValidateInjuryTotalsAgreement(
            injuries, model, request.RoleBasedEditor, enforceLegacyNonRoleTotalsCheck: false);
        if (totalsError != null) return BadRequest(new { message = "Injury totals must match the road-user details supplied." });

        summary.Station = model.Station;
        if (isQuickCapture)
        {
            summary.CostCentreId = model.CostCentreId;
            summary.NoInjuriesTotal = model.NoInjuriesTotal;
        }
        if (request.TotalsOnlyEditor || request.RoleBasedEditor)
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

        _summaryValidation.ApplyInjuryRollup(summary, injuries, vehicles.Count);

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await _summaryValidation.DeleteVehiclesAndInjuriesAsync(id);
            var vehicleNumberToId = await _summaryValidation.SaveVehiclesAsync(id, vehicles);
            await _summaryValidation.SaveInjuriesAsync(id, injuries, vehicleNumberToId);
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = $"Something went wrong while saving: {ex.Message} | Inner: {ex.InnerException?.Message}" });
        }

        return Ok(new { message = $"Crash record '{summary.CrNo}' has been updated." });
    }

    [HttpDelete("summaries/{id:int}")]
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

        return NoContent();
    }
}
