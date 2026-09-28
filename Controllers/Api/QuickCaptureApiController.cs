using CrashReport.Data;
using CrashReport.Models.Dtos;
using CrashReport.Security;
using CrashReport.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Controllers.Api;


[Route("api/quick-capture")]
[ApiController]
public class QuickCaptureApiController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ICrashSummaryValidationService _validation;

    public QuickCaptureApiController(AppDbContext context, ICrashSummaryValidationService validation)
    {
        _context = context;
        _validation = validation;
    }

    [HttpPost]
    [Authorize(Policy = Privileges.Crashes.CreateSummary)]
    public async Task<IActionResult> Create([FromBody] QuickCaptureSummaryRequest request)
    {
        var model = request.Summary;

        if (!model.CostCentreId.HasValue ||
            !await _context.LookupCostCentres.AnyAsync(c => c.CostCentreId == model.CostCentreId.Value))
            return BadRequest(new { message = "Select a valid cost centre." });

        if (string.IsNullOrWhiteSpace(model.Station)) return BadRequest(new { message = "Station is required." });
        if (string.IsNullOrWhiteSpace(model.CrNo)) return BadRequest(new { message = "CR number is required." });

        try
        {
            model.CrNo = QuickCaptureIdentifier.FormatAr(model.CrNo, model.CrashDate);
            model.CasNo = QuickCaptureIdentifier.FormatCas(model.CasNo, model.CrashDate);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }

        var vehicles = request.Vehicles;
        var vehicleError = _validation.ValidateVehicles(vehicles, out var vehicleNumbers);
        if (vehicleError != null) return BadRequest(new { message = vehicleError });

        var injuries = request.Injuries;
        var injuryError = _validation.ValidateInjuries(injuries, vehicles, vehicleNumbers);
        if (injuryError != null) return BadRequest(new { message = injuryError });

        var totalsError = _validation.ValidateInjuryTotalsAgreement(
            injuries, model, request.RoleBasedEditor, enforceLegacyNonRoleTotalsCheck: true);
        if (totalsError != null) return BadRequest(new { message = totalsError });

        _validation.ApplyInjuryRollup(model, injuries, vehicles.Count);

        if (await _context.CrashSummaries.AnyAsync(s => s.Station == model.Station && s.CrNo == model.CrNo))
            return Conflict(new { message = $"AR number '{model.CrNo}' already exists for {model.Station}." });
        if (model.CasNo is not null && await _context.CrashSummaries.AnyAsync(s => s.Station == model.Station && s.CasNo == model.CasNo))
            return Conflict(new { message = $"CAS number '{model.CasNo}' already exists for {model.Station}." });

        var existsAsFullReport = await _context.Crashes.AnyAsync(c => c.CrNo == model.CrNo);
        if (existsAsFullReport)
            return Conflict(new { message = $"A record with AR/CR number '{model.CrNo}' already exists as a full report." });

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
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = $"Something went wrong while saving: {ex.Message} | Inner: {ex.InnerException?.Message}" });
        }

        return CreatedAtAction(
            nameof(CrashesApiController.GetSummary), "CrashesApi", new { id = model.SummaryId },
            new { crNo = model.CrNo, summaryId = model.SummaryId });
    }
}
