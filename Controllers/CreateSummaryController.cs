using CrashReport.Data;
using CrashReport.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Controllers
{
    public class CreateSummaryController : Controller
    {
        private readonly AppDbContext _context;

        public CreateSummaryController(AppDbContext context)
        {
            _context = context;
        }
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult CreateSummary()
        {
            var model = new CrashSummary
            {
                CrashDate = DateOnly.FromDateTime(DateTime.Today)
            };
            return View(model);

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSummary(CrashSummary model)
        {
            if(string.IsNullOrWhiteSpace(model.CrNo))
            {
                return Json(new { success = false, message = "CR number is required." });
            }
            else if(await _context.CrashSummaries.AnyAsync(s => s.CrNo == model.CrNo))
            {
                return Json(new { success = false, message = $"A record with CR number '{model.CrNo}' already exists." });
            }

            if (string.IsNullOrWhiteSpace(model.Station))
                return Json(new { success = false, message = $"Station is required." });

            if (!ModelState.IsValid) return View(model);

            model.CrNo = await GenerateNextCrNo(model.Station);

            model.SourceFile = "Quick add (manual entry)";
            model.ImportedAt = DateTime.UtcNow;

            _context.CrashSummaries.Add(model);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Crash record '{model.CrNo}' added successfully.", summaryId = model.SummaryId });
        }

        private async Task<string> GenerateNextCrNo(string station)
        {
            var prefix = station.Trim().ToUpperInvariant();

            var fromSummaries = await _context.CrashSummaries
                .Where(s => s.CrNo != null && s.CrNo.ToUpper().StartsWith(prefix + "-"))
                .Select(s => s.CrNo!)
                .ToListAsync();

            var fromCrashes = await _context.Crashes
                .Where(s => s.CrNo != null && s.CrNo.ToUpper().StartsWith(prefix + "-"))
                .Select(s => s.CrNo!)
                .ToListAsync();

            var maxSeq = fromSummaries.Concat(fromCrashes)
                .Select(cr =>
                {
                    var idx = cr.LastIndexOf('-');
                    return idx >= 0 && int.TryParse(cr[(idx + 1)..], out var n) ? n : 0;

                })
                .DefaultIfEmpty(0)
                .Max();

            return $"{prefix} - {(maxSeq + 1): D2}";


        }
    }
}
