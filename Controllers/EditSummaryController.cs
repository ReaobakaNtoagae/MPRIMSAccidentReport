using CrashReport.Data;
using CrashReport.Models;
using Microsoft.AspNetCore.Mvc;

namespace CrashReport.Controllers
{
    public class EditSummaryController : Controller
    {
        private readonly AppDbContext _context;

        public EditSummaryController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSummary(int id, CrashSummary model)
        {
            if (id != model.SummaryId) return NotFound();

            var summary = await _context.CrashSummaries.FindAsync(id);
            if (summary == null) return NotFound();

            if (!ModelState.IsValid) return View(model);

            summary.CrNo = model.CrNo;
            summary.CasNo = model.CasNo;
            summary.ArNo = model.ArNo;
            summary.Station = model.Station;
            summary.Route = model.Route;
            summary.Location = model.Location;
            summary.CrashDate = model.CrashDate;
            summary.CrashTime = model.CrashTime;
            summary.CrashType = model.CrashType;
            summary.VehicleCount = model.VehicleCount;
            summary.VehiclesString = model.VehiclesString;
            summary.FatalDrivers = model.FatalDrivers;
            summary.FatalPassengers = model.FatalPassengers;
            summary.FatalPedestrians = model.FatalPedestrians;
            summary.FatalCyclists = model.FatalCyclists;
            summary.FatalMale = model.FatalMale;
            summary.FatalFemale = model.FatalFemale;
            summary.SeriousDrivers = model.SeriousDrivers;
            summary.SeriousPassengers = model.SeriousPassengers;
            summary.SeriousPedestrians = model.SeriousPedestrians;
            summary.SeriousCyclists = model.SeriousCyclists;
            summary.SlightDrivers = model.SlightDrivers;
            summary.SlightPassengers = model.SlightPassengers;
            summary.SlightPedestrians = model.SlightPedestrians;
            summary.SlightCyclists = model.SlightCyclists;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Imported record '{summary.CrNo}' updated successfully.";
            return RedirectToAction("Index");

        }
    }
}
