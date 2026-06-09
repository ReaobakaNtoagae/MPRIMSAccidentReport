using CrashReport.Data;
using CrashReport.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class VehiclesController : Controller
{
    private readonly AppDbContext _context;
    public VehiclesController(AppDbContext context) => _context = context;

    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var data = await _context.Vehicles
            .Select(v => new
            {
                v.VehicleId,
                v.LicenceDiscNumber,
                v.Make,
                v.Model,
                v.Colour,
                v.VehicleCategory,
                v.SpecialFunction,
                v.PrivateOrBusiness,
                v.VinNumber,
                CrashCount = v.CrashVehicles.Count
            })
            .OrderBy(v => v.Make)
            .ToListAsync();
        return Json(data);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        var vehicle = await _context.Vehicles
            .Include(v => v.CrashVehicles)
                .ThenInclude(cv => cv.Crash)
            .FirstOrDefaultAsync(v => v.VehicleId == id);
        if (vehicle == null) return NotFound();
        return View(vehicle);
    }

    public IActionResult Create() => View(new Vehicle());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("CountryOfRegistration,LicenceDiscNumber,Colour,Make,Model,VinNumber," +
              "TrailerLicenceNumber,VehicleCategory,VehicleTypeCode,SpecialFunction," +
              "PrivateOrBusiness,LicenceTypeFitting")] Vehicle vehicle)
    {
        if (ModelState.IsValid)
        {
            _context.Add(vehicle);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(vehicle);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var vehicle = await _context.Vehicles.FindAsync(id);
        if (vehicle == null) return NotFound();
        return View(vehicle);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id,
        [Bind("VehicleId,CountryOfRegistration,LicenceDiscNumber,Colour,Make,Model,VinNumber," +
              "TrailerLicenceNumber,VehicleCategory,VehicleTypeCode,SpecialFunction," +
              "PrivateOrBusiness,LicenceTypeFitting")] Vehicle vehicle)
    {
        if (id != vehicle.VehicleId) return NotFound();
        if (ModelState.IsValid)
        {
            try { _context.Update(vehicle); await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Vehicles.Any(v => v.VehicleId == id)) return NotFound();
                throw;
            }
            return RedirectToAction(nameof(Index));
        }
        return View(vehicle);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.VehicleId == id);
        if (vehicle == null) return NotFound();
        return View(vehicle);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var vehicle = await _context.Vehicles.FindAsync(id);
        if (vehicle != null) _context.Vehicles.Remove(vehicle);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
