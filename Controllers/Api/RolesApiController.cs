using CrashReport.Models.Dtos;
using CrashReport.Security;
using CrashReport.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Controllers.Api;


[Route("api/roles")]
[ApiController]
[Authorize(Policy = Privileges.Admin.Roles)]
public class RolesApiController : ControllerBase
{
    private readonly RoleManager<IdentityRole> _roles;
    private readonly IRoleAdminService _roleAdmin;

    public RolesApiController(RoleManager<IdentityRole> roles, IRoleAdminService roleAdmin)
    {
        _roles = roles;
        _roleAdmin = roleAdmin;
    }

    // GET api/roles — role list with their current privileges
    [HttpGet]
    [Authorize(Policy = Privileges.Admin.Roles)]
    public async Task<IActionResult> GetAll()
    {
        var data = new List<object>();
        foreach (var role in await _roles.Roles.ToListAsync())
        {
            var claims = await _roles.GetClaimsAsync(role);
            var privs = claims
                .Where(c => c.Type == Privileges.ClaimType)
                .Select(c => c.Value)
                .ToList();

            data.Add(new
            {
                role.Id,
                role.Name,
                IsCore = _roleAdmin.IsCoreRole(role.Name),
                Privileges = privs
            });
        }
        return Ok(data);
    }

    // GET api/roles/privileges — full privilege list for the UI
    [HttpGet("privileges")]
    [Authorize(Policy = Privileges.Admin.Roles)]
    public IActionResult GetPrivileges() =>
        Ok(Privileges.All.Select(p => new
        {
            p.Value,
            p.Label,
            p.Group
        }));

    // POST api/roles
    [HttpPost]
    [Authorize(Policy = Privileges.Admin.Roles)]
    public async Task<IActionResult> Create([FromBody] CreateRoleRequest req)
    {
        var result = await _roleAdmin.CreateRoleAsync(req.RoleName);

        return result.Outcome switch
        {
            RoleAdminOutcome.Success => Created($"/api/roles/{result.Role!.Id}", new { result.Role.Id, result.Role.Name }),
            RoleAdminOutcome.AlreadyExists => Conflict(new { message = $"Role '{req.RoleName}' already exists." }),
            RoleAdminOutcome.IdentityError => BadRequest(new { errors = result.Errors }),
            _ => BadRequest()
        };
    }

    // DELETE api/roles/{id}
    [HttpDelete("{id}")]
    [Authorize(Policy = Privileges.Admin.Roles)]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _roleAdmin.DeleteRoleAsync(id);

        return result.Outcome switch
        {
            RoleAdminOutcome.Success => NoContent(),
            RoleAdminOutcome.NotFound => NotFound(),
            RoleAdminOutcome.CoreRole => BadRequest(new { message = $"'{result.Role?.Name}' is a core role and cannot be deleted." }),
            RoleAdminOutcome.IdentityError => BadRequest(new { errors = result.Errors }),
            _ => BadRequest()
        };
    }

    // PUT api/roles/{id}/privileges
    [HttpPut("{id}/privileges")]
    [Authorize(Policy = Privileges.Admin.Roles)]
    public async Task<IActionResult> SetPrivileges(string id, [FromBody] SetPrivilegesRequest req)
    {
        var outcome = await _roleAdmin.SetPrivilegesAsync(id, req.Privileges);

        return outcome switch
        {
            RoleAdminOutcome.Success => NoContent(),
            RoleAdminOutcome.NotFound => NotFound(),
            _ => BadRequest()
        };
    }
}
