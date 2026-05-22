using CrashReport.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Controllers;

/// <summary>
/// MVC controller for the Lookups management page.
/// Separate from the API LookupController.
/// </summary>
public class LookupsController : Controller
{
    private readonly AppDbContext _context;
    public LookupsController(AppDbContext context) => _context = context;

    public async Task<IActionResult> Index()
    {
        ViewBag.StationsCount = await _context.SapsStations.CountAsync();
        ViewBag.LocationsCount = await _context.LookupLocations.CountAsync();
        ViewBag.RoutesCount = await _context.LookupRoutes.CountAsync();
        ViewBag.CrashTypesCount = await _context.LookupCrashTypes.CountAsync();
        ViewBag.VehicleTypesCount = await _context.LookupVehicleTypes.CountAsync();
        return View();
    }
}