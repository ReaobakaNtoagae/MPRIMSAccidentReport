using System.Text.Json;
using CrashReport.Data;
using CrashReport.Models;
using CrashReport.Security;
using Microsoft.AspNetCore.Authorization;
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
        [Authorize(Policy = Privileges.Crashes.CreateSummary)]
        public async Task<IActionResult> CreateSummary(CrashSummary model, string? fatalitiesJson)
        {
            // BUG FIXED: the original checked "is model.CrNo blank?" and failed
            // every time, because the client never sends a CrNo — it's meant
            // to be generated below, after Station is known. That check is
            // removed entirely; CrNo no longer comes from the client at all.

            if (string.IsNullOrWhiteSpace(model.Station))
                return Json(new { success = false, message = "Station is required." });

            // BUG FIXED: this used to "return View(model)" on invalid
            // ModelState, which sends HTML back to a caller expecting JSON
            // (res.success would be undefined on the client). Every response
            // from this action must be Json().
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, message = string.Join(" ", errors) });
            }

            // ── Fatality details (age/gender/race per victim) ────────────
            var fatalities = new List<FatalityEntryInput>();
            if (!string.IsNullOrWhiteSpace(fatalitiesJson))
            {
                try
                {
                    fatalities = JsonSerializer.Deserialize<List<FatalityEntryInput>>(fatalitiesJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }
                catch
                {
                    return Json(new { success = false, message = "Fatality details could not be read. Please try again." });
                }
            }

            var roleTotal = model.FatalDrivers + model.FatalPassengers + model.FatalPedestrians + model.FatalCyclists;

            if (fatalities.Count != roleTotal)
            {
                return Json(new
                {
                    success = false,
                    message = $"Fatality details entered ({fatalities.Count}) don't match the fatal injuries by role " +
                              $"({roleTotal}). Every fatality needs an age, gender, and race, and the count must match " +
                              $"the driver/passenger/pedestrian/cyclist fatal totals above."
                });
            }

            foreach (var f in fatalities)
            {
                if (f.Age < 0 || f.Age > 120)
                    return Json(new { success = false, message = $"Age {f.Age} is out of range (0–120)." });

                if (f.Gender != "M" && f.Gender != "F")
                    return Json(new { success = false, message = "Each fatality needs a gender (M or F)." });

                if (!new[] { "B", "C", "I", "W", "O" }.Contains(f.Race))
                    return Json(new { success = false, message = "Each fatality needs a race code (B, C, I, W, or O)." });
            }

            // ── Group ages into buckets, tally gender/race onto the rollup columns ──
            foreach (var f in fatalities)
            {
                if (f.Age <= 7) model.FatalAge0to7++;
                else if (f.Age <= 12) model.FatalAge8to12++;
                else if (f.Age <= 18) model.FatalAge13to18++;
                else if (f.Age <= 35) model.FatalAge19to35++;
                else model.FatalAge36Plus++;

                if (f.Gender == "M") model.FatalMale++;
                else model.FatalFemale++;

                switch (f.Race)
                {
                    case "B": model.FatalAfrican++; break;
                    case "C": model.FatalColoured++; break;
                    case "I": model.FatalIndian++; break;
                    case "W": model.FatalWhite++; break;
                    case "O": model.FatalOtherRace++; break;
                }
            }

            model.CrNo = await GenerateNextCrNo(model.Station);

            // Duplicate check now runs AFTER generation, against a real
            // generated value — the original ran it against a client-supplied
            // CrNo that was always null, so it could never actually catch anything.
            if (await _context.CrashSummaries.AnyAsync(s => s.CrNo == model.CrNo))
                return Json(new { success = false, message = $"A record with CR number '{model.CrNo}' already exists. Please try again." });

            model.SourceFile = "Quick add (manual entry)";
            model.ImportedAt = DateTime.UtcNow;

            _context.CrashSummaries.Add(model);
            await _context.SaveChangesAsync(); // model.SummaryId is populated after this

            // Persist the real per-victim rows, not just the rollup counts.
            foreach (var f in fatalities)
            {
                _context.CrashFatalities.Add(new CrashFatality
                {
                    SummaryId = model.SummaryId,
                    Age = (byte)f.Age,
                    Gender = f.Gender,
                    Race = f.Race
                });
            }
            if (fatalities.Count > 0)
                await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Crash record '{model.CrNo}' added successfully.", crNo = model.CrNo, summaryId = model.SummaryId });
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

            // BUG FIXED: was $"{prefix} - {(maxSeq + 1): D2}" — the space
            // before "D2" makes an invalid format string (throws
            // FormatException at runtime), and the spaced-out dash would
            // have produced "TONGA - 01" instead of "TONGA-01", breaking
            // every place that parses CR numbers by splitting on '-'.
            return $"{prefix}-{(maxSeq + 1):D2}";
        }
    }
}

public class FatalityEntryInput
{
    public int Age { get; set; }
    public string Gender { get; set; } = ""; // "M" or "F"
    public string Race { get; set; } = "";   // "B" African, "C" Coloured, "I" Indian, "W" White, "O" Other
}
