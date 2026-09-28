using System.Text.Json;
using CrashReport.Data;
using CrashReport.Models;
using CrashReport.Models.Dtos;
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
        private readonly ICrashSummaryValidationService _validation;

        public CreateSummaryController(AppDbContext context, ICrashSummaryValidationService validation)
        {
            _context = context;
            _validation = validation;
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
            if (!model.CostCentreId.HasValue ||
                !await _context.LookupCostCentres.AnyAsync(c => c.CostCentreId == model.CostCentreId.Value))
                return Json(new { success = false, message = "Select a valid cost centre." });

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

            var vehicleError = _validation.ValidateVehicles(vehicles, out var vehicleNumbers);
            if (vehicleError != null)
                return Json(new { success = false, message = vehicleError });

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

            var injuryError = _validation.ValidateInjuries(injuries, vehicles, vehicleNumbers);
            if (injuryError != null)
                return Json(new { success = false, message = injuryError });

            // The role-based Quick Capture sends one road-user row per count. Require
            // exact agreement, while keeping compatibility with previous callers.
            var roleBasedEditor = Request.Form.ContainsKey("RoleBasedInjuryEditor");
            var totalsError = _validation.ValidateInjuryTotalsAgreement(
                injuries, model, roleBasedEditor, enforceLegacyNonRoleTotalsCheck: true);
            if (totalsError != null)
                return Json(new { success = false, message = totalsError });

            _validation.ApplyInjuryRollup(model, injuries, vehicles.Count);

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

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _context.CrashSummaries.Add(model);
                await _context.SaveChangesAsync();

                var vehicleNumberToId = await _validation.SaveVehiclesAsync(model.SummaryId, vehicles);
                await _validation.SaveInjuriesAsync(model.SummaryId, injuries, vehicleNumberToId);

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
}
