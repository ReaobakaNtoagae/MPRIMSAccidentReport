using CrashReport.Data;
using CrashReport.Models;
using CrashReport.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Controllers;

[Authorize(Roles = "Administrator")]   // only admins manage users
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole> _roles;

    public UsersController(
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole> roles)
    {
        _users = users;
        _roles = roles;
    }

    [Authorize(Policy = Privileges.Admin.Users)]
    public IActionResult Index() => View();

    // GET: /Users/GetAll  (JSON for DataGrid)
    [HttpGet]
    [Authorize(Policy = Privileges.Admin.Users)]
    public async Task<IActionResult> GetAll()
    {
        var users = await _users.Users.ToListAsync();

        var data = new List<object>();
        foreach (var u in users)
        {
            var roles = await _users.GetRolesAsync(u);
            data.Add(new
            {
                u.Id,
                u.FullName,
                u.Email,
                u.UserName,
                u.Station,
                u.District,
                u.IsActive,
                u.CreatedAt,
                Roles = string.Join(", ", roles)
            });
        }
        return Json(data);
    }

    // GET: /Users/Create
    [HttpGet]

    public async Task<IActionResult> Create()
    {
        ViewBag.Roles = await _roles.Roles.Select(r => r.Name).ToListAsync();
        return View();
    }

    // POST: /Users/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Privileges.Admin.Users)]
    public async Task<IActionResult> Create(
        [FromForm] string fullName,
        [FromForm] string email,
        [FromForm] string station,
        [FromForm] string district,
        [FromForm] string role,
        [FromForm] string password)
    {
        var existing = await _users.FindByEmailAsync(email);
        if (existing != null)
        {
            TempData["ErrorMessage"] = $"A user with email {email} already exists.";
            return RedirectToAction(nameof(Index));
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            Station = station,
            District = district,
            IsActive = true,
            EmailConfirmed = true
        };

        var result = await _users.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = string.Join(" ", result.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Index));
        }

        if (!string.IsNullOrWhiteSpace(role))
            await _users.AddToRoleAsync(user, role);

        TempData["SuccessMessage"] = $"User {fullName} created successfully.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Users/ToggleActive
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Privileges.Admin.Users)]
    public async Task<IActionResult> ToggleActive(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();

        // Prevent disabling yourself
        var currentUser = await _users.GetUserAsync(User);
        if (currentUser?.Id == id)
        {
            TempData["ErrorMessage"] = "You cannot deactivate your own account.";
            return RedirectToAction(nameof(Index));
        }

        user.IsActive = !user.IsActive;
        await _users.UpdateAsync(user);

        TempData["SuccessMessage"] = $"{user.FullName} has been {(user.IsActive ? "activated" : "deactivated")}.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Users/ResetPassword
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Privileges.Admin.Users)]
    public async Task<IActionResult> ResetPassword(string id, string newPassword)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var result = await _users.ResetPasswordAsync(user, token, newPassword);

        if (result.Succeeded)
            TempData["SuccessMessage"] = $"Password for {user.FullName} has been reset.";
        else
            TempData["ErrorMessage"] = string.Join(" ", result.Errors.Select(e => e.Description));

        return RedirectToAction(nameof(Index));
    }

    // POST: /Users/UpdateRole
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Privileges.Admin.Users)]
    public async Task<IActionResult> UpdateRole(string id, string newRole)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();

        var currentRoles = await _users.GetRolesAsync(user);
        await _users.RemoveFromRolesAsync(user, currentRoles);

        if (!string.IsNullOrWhiteSpace(newRole))
            await _users.AddToRoleAsync(user, newRole);

        TempData["SuccessMessage"] = $"{user.FullName}'s role updated to {newRole}.";
        return RedirectToAction(nameof(Index));
    }
}