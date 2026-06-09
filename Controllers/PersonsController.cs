using CrashReport.Data;
using CrashReport.Models;


using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Controllers;

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
