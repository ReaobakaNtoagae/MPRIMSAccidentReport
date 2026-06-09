using CrashReport.Data;
using CrashReport.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Controllers;

[Route("api/lookup")]
[ApiController]
public class LookupController : ControllerBase
{
    private readonly AppDbContext _context;
    public LookupController(AppDbContext context) => _context = context;

    

    [HttpGet("stations")]
    public async Task<IActionResult> SearchStations(string? q = null)
    {
        var query = _context.SapsStations.Where(s => s.IsActive);
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
            query = query.Where(c => c.CrashTypeCode.Contains(q) ||
                                     (c.Description != null && c.Description.Contains(q)));

        var items = await query
            .OrderBy(c => c.CrashTypeCode)
            .Select(c => new {
                id = c.CrashTypeId,
                text = c.CrashTypeCode,
                description = c.Description
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
                                     (v.Description != null && v.Description.Contains(q)));

        var items = await query
            .OrderBy(v => v.VehicleTypeCode)
            .Select(v => new {
                id = v.VehicleTypeId,
                text = v.VehicleTypeCode,
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
        switch (table.ToLower())
        {
            case "stations":
                var s = await _context.SapsStations.FindAsync(id);
                if (s == null) return NotFound();
                s.IsActive = false;
                break;
            case "locations":
                var l = await _context.LookupLocations.FindAsync(id);
                if (l == null) return NotFound();
                l.IsActive = false;
                break;
            case "routes":
                var r = await _context.LookupRoutes.FindAsync(id);
                if (r == null) return NotFound();
                r.IsActive = false;
                break;
            case "crashtypes":
                var ct = await _context.LookupCrashTypes.FindAsync(id);
                if (ct == null) return NotFound();
                ct.IsActive = false;
                break;
            case "vehicletypes":
                var vt = await _context.LookupVehicleTypes.FindAsync(id);
                if (vt == null) return NotFound();
                vt.IsActive = false;
                break;
            default:
                return BadRequest("Unknown table.");
        }
        await _context.SaveChangesAsync();
        return Ok();
    }
}


public class AddLookupRequest
{
    public string Text { get; set; } = string.Empty;
    public string? Province { get; set; }
    public string? Extra { get; set; } 
}