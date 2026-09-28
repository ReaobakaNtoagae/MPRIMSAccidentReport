using CrashReport.Data;
using CrashReport.Models;
using CrashReport.Models.Dtos;
using CrashReport.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Controllers.Api;


[Route("api/persons")]
[ApiController]
public class PersonsApiController : ControllerBase
{
    private readonly AppDbContext _context;

    public PersonsApiController(AppDbContext context)
    {
        _context = context;
    }

    // GET api/persons
    [HttpGet]
    [Authorize(Policy = Privileges.Crashes.View)]
    public async Task<IActionResult> GetAll()
    {
        var data = await _context.Persons
            .Select(p => new PersonListItemDto
            {
                PersonId = p.PersonId,
                Surname = p.Surname,
                FullNames = p.FullNames,
                IdNumber = p.IdNumber,
                IdType = p.IdType,
                Gender = p.Gender,
                CellPhone = p.CellPhone,
                PopulationGroup = p.PopulationGroup,
                CrashCount = p.CrashPeople.Count
            })
            .OrderBy(p => p.Surname)
            .ToListAsync();

        return Ok(data);
    }

    // GET api/persons/5
    [HttpGet("{id:int}")]
    [Authorize(Policy = Privileges.Crashes.View)]
    public async Task<IActionResult> GetById(int id)
    {
        var person = await _context.Persons
            .Include(p => p.DriversLicences)
            .Include(p => p.CrashPeople)
                .ThenInclude(cp => cp.Crash)
            .FirstOrDefaultAsync(p => p.PersonId == id);

        if (person == null) return NotFound();

        var dto = new PersonDetailDto
        {
            PersonId = person.PersonId,
            IdType = person.IdType,
            IdNumber = person.IdNumber,
            Age = person.Age,
            Surname = person.Surname,
            FullNames = person.FullNames,
            CountryOfOrigin = person.CountryOfOrigin,
            Nationality = person.Nationality,
            PopulationGroup = person.PopulationGroup,
            Gender = person.Gender,
            HomeAddress = person.HomeAddress,
            CellPhone = person.CellPhone,
            OtherPhone = person.OtherPhone,
            WorkContactAddress = person.WorkContactAddress,
            CreatedAt = person.CreatedAt,
            CrashInvolvements = person.CrashPeople.Select(cp => new PersonCrashInvolvementDto
            {
                CrashId = cp.CrashId,
                CrNo = cp.Crash?.CrNo,
                Role = cp.Role,
                SeverityOfInjury = cp.SeverityOfInjury
            }).ToList(),
            DriversLicences = person.DriversLicences.Select(l => new DriversLicenceDto
            {
                LicenceId = l.LicenceId,
                LicenceType = l.LicenceType,
                LicenceNumber = l.LicenceNumber,
                LicenceCode = l.LicenceCode,
                DateOfIssue = l.DateOfIssue,
                PrdpCode = l.PrdpCode
            }).ToList()
        };

        return Ok(dto);
    }

    // POST api/persons
    [HttpPost]
    [Authorize(Policy = Privileges.Crashes.Create)]
    public async Task<IActionResult> Create([FromBody] PersonCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var person = new Person
        {
            IdType = dto.IdType,
            IdNumber = dto.IdNumber,
            Age = dto.Age,
            Surname = dto.Surname,
            FullNames = dto.FullNames,
            CountryOfOrigin = dto.CountryOfOrigin,
            Nationality = dto.Nationality,
            PopulationGroup = dto.PopulationGroup,
            Gender = dto.Gender,
            HomeAddress = dto.HomeAddress,
            CellPhone = dto.CellPhone,
            OtherPhone = dto.OtherPhone,
            WorkContactAddress = dto.WorkContactAddress
        };

        _context.Persons.Add(person);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = person.PersonId }, new { id = person.PersonId });
    }

    // PUT api/persons/5
    [HttpPut("{id:int}")]
    [Authorize(Policy = Privileges.Crashes.Edit)]
    public async Task<IActionResult> Update(int id, [FromBody] PersonUpdateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var person = await _context.Persons.FindAsync(id);
        if (person == null) return NotFound();

        person.IdType = dto.IdType;
        person.IdNumber = dto.IdNumber;
        person.Age = dto.Age;
        person.Surname = dto.Surname;
        person.FullNames = dto.FullNames;
        person.CountryOfOrigin = dto.CountryOfOrigin;
        person.Nationality = dto.Nationality;
        person.PopulationGroup = dto.PopulationGroup;
        person.Gender = dto.Gender;
        person.HomeAddress = dto.HomeAddress;
        person.CellPhone = dto.CellPhone;
        person.OtherPhone = dto.OtherPhone;
        person.WorkContactAddress = dto.WorkContactAddress;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await _context.Persons.AnyAsync(p => p.PersonId == id)) return NotFound();
            throw;
        }

        return NoContent();
    }

    // DELETE api/persons/5
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Privileges.Crashes.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var person = await _context.Persons.FindAsync(id);
        if (person == null) return NotFound();

        _context.Persons.Remove(person);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Person is still referenced by a crash (CrashPerson/CrashVehicle driver
            // link/DriversLicence) — surface as a conflict rather than a 500.
            return Conflict(new { message = "This person cannot be deleted because they are still linked to one or more crash records." });
        }

        return NoContent();
    }
}
