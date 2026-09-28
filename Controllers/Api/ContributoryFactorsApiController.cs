using CrashReport.Data;
using CrashReport.Models;
using CrashReport.Models.Dtos;
using CrashReport.Security;
using CrashReport.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Controllers.Api;

/// <summary>
/// JSON API counterpart of ContributoryFactorsController (MVC), for the
/// Angular/DevExtreme migration. Purely additive — does not change the MVC
/// controller, its views, or its routes.
///
/// Create's "only one factor per crash may be the major factor" rule was
/// non-trivial cross-entity logic, so it has been extracted into
/// IContributoryFactorService/ContributoryFactorService (see Services/), which
/// this controller and the MVC controller could both use.
///
/// The MVC controller only exposes Create/Delete (no list/details actions), so
/// GetByCrash/GetById below are new, REST-appropriate additions for the SPA to
/// fetch a crash's contributory factors — authorized the same way Persons/
/// Vehicles reads are (Crashes.View), matching the read-vs-mutate privilege
/// split used throughout this group.
/// </summary>
[Route("api/contributory-factors")]
[ApiController]
public class ContributoryFactorsApiController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IContributoryFactorService _factorService;

    public ContributoryFactorsApiController(AppDbContext context, IContributoryFactorService factorService)
    {
        _context = context;
        _factorService = factorService;
    }

    private static ContributoryFactorDto ToDto(ContributoryFactor f) => new()
    {
        FactorId = f.FactorId,
        CrashId = f.CrashId,
        FactorCategory = f.FactorCategory,
        FactorDescription = f.FactorDescription,
        IsMajorFactor = f.IsMajorFactor
    };

    // GET api/contributory-factors?crashId=5
    [HttpGet]
    [Authorize(Policy = Privileges.Crashes.View)]
    public async Task<IActionResult> GetByCrash([FromQuery] int crashId)
    {
        var factors = await _context.ContributoryFactors
            .Where(f => f.CrashId == crashId)
            .OrderByDescending(f => f.IsMajorFactor)
            .ThenBy(f => f.FactorCategory)
            .ToListAsync();

        return Ok(factors.Select(ToDto).ToList());
    }

    // GET api/contributory-factors/5
    [HttpGet("{id:int}")]
    [Authorize(Policy = Privileges.Crashes.View)]
    public async Task<IActionResult> GetById(int id)
    {
        var factor = await _context.ContributoryFactors.FindAsync(id);
        if (factor == null) return NotFound();
        return Ok(ToDto(factor));
    }

    // POST api/contributory-factors
    [HttpPost]
    [Authorize(Policy = Privileges.Crashes.Edit)]
    public async Task<IActionResult> Create([FromBody] ContributoryFactorCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var factor = new ContributoryFactor
        {
            CrashId = dto.CrashId,
            FactorCategory = dto.FactorCategory,
            FactorDescription = dto.FactorDescription,
            IsMajorFactor = dto.IsMajorFactor
        };

        try
        {
            await _factorService.CreateAsync(factor);
        }
        catch (DbUpdateException)
        {
            // CrashId doesn't reference an existing crash.
            return BadRequest(new { message = $"Crash {dto.CrashId} does not exist." });
        }

        return CreatedAtAction(nameof(GetById), new { id = factor.FactorId }, ToDto(factor));
    }

    // DELETE api/contributory-factors/5
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Privileges.Crashes.Edit)]
    public async Task<IActionResult> Delete(int id)
    {
        var factor = await _context.ContributoryFactors.FindAsync(id);
        if (factor == null) return NotFound();

        _context.ContributoryFactors.Remove(factor);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
