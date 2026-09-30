using CrashReport.Data;
using CrashReport.Models;
using CrashReport.Models.Dtos;
using CrashReport.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Controllers.Api;

[Route("api/witnesses")]
[ApiController]
public class WitnessesApiController : ControllerBase

{
    private readonly AppDbContext _context;

    public WitnessesApiController(AppDbContext context)
    {
        _context = context;
    }

    private static WitnessDto ToDto(Witness w) => new()
    {
        WitnessId = w.WitnessId,
        CrashId = w.CrashId,
        SurnameInitials = w.SurnameInitials,
        IdType = w.IdType,
        IdNumber = w.IdNumber,
        WorkContactAddress = w.WorkContactAddress,
        CellPhone = w.CellPhone,
        OtherPhone = w.OtherPhone
    };

    // GET api/witnesses?crashId=5
    [HttpGet]
    [Authorize(Policy = Privileges.Crashes.View)]
    public async Task<IActionResult> GetByCrash([FromQuery] int crashId)
    {
        var witnesses = await _context.Witnesses
            .Where(w => w.CrashId == crashId)
            .OrderBy(w => w.SurnameInitials)
            .ToListAsync();

        return Ok(witnesses.Select(ToDto).ToList());
    }

    // GET api/witnesses/5
    [HttpGet("{id:int}")]
    [Authorize(Policy = Privileges.Crashes.View)]
    public async Task<IActionResult> GetById(int id)
    {
        var witness = await _context.Witnesses.FindAsync(id);
        if (witness == null) return NotFound();
        return Ok(ToDto(witness));
    }

    // POST api/witnesses
    [HttpPost]
    [Authorize(Policy = Privileges.Crashes.Edit)]
    public async Task<IActionResult> Create([FromBody] WitnessCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var witness = new Witness
        {
            CrashId = dto.CrashId,
            SurnameInitials = dto.SurnameInitials,
            IdType = dto.IdType,
            IdNumber = dto.IdNumber,
            WorkContactAddress = dto.WorkContactAddress,
            CellPhone = dto.CellPhone,
            OtherPhone = dto.OtherPhone
        };

        try
        {
            _context.Witnesses.Add(witness);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // CrashId doesn't reference an existing crash.
            return BadRequest(new { message = $"Crash {dto.CrashId} does not exist." });
        }

        return CreatedAtAction(nameof(GetById), new { id = witness.WitnessId }, ToDto(witness));
    }

    // DELETE api/witnesses/5
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Privileges.Crashes.Edit)]
    public async Task<IActionResult> Delete(int id)
    {
        var witness = await _context.Witnesses.FindAsync(id);
        if (witness == null) return NotFound();

        _context.Witnesses.Remove(witness);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
