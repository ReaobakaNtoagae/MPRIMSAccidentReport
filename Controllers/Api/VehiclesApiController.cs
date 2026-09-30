using CrashReport.Data;
using CrashReport.Models;
using CrashReport.Models.Dtos;
using CrashReport.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Controllers.Api;


[Route("api/vehicles")]
[ApiController]
public class VehiclesApiController : ControllerBase
{
    private readonly AppDbContext _context;

    public VehiclesApiController(AppDbContext context)
    {
        _context = context;
    }

    // GET api/vehicles
    [HttpGet]
    [Authorize(Policy = Privileges.Crashes.View)]
    public async Task<IActionResult> GetAll()
    {
        var data = await _context.Vehicles
            .Select(v => new VehicleListItemDto
            {
                VehicleId = v.VehicleId,
                LicenceDiscNumber = v.LicenceDiscNumber,
                Make = v.Make,
                Model = v.Model,
                Colour = v.Colour,
                VehicleCategory = v.VehicleCategory,
                SpecialFunction = v.SpecialFunction,
                PrivateOrBusiness = v.PrivateOrBusiness,
                VinNumber = v.VinNumber,
                CrashCount = v.CrashVehicles.Count
            })
            .OrderBy(v => v.Make)
            .ToListAsync();

        return Ok(data);
    }

    // GET api/vehicles/5
    [HttpGet("{id:int}")]
    [Authorize(Policy = Privileges.Crashes.View)]
    public async Task<IActionResult> GetById(int id)
    {
        var vehicle = await _context.Vehicles
            .Include(v => v.CrashVehicles)
                .ThenInclude(cv => cv.Crash)
            .FirstOrDefaultAsync(v => v.VehicleId == id);

        if (vehicle == null) return NotFound();

        var dto = new VehicleDetailDto
        {
            VehicleId = vehicle.VehicleId,
            CountryOfRegistration = vehicle.CountryOfRegistration,
            LicenceDiscNumber = vehicle.LicenceDiscNumber,
            Colour = vehicle.Colour,
            Make = vehicle.Make,
            Model = vehicle.Model,
            VinNumber = vehicle.VinNumber,
            TrailerLicenceNumber = vehicle.TrailerLicenceNumber,
            VehicleCategory = vehicle.VehicleCategory,
            VehicleTypeCode = vehicle.VehicleTypeCode,
            SpecialFunction = vehicle.SpecialFunction,
            PrivateOrBusiness = vehicle.PrivateOrBusiness,
            LicenceTypeFitting = vehicle.LicenceTypeFitting,
            CreatedAt = vehicle.CreatedAt,
            CrashInvolvements = vehicle.CrashVehicles.Select(cv => new VehicleCrashInvolvementDto
            {
                CrashId = cv.CrashId,
                CrNo = cv.Crash?.CrNo,
                VehicleReference = cv.VehicleReference
            }).ToList()
        };

        return Ok(dto);
    }

    // POST api/vehicles
    [HttpPost]
    [Authorize(Policy = Privileges.Crashes.Create)]
    public async Task<IActionResult> Create([FromBody] VehicleCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var vehicle = new Vehicle
        {
            CountryOfRegistration = dto.CountryOfRegistration,
            LicenceDiscNumber = dto.LicenceDiscNumber,
            Colour = dto.Colour,
            Make = dto.Make,
            Model = dto.Model,
            VinNumber = dto.VinNumber,
            TrailerLicenceNumber = dto.TrailerLicenceNumber,
            VehicleCategory = dto.VehicleCategory,
            VehicleTypeCode = dto.VehicleTypeCode,
            SpecialFunction = dto.SpecialFunction,
            PrivateOrBusiness = dto.PrivateOrBusiness,
            LicenceTypeFitting = dto.LicenceTypeFitting
        };

        _context.Vehicles.Add(vehicle);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = vehicle.VehicleId }, new { id = vehicle.VehicleId });
    }

    // PUT api/vehicles/5
    [HttpPut("{id:int}")]
    [Authorize(Policy = Privileges.Crashes.Edit)]
    public async Task<IActionResult> Update(int id, [FromBody] VehicleUpdateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var vehicle = await _context.Vehicles.FindAsync(id);
        if (vehicle == null) return NotFound();

        vehicle.CountryOfRegistration = dto.CountryOfRegistration;
        vehicle.LicenceDiscNumber = dto.LicenceDiscNumber;
        vehicle.Colour = dto.Colour;
        vehicle.Make = dto.Make;
        vehicle.Model = dto.Model;
        vehicle.VinNumber = dto.VinNumber;
        vehicle.TrailerLicenceNumber = dto.TrailerLicenceNumber;
        vehicle.VehicleCategory = dto.VehicleCategory;
        vehicle.VehicleTypeCode = dto.VehicleTypeCode;
        vehicle.SpecialFunction = dto.SpecialFunction;
        vehicle.PrivateOrBusiness = dto.PrivateOrBusiness;
        vehicle.LicenceTypeFitting = dto.LicenceTypeFitting;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await _context.Vehicles.AnyAsync(v => v.VehicleId == id)) return NotFound();
            throw;
        }

        return NoContent();
    }

    // DELETE api/vehicles/5
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Privileges.Crashes.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var vehicle = await _context.Vehicles.FindAsync(id);
        if (vehicle == null) return NotFound();

        _context.Vehicles.Remove(vehicle);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            
            return Conflict(new { message = "This vehicle cannot be deleted because it is still linked to one or more crash records." });
        }

        return NoContent();
    }
}
