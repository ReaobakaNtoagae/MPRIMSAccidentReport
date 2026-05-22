using CrashReport.Data;
using CrashReport.Models;


using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Controllers;

// =============================================================
// PERSONS
// =============================================================
public class PersonsController : Controller
{
    private readonly AppDbContext _context;
    public PersonsController(AppDbContext context) => _context = context;

    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var data = await _context.Persons
            .Select(p => new
            {
                p.PersonId,
                p.Surname,
                p.FullNames,
                p.IdNumber,
                p.IdType,
                p.Gender,
                p.CellPhone,
                p.PopulationGroup,
                CrashCount = p.CrashPeople.Count
            })
            .OrderBy(p => p.Surname)
            .ToListAsync();
        return Json(data);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        var person = await _context.Persons
            .Include(p => p.DriversLicences)
            .Include(p => p.CrashPeople)
                .ThenInclude(cp => cp.Crash)
            .FirstOrDefaultAsync(p => p.PersonId == id);
        if (person == null) return NotFound();
        return View(person);
    }

    public IActionResult Create() => View(new Person());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("IdType,IdNumber,Age,Surname,FullNames,CountryOfOrigin,Nationality," +
              "PopulationGroup,Gender,HomeAddress,CellPhone,OtherPhone,WorkContactAddress")] Person person)
    {
        if (ModelState.IsValid)
        {
            _context.Add(person);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(person);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var person = await _context.Persons.FindAsync(id);
        if (person == null) return NotFound();
        return View(person);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id,
        [Bind("PersonId,IdType,IdNumber,Age,Surname,FullNames,CountryOfOrigin,Nationality," +
              "PopulationGroup,Gender,HomeAddress,CellPhone,OtherPhone,WorkContactAddress")] Person person)
    {
        if (id != person.PersonId) return NotFound();
        if (ModelState.IsValid)
        {
            try { _context.Update(person); await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Persons.Any(p => p.PersonId == id)) return NotFound();
                throw;
            }
            return RedirectToAction(nameof(Index));
        }
        return View(person);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var person = await _context.Persons.FirstOrDefaultAsync(p => p.PersonId == id);
        if (person == null) return NotFound();
        return View(person);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var person = await _context.Persons.FindAsync(id);
        if (person != null) _context.Persons.Remove(person);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}


// =============================================================
// VEHICLES
// =============================================================
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


// =============================================================
// WITNESSES
// =============================================================
public class WitnessesController : Controller
{
    private readonly AppDbContext _context;
    public WitnessesController(AppDbContext context) => _context = context;

    public IActionResult Create(int crashId)
    {
        ViewBag.CrashId = crashId;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("CrashId,SurnameInitials,IdType,IdNumber,WorkContactAddress,CellPhone,OtherPhone")] Witness witness)
    {
        if (ModelState.IsValid)
        {
            _context.Add(witness);
            await _context.SaveChangesAsync();
            return RedirectToAction("Details", "Crashes", new { id = witness.CrashId });
        }
        ViewBag.CrashId = witness.CrashId;
        return View(witness);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var witness = await _context.Witnesses.FindAsync(id);
        int crashId = witness?.CrashId ?? 0;
        if (witness != null) _context.Witnesses.Remove(witness);
        await _context.SaveChangesAsync();
        return RedirectToAction("Details", "Crashes", new { id = crashId });
    }
}


// =============================================================
// CONTRIBUTORY FACTORS
// =============================================================
public class ContributoryFactorsController : Controller
{
    private readonly AppDbContext _context;
    public ContributoryFactorsController(AppDbContext context) => _context = context;

    public IActionResult Create(int crashId)
    {
        ViewBag.CrashId = crashId;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("CrashId,FactorCategory,FactorDescription,IsMajorFactor")] ContributoryFactor factor)
    {
        if (ModelState.IsValid)
        {
            // Enforce only one major factor per crash
            if (factor.IsMajorFactor)
            {
                var existing = await _context.ContributoryFactors
                    .Where(f => f.CrashId == factor.CrashId && f.IsMajorFactor)
                    .ToListAsync();
                existing.ForEach(f => f.IsMajorFactor = false);
            }
            _context.Add(factor);
            await _context.SaveChangesAsync();
            return RedirectToAction("Details", "Crashes", new { id = factor.CrashId });
        }
        ViewBag.CrashId = factor.CrashId;
        return View(factor);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var factor = await _context.ContributoryFactors.FindAsync(id);
        int crashId = factor?.CrashId ?? 0;
        if (factor != null) _context.ContributoryFactors.Remove(factor);
        await _context.SaveChangesAsync();
        return RedirectToAction("Details", "Crashes", new { id = crashId });
    }
}