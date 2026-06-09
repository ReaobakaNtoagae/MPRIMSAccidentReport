using CrashReport.Data;
using CrashReport.Models;


using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Controllers;


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
