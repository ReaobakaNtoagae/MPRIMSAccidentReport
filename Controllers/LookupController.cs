using CrashReport.Data;
using CrashReport.Models;
using CrashReport.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static CrashReport.Models.FixedEnum;


namespace CrashReport.Controllers;

[Route("api/lookup")]
[ApiController]
public class LookupController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILookupAdminService _lookupAdmin;
    public LookupController(AppDbContext context, ILookupAdminService lookupAdmin)
    {
        _context = context;
        _lookupAdmin = lookupAdmin;
    }


    [HttpGet("districts")]
    public async Task<IActionResult> Districts()
    {
        var names = await _context.LookupDistricts
            .Where(d => d.IsActive)
            .OrderBy(d => d.DistrictName)
            .Select(d => d.DistrictName)
            .ToListAsync();

        return Ok(names);
    }

    [HttpGet("costcentres")]
    public async Task<IActionResult> CostCentres()
    {
        // Keep this lookup independent from District and SAPS Station for now.
        var items = await _context.LookupCostCentres
            .AsNoTracking()
            .OrderBy(costCentre => costCentre.CostCentreName)
            .Select(costCentre => new
            {
                id = costCentre.CostCentreId,
                text = costCentre.CostCentreName,
                stationId = costCentre.StationId,
                districtId = costCentre.DistrictId
            })
            .ToListAsync();

        return Ok(items);
    }


    [HttpGet("stations")]
    public async Task<IActionResult> SearchStations(string? q = null, string? district = null)
    {
        var query = _context.SapsStations.Where(s => s.IsActive);
        // Quick Capture can narrow stations without changing the authoritative lookup.
        if (!string.IsNullOrWhiteSpace(district))
            query = query.Where(s => s.DistrictLookup != null
                ? s.DistrictLookup.DistrictName == district : s.District == district);
        if (!string.IsNullOrEmpty(q))
            query = query.Where(s => s.StationName.Contains(q));

        var items = await query
            .OrderBy(s => s.StationName)
            .Select(s => new {
                id = s.StationId,
                text = s.StationName,
                province = s.ProvinceCode,
                district = s.District
            })
            .ToListAsync();
        return Ok(items);
    }

    [HttpPost("stations")]
    public async Task<IActionResult> AddStation([FromBody] AddLookupRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Text))
            return BadRequest("Station name is required.");

        var name = req.Text.Trim().ToUpper();
        if (await _context.SapsStations.AnyAsync(s => s.StationName == name))
            return Conflict(new { message = $"'{name}' already exists." });

        var station = new SapsStation
        {
            StationName = name,
            ProvinceCode = req.Province,
            District = req.Extra
        };
        _context.SapsStations.Add(station);
        await _context.SaveChangesAsync();
        return Ok(new { id = station.StationId, text = station.StationName });
    }



    [HttpGet("locations")]
    public async Task<IActionResult> SearchLocations(string? q = null)
    {
        var query = _context.LookupLocations.Where(l => l.IsActive);
        if (!string.IsNullOrEmpty(q))
            query = query.Where(l => l.LocationName.Contains(q));

        var items = await query
            .OrderBy(l => l.LocationName)
            .Select(l => new {
                id = l.LocationId,
                text = l.LocationName,
                province = l.ProvinceCode
            })
            .ToListAsync();
        return Ok(items);
    }

    [HttpPost("locations")]
    public async Task<IActionResult> AddLocation([FromBody] AddLookupRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Text))
            return BadRequest("Location name is required.");

        var name = req.Text.Trim().ToUpper();
        if (await _context.LookupLocations.AnyAsync(l => l.LocationName == name))
            return Conflict(new { message = $"'{name}' already exists." });

        var loc = new LookupLocation { LocationName = name, ProvinceCode = req.Province };
        _context.LookupLocations.Add(loc);
        await _context.SaveChangesAsync();
        return Ok(new { id = loc.LocationId, text = loc.LocationName });
    }



    [HttpGet("routes")]
    public async Task<IActionResult> SearchRoutes(string? q = null)
    {
        var query = _context.LookupRoutes.Where(r => r.IsActive);
        if (!string.IsNullOrEmpty(q))
            query = query.Where(r => r.RouteCode.Contains(q) ||
                                     (r.Description != null && r.Description.Contains(q)));

        var items = await query
            .OrderBy(r => r.RouteCode)
            .Select(r => new {
                id = r.RouteId,
                text = r.RouteCode,
                description = r.Description
            })
            .ToListAsync();
        return Ok(items);
    }

    [HttpPost("routes")]
    public async Task<IActionResult> AddRoute([FromBody] AddLookupRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Text))
            return BadRequest("Route code is required.");

        var code = req.Text.Trim().ToUpper();
        if (await _context.LookupRoutes.AnyAsync(r => r.RouteCode == code))
            return Conflict(new { message = $"'{code}' already exists." });

        var route = new LookupRoute { RouteCode = code, ProvinceCode = req.Province };
        _context.LookupRoutes.Add(route);
        await _context.SaveChangesAsync();
        return Ok(new { id = route.RouteId, text = route.RouteCode });
    }


    [HttpGet("crashtypes")]
    public async Task<IActionResult> SearchCrashTypes(string? q = null)
    {
        var query = _context.LookupCrashTypes.Where(c => c.IsActive);
        if (!string.IsNullOrEmpty(q))
            query = query.Where(c => c.CrashTypeCode.Contains(q));

        var items = await query
            .OrderBy(c => c.CrashTypeCode)
            .Select(c => new {
                id = c.CrashTypeId,
                text = c.CrashTypeCode
            })
            .ToListAsync();
        return Ok(items);
    }

    [HttpPost("crashtypes")]
    public async Task<IActionResult> AddCrashType([FromBody] AddLookupRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Text))
            return BadRequest("Crash type is required.");

        var code = req.Text.Trim().ToUpper();
        if (await _context.LookupCrashTypes.AnyAsync(c => c.CrashTypeCode == code))
            return Conflict(new { message = $"'{code}' already exists." });

        var ct = new LookupCrashType
        {
            CrashTypeCode = code,
            Description = req.Extra
        };
        _context.LookupCrashTypes.Add(ct);
        await _context.SaveChangesAsync();
        return Ok(new { id = ct.CrashTypeId, text = ct.CrashTypeCode });
    }



    [HttpGet("vehicletypes")]
    public async Task<IActionResult> SearchVehicleTypes(string? q = null)
    {
        var query = _context.LookupVehicleTypes.Where(v => v.IsActive);
        if (!string.IsNullOrEmpty(q))
            query = query.Where(v => v.VehicleTypeCode.Contains(q) ||
                                     (v.Description != null && v.Description.Contains(q)) ||
                                     (v.FullName != null && v.FullName.Contains(q)));

        var items = await query
            .OrderBy(v => v.VehicleTypeCode)
            .Select(v => new {
                id = v.VehicleTypeId,
                value = v.VehicleTypeCode,
                text = v.FullName ?? v.VehicleTypeCode, 
                                                        
                description = v.Description
            })
            .ToListAsync();
        return Ok(items);
    }

    [HttpPost("vehicletypes")]
    public async Task<IActionResult> AddVehicleType([FromBody] AddLookupRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Text))
            return BadRequest("Vehicle type code is required.");

        var code = req.Text.Trim().ToUpper();
        if (await _context.LookupVehicleTypes.AnyAsync(v => v.VehicleTypeCode == code))
            return Conflict(new { message = $"'{code}' already exists." });

        var vt = new LookupVehicleType
        {
            VehicleTypeCode = code,
            Description = req.Extra
        };
        _context.LookupVehicleTypes.Add(vt);
        await _context.SaveChangesAsync();
        return Ok(new { id = vt.VehicleTypeId, text = vt.VehicleTypeCode });
    }


    [HttpDelete("{table}/{id}")]
    public async Task<IActionResult> Deactivate(string table, int id)
    {
        var outcome = await _lookupAdmin.DeactivateAsync(table, id);
        return outcome switch
        {
            LookupDeactivateOutcome.Deactivated => Ok(),
            LookupDeactivateOutcome.NotFound => NotFound(),
            _ => BadRequest("Unknown table.")
        };
    }

    [HttpGet("formoptions")]
    public async Task<IActionResult> FormOptions()
    {
        var items = await _context.OptionListItems
            .Where(o => o.IsActive)
            .OrderBy(o => o.ListName)
            .ThenBy(o => o.DisplayOrder)
            .ToListAsync();

        var grouped = items
            .GroupBy(o => o.ListName)
            .ToDictionary(g => g.Key, g => g.Select(o => o.OptionValue).ToList());

        return Ok(grouped);

    }

    [HttpGet("fixedvalues")]
    public IActionResult FixedValues()
    {
        return Ok(new
        {
            InjurySeverity = Enum.GetValues<InjurySeverity>().Select(v => v.Display()).ToList(),
            PersonRole = Enum.GetValues<PersonRole>().Select(v => v.Display()).ToList()
        });
    }
}


public class AddLookupRequest
{
    public string Text { get; set; } = string.Empty;
    public string? Province { get; set; }
    public string? Extra { get; set; }
}
