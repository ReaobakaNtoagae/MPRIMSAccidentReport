using CrashReport.Data;
using CrashReport.Models;
using CrashReport.Services;
using CrashReport.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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


    [Authorize(Policy = Privileges.Crashes.View)]
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

        // ── Keyword (searches AR No, CAS, location, route)
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

        // ── AR Number
        if (!string.IsNullOrWhiteSpace(arNo))
            query = query.Where(c => c.CrNo != null &&
                c.CrNo.ToLower().Contains(arNo.Trim().ToLower()));

        // ── CAS Number
        if (!string.IsNullOrWhiteSpace(casNo))
            query = query.Where(c => c.CasNo != null &&
                c.CasNo.ToLower().Contains(casNo.Trim().ToLower()));

        // ── SAPS Station (stored as prefix of CrNo: "TONGA-01")
        if (!string.IsNullOrWhiteSpace(sapsStation))
            query = query.Where(c => c.CrNo != null &&
                c.CrNo.ToLower().StartsWith(sapsStation.Trim().ToLower()));

        // ── Route
        if (!string.IsNullOrWhiteSpace(route))
            query = query.Where(c => c.RoadNumber != null &&
                c.RoadNumber.ToLower().Contains(route.Trim().ToLower()));

        // ── Crash Type
        if (!string.IsNullOrWhiteSpace(crashType))
            query = query.Where(c =>
                c.CrashConditions.Any(cc => cc.CrashType != null &&
                    cc.CrashType.ToLower().Contains(crashType.Trim().ToLower())));

        // ── Province
        if (!string.IsNullOrWhiteSpace(province))
            query = query.Where(c => c.ProvinceCode == province);

        // ── Severity (filter crashes that have at least one person with this severity)
        if (!string.IsNullOrWhiteSpace(severity))
            query = query.Where(c =>
                c.CrashPeople.Any(p => p.SeverityOfInjury == severity));

        // ── Date range
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
            _context.Add(crash);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = crash.CrashId });
        }
        return View(crash);
    }



   
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var crash = await _context.Crashes.FindAsync(id);
        if (crash == null) return NotFound();
        return View(crash);
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

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            rows = rows.Where(r =>
                r.CrNo.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.Route.Contains(term, StringComparison.OrdinalIgnoreCase)
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
                r.CrNo,
                r.Station,
                r.District,
                Date = r.Date.ToString("yyyy-MM-dd"),
                r.Route,
                r.CrashType,
                r.VehicleCount,
                r.Fatalities,
                r.Serious,
                Severity = r.OverallSeverity,
                r.Source
            });

        return Json(new { total, page, pageSize, rows = paged });
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