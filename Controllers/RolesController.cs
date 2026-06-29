using System.Security.Claims;
using CrashReport.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Controllers;

[Authorize(Policy = Privileges.Admin.Roles)]
public class RolesController : Controller
{
    private readonly RoleManager<IdentityRole> _roles;

    public RolesController(RoleManager<IdentityRole> roles) => _roles = roles;

    [Authorize(Policy = Privileges.Admin.Roles)]
    public IActionResult Index() => View();

    // GET: /Roles/GetAll  — role list with their current privileges
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
                IsCore = IsCore(role.Name),
                Privileges = privs
            });
        }
        return Json(data);
    }

    // GET: /Roles/GetPrivileges — full privilege list for the UI
    [HttpGet]
    [Authorize(Policy = Privileges.Admin.Roles)]
    public IActionResult GetPrivileges() =>
        Json(Privileges.All.Select(p => new
        {
            p.Value,
            p.Label,
            p.Group
        }));

    // POST: /Roles/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Privileges.Admin.Roles)]
    public async Task<IActionResult> Create([FromForm] string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
            return Json(new { success = false, message = "Role name is required." });

        if (await _roles.RoleExistsAsync(roleName))
            return Json(new { success = false, message = $"Role '{roleName}' already exists." });

        var result = await _roles.CreateAsync(new IdentityRole(roleName.Trim()));
        return Json(result.Succeeded
            ? new { success = true, message = $"Role '{roleName}' created." }
            : new { success = false, message = string.Join(" ", result.Errors.Select(e => e.Description)) });
    }

    // POST: /Roles/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Privileges.Admin.Roles)]
    public async Task<IActionResult> Delete([FromForm] string id)
    {
        var role = await _roles.FindByIdAsync(id);
        if (role == null) return NotFound();

        if (IsCore(role.Name))
            return Json(new { success = false, message = $"'{role.Name}' is a core role and cannot be deleted." });

        var result = await _roles.DeleteAsync(role);
        return Json(result.Succeeded
            ? new { success = true, message = $"Role '{role.Name}' deleted." }
            : new { success = false, message = string.Join(" ", result.Errors.Select(e => e.Description)) });
    }

    // POST: /Roles/SetPrivileges
    // Replaces the full privilege set for a role with the posted list.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Privileges.Admin.Roles)]
    public async Task<IActionResult> SetPrivileges(
        [FromForm] string roleId,
        [FromForm] string[] privileges)
    {
        var role = await _roles.FindByIdAsync(roleId);
        if (role == null) return NotFound();

        // Remove all existing privilege claims
        var existing = await _roles.GetClaimsAsync(role);
        foreach (var claim in existing.Where(c => c.Type == Privileges.ClaimType))
            await _roles.RemoveClaimAsync(role, claim);

        // Add the new set
        foreach (var priv in privileges ?? [])
            await _roles.AddClaimAsync(role, new Claim(Privileges.ClaimType, priv));

        return Json(new { success = true, message = $"Privileges for '{role.Name}' updated." });
    }

    private static bool IsCore(string? name) =>
        name is "Administrator" or "Supervisor" or "DataCapturer";
}