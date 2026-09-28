using CrashReport.Data;
using CrashReport.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Controllers.Api;


[Route("api/lookup-management")]
[ApiController]
[Authorize(Policy = Privileges.Admin.Lookups)]
public class LookupsApiController : ControllerBase
{
    private readonly AppDbContext _context;

    public LookupsApiController(AppDbContext context) => _context = context;

    // GET api/lookup-management/counts
    [HttpGet("counts")]
    [Authorize(Policy = Privileges.Admin.Lookups)]
    public async Task<IActionResult> GetCounts()
    {
        return Ok(new
        {
            stationsCount = await _context.SapsStations.CountAsync(),
            locationsCount = await _context.LookupLocations.CountAsync(),
            routesCount = await _context.LookupRoutes.CountAsync(),
            crashTypesCount = await _context.LookupCrashTypes.CountAsync(),
            vehicleTypesCount = await _context.LookupVehicleTypes.CountAsync()
        });
    }
}
