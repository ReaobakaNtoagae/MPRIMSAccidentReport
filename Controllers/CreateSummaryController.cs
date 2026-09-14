using System.Text.Json;
using CrashReport.Data;
using CrashReport.Models;
using CrashReport.Security;
using CrashReport.Services;
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
        [Authorize(Policy = Privileges.Crashes.CreateSummary)]
        public IActionResult CreateSummary()
        {
            var model = new CrashSummary
            {
                CrashDate = DateOnly.FromDateTime(DateTime.Today)
            };
            return View("QuickCapture", model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Privileges.Crashes.CreateSummary)]
        public async Task<IActionResult> CreateSummary(
                    CrashSummary model, string? vehiclesJson, string? injuriesJson)
        {
            if (string.IsNullOrWhiteSpace(model.Station))
                return Json(new { success = false, message = "Station is required." });

            if (string.IsNullOrWhiteSpace(model.CrNo))
                return Json(new { success = false, message = "CR number is required." });

            try
            {
                // Users enter only the AR sequence. The stored value includes the crash month/year.
                model.CrNo = QuickCaptureIdentifier.FormatAr(model.CrNo, model.CrashDate);
                model.CasNo = QuickCaptureIdentifier.FormatCas(model.CasNo, model.CrashDate);
            }
            catch (ArgumentException ex) { return Json(new { success = false, message = ex.Message }); }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, message = string.Join(" ", errors) });
            }

            // ── Vehicles (instance-based: one row per real vehicle) ───────
            var vehicles = new List<VehicleEntryInput>();
            if (!string.IsNullOrWhiteSpace(vehiclesJson))
            {
                try
                {
                    vehicles = JsonSerializer.Deserialize<List<VehicleEntryInput>>(vehiclesJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }
                catch
                {
                    return Json(new { success = false, message = "Vehicle details could not be read. Please try again." });
                }
            }

            foreach (var v in vehicles)
            {
                if (string.IsNullOrWhiteSpace(v.VehicleTypeCode))
                    return Json(new { success = false, message = $"Vehicle {v.VehicleNumber}: vehicle type is required." });
            }

            var vehicleNumbers = vehicles.Select(v => v.VehicleNumber).ToList();
            if (vehicleNumbers.Distinct().Count() != vehicleNumbers.Count)
                return Json(new { success = false, message = "Each vehicle must have a unique vehicle number." });

            // ── Injuries (all severities, per-victim) ──────────────────────
            var injuries = new List<InjuryEntryInput>();
            if (!string.IsNullOrWhiteSpace(injuriesJson))
            {
                try
                {
                    injuries = JsonSerializer.Deserialize<List<InjuryEntryInput>>(injuriesJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }
                catch
                {
                    return Json(new { success = false, message = "Casualty details could not be read. Please try again." });
                }
            }

            var validSeverities = new[] { "Fatal", "Serious", "Slight" };
            // Bound person counts before casting role/demographic counts to byte columns.
            if (injuries.Count > 255 || vehicles.Count > 255)
                return Json(new { success = false, message = "A quick record supports up to 255 people and vehicles." });
            var validRoles = new[] { "Driver", "Passenger", "Pedestrian", "Cyclist" };

            foreach (var inj in injuries)
            {
                if (inj.AgeGroupCode != null && !new[] { "0-7", "8-12", "13-18", "19-35", "36+" }.Contains(inj.AgeGroupCode))
                    return Json(new { success = false, message = "Select a valid age group." });
                if (!validSeverities.Contains(inj.Severity))
                    return Json(new { success = false, message = $"Each casualty needs a valid severity (Fatal, Serious, or Slight) -- got '{inj.Severity}'." });

                // Fatality details may be unknown until the post-mortem. Serious and
                // slight injuries still require the road-user classification requested.
                if (inj.Severity != "Fatal" && !validRoles.Contains(inj.Role))
                    return Json(new { success = false, message = $"Each casualty needs a valid role (Driver, Passenger, Pedestrian, or Cyclist) -- got '{inj.Role}'." });
                if (inj.Role is not null && !validRoles.Contains(inj.Role))
                    return Json(new { success = false, message = "Select a valid road user when fatality details are supplied." });

                // Demographics are optional -- a mass-casualty day still
                // works even if nobody has time to fill these in. Only
                // validate the ones that WERE provided.
                if (inj.Age.HasValue && (inj.Age.Value < 0 || inj.Age.Value > 120))
                    return Json(new { success = false, message = $"Age {inj.Age} is out of range (0-120)." });

                if (!string.IsNullOrEmpty(inj.Gender) && inj.Gender != "M" && inj.Gender != "F")
                    return Json(new { success = false, message = "Gender must be M or F if provided." });

                if (!string.IsNullOrEmpty(inj.Race) && !new[] { "B", "C", "I", "W", "O" }.Contains(inj.Race))
                    return Json(new { success = false, message = "Race must be B, C, I, W, or O if provided." });

                // Driver/Passenger must reference a real submitted vehicle;
                // Pedestrian/Cyclist must NOT (no vehicle to link to).
                if ((inj.Role == "Driver" || inj.Role == "Passenger"))
                {
                    // Quick Capture may not know which vehicle a fatality occupied.
                    // A supplied link must still point at a submitted vehicle.
                    if (inj.VehicleNumber.HasValue && !vehicleNumbers.Contains(inj.VehicleNumber.Value))
                        return Json(new { success = false, message = $"A {inj.Role.ToLower()} casualty must reference one of the vehicles entered above." });
                }
                else if (inj.VehicleNumber != null)
                {
                    return Json(new { success = false, message = $"A {inj.Role.ToLower()} casualty cannot be linked to a vehicle." });
                }
            }


            int Count(string severity, string role) =>
                injuries.Count(i => i.Severity == severity && i.Role == role);

            // The role-based Quick Capture sends one road-user row per count. Require
            // exact agreement, while keeping compatibility with previous callers.
            var seriousRows = injuries.Count(i => i.Severity == "Serious");
            var slightRows = injuries.Count(i => i.Severity == "Slight");
            var roleBasedEditor = Request.Form.ContainsKey("RoleBasedInjuryEditor");
            if ((roleBasedEditor && (model.SeriousInjuriesTotal != seriousRows || model.SlightInjuriesTotal != slightRows)) ||
                (!roleBasedEditor && ((seriousRows > 0 && model.SeriousInjuriesTotal.HasValue && model.SeriousInjuriesTotal != seriousRows) ||
                 (slightRows > 0 && model.SlightInjuriesTotal.HasValue && model.SlightInjuriesTotal != slightRows))))
                return Json(new { success = false, message = "Injury totals must match the supplied casualty details." });

            model.FatalDrivers = (byte)Count("Fatal", "Driver");
            model.FatalPassengers = (byte)Count("Fatal", "Passenger");
            model.FatalPedestrians = (byte)Count("Fatal", "Pedestrian");
            model.FatalCyclists = (byte)Count("Fatal", "Cyclist");

            model.SeriousDrivers = (byte)Count("Serious", "Driver");
            model.SeriousPassengers = (byte)Count("Serious", "Passenger");
            model.SeriousPedestrians = (byte)Count("Serious", "Pedestrian");
            model.SeriousCyclists = (byte)Count("Serious", "Cyclist");

            model.SlightDrivers = (byte)Count("Slight", "Driver");
            model.SlightPassengers = (byte)Count("Slight", "Passenger");
            model.SlightPedestrians = (byte)Count("Slight", "Pedestrian");
            model.SlightCyclists = (byte)Count("Slight", "Cyclist");

            // ── Fatal-only demographic rollups (age bucket / gender / race)
            // stay on crash_summaries for fast reporting -- Serious/Slight
            // demographics live only in crash_summary_injuries, matching
            // how the source data itself never had aggregate demographic
            // breakdowns for non-fatal severities. ─────────────────────────
            foreach (var inj in injuries.Where(i => i.Severity == "Fatal"))
            {
                if (!inj.Age.HasValue)
                {
                    switch (inj.AgeGroupCode)
                    {
                        case "0-7": model.FatalAge0to7++; break;
                        case "8-12": model.FatalAge8to12++; break;
                        case "13-18": model.FatalAge13to18++; break;
                        case "19-35": model.FatalAge19to35++; break;
                        case "36+": model.FatalAge36Plus++; break;
                    }
                }
                if (inj.Age.HasValue)
                {
                    var age = inj.Age.Value;
                    if (age <= 7) model.FatalAge0to7++;
                    else if (age <= 12) model.FatalAge8to12++;
                    else if (age <= 18) model.FatalAge13to18++;
                    else if (age <= 35) model.FatalAge19to35++;
                    else model.FatalAge36Plus++;
                }

                if (inj.Gender == "M") model.FatalMale++;
                else if (inj.Gender == "F") model.FatalFemale++;

                switch (inj.Race)
                {
                    case "B": model.FatalAfrican++; break;
                    case "C": model.FatalColoured++; break;
                    case "I": model.FatalIndian++; break;
                    case "W": model.FatalWhite++; break;
                    case "O": model.FatalOtherRace++; break;
                }
            }

            // AR and CAS sequences are scoped to the SAPS station.
            if (await _context.CrashSummaries.AnyAsync(s => s.Station == model.Station && s.CrNo == model.CrNo))
                return Json(new { success = false, message = $"AR number '{model.CrNo}' already exists for {model.Station}." });
            if (model.CasNo is not null && await _context.CrashSummaries.AnyAsync(s => s.Station == model.Station && s.CasNo == model.CasNo))
                return Json(new { success = false, message = $"CAS number '{model.CasNo}' already exists for {model.Station}." });

            var existsAsFullReport = await _context.Crashes.AnyAsync(c => c.CrNo == model.CrNo);
            // Quick Capture is the lighter record, so do not add it after an
            // authoritative full report already exists.
            if (existsAsFullReport)
                return Json(new { success = false, message = $"A record with AR/CR number '{model.CrNo}' already exists as a full report." });

            model.SourceFile = "Quick add (manual entry)";
            model.ImportedAt = DateTime.UtcNow;
            model.VehicleCount = (byte)vehicles.Count;
            model.FatalitiesTotal = injuries.Count(i => i.Severity == "Fatal");

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _context.CrashSummaries.Add(model);
                await _context.SaveChangesAsync();


                var vehicleNumberToId = new Dictionary<byte, int>();
                foreach (var v in vehicles)
                {
                    var entity = new CrashSummaryVehicle
                    {
                        SummaryId = model.SummaryId,
                        VehicleNumber = v.VehicleNumber,
                        VehicleTypeCode = v.VehicleTypeCode,
                        VehicleTypeName = v.VehicleTypeName,
                        Registration = string.IsNullOrWhiteSpace(v.Registration) ? null : v.Registration
                    };
                    _context.CrashSummaryVehicles.Add(entity);
                    await _context.SaveChangesAsync();
                    vehicleNumberToId[v.VehicleNumber] = entity.VehicleId;
                }

                foreach (var inj in injuries)
                {
                    _context.CrashSummaryInjuries.Add(new CrashSummaryInjury
                    {
                        SummaryId = model.SummaryId,
                        VehicleId = inj.VehicleNumber.HasValue ? vehicleNumberToId[inj.VehicleNumber.Value] : null,
                        Severity = inj.Severity,
                        Role = inj.Role,
                        Age = (byte?)inj.Age,
                        AgeGroupCode = inj.AgeGroupCode,
                        Gender = string.IsNullOrEmpty(inj.Gender) ? null : inj.Gender,
                        Race = string.IsNullOrEmpty(inj.Race) ? null : inj.Race
                    });
                }
                if (injuries.Count > 0)
                    await _context.SaveChangesAsync();

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new
                {
                    success = false,
                    message = $"Something went wrong while saving: {ex.Message} | Inner: {ex.InnerException?.Message}"
                });
            }

            var message = $"Crash record '{model.CrNo}' added successfully.";
            if (existsAsFullReport)
                message += $" Note: CR number '{model.CrNo}' also exists as a full CR1 report — both are saved; " +
                           "reports won't double-count them, but you may want to reconcile the two records.";

            return Json(new { success = true, message, crNo = model.CrNo, summaryId = model.SummaryId, duplicatePair = existsAsFullReport });
        }
    }

    public class VehicleEntryInput
    {
        public byte VehicleNumber { get; set; }
        public string VehicleTypeCode { get; set; } = "";
        public string VehicleTypeName { get; set; } = "";
        public string? Registration { get; set; }
    }

    public class InjuryEntryInput
    {
        public string Severity { get; set; } = "";
        public string? Role { get; set; }
        public byte? VehicleNumber { get; set; } // null for Pedestrian/Cyclist
        public int? Age { get; set; }
        public string? AgeGroupCode { get; set; }
        public string? Gender { get; set; }
        public string? Race { get; set; }
    }
}
