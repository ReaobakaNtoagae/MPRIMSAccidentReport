using CrashReport.Data;
using CrashReport.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Controllers;

public class CrashesController : Controller
{
    private readonly AppDbContext _context;

    public CrashesController(AppDbContext context)
    {
        _context = context;
    }

    // GET: /Crashes
    public IActionResult Index() => View();

    // GET: /Crashes/Search — filtered JSON for the DataGrid
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



    // GET: /Crashes/GetAll — used by dashboard (unfiltered)
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var data = await _context.Crashes
            .Select(c => new
            {
                c.CrashId,
                c.CrNo,
                c.CasNo,
                c.CrashDate,
                c.CrashTime,
                c.ProvinceCode,
                c.SpeedLimitKmh,
                Location = c.CrashLocations
                                 .Select(l => l.CityTown ?? l.StreetRoadName)
                                 .FirstOrDefault(),
                VehicleCount = c.CrashVehicles.Count,
                PersonCount = c.CrashPeople.Count,
                FatalCount = c.CrashPeople.Count(cp => cp.SeverityOfInjury == "Fatal"),
                SeriousCount = c.CrashPeople.Count(cp => cp.SeverityOfInjury == "Serious")
            })
            .OrderByDescending(c => c.CrashDate)
            .ToListAsync();

        return Json(data);
    }



    // GET: /Crashes/FilterOptions — distinct values for dropdowns
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


    // GET: /Crashes/Details/5
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
        return View(crash);
    }

    // GET: /Crashes/Create
    public IActionResult Create() =>
        View(new Crash { CrashDate = DateOnly.FromDateTime(DateTime.Today) });


    // POST: /Crashes/Create
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



    // GET: /Crashes/Edit/5
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



    private bool CrashExists(int id) =>
        _context.Crashes.Any(c => c.CrashId == id);
}