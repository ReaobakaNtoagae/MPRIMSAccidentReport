using CrashReport.Data;
using CrashReport.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


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