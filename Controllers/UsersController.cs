using CrashReport.Data;
using CrashReport.Models;
using CrashReport.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Controllers;

[Authorize(Roles = "Administrator")]
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


    [HttpGet]
    [Authorize(Policy = Privileges.Admin.Users)]
    public async Task<IActionResult> GetAll()
    {
        var users = await _users.Users.ToListAsync();

        var data = new List<object>();
        foreach (var u in users)
        {
            var roles = await _users.GetRolesAsync(u);
            var currentUser = await _users.GetUserAsync(User);
            data.Add(new
            {
                u.Id,
                u.FullName,
                u.Email,
                u.UserName,
                u.District,
                u.IsActive,
                u.CreatedAt,
                Roles = string.Join(", ", roles),
                IsSelf = currentUser?.Id == u.Id
            });
        }
        return Json(data);
    }

    // GET: /Users/GetRoles — role name list for dropdowns, sourced from AspNetRoles
    // (same table RolesController manages), so this list always matches whatever
    // roles actually exist rather than a hardcoded copy living in the view.
    [HttpGet]
    [Authorize(Policy = Privileges.Admin.Users)]
    public async Task<IActionResult> GetRoles()
    {
        var names = await _roles.Roles
            .OrderBy(r => r.Name)
            .Select(r => r.Name)
            .ToListAsync();

        return Json(names);
    }

    // POST: /Users/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Privileges.Admin.Users)]
    public async Task<IActionResult> Create(
        [FromForm] string fullName,
        [FromForm] string email,
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

        if (!string.IsNullOrWhiteSpace(role) && !await _roles.RoleExistsAsync(role))
        {
            TempData["ErrorMessage"] = $"Role '{role}' does not exist.";
            return RedirectToAction(nameof(Index));
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
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

    // GET: /Users/Edit/{id}
    [HttpGet]
    [Authorize(Policy = Privileges.Admin.Users)]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();

        var currentRoles = await _users.GetRolesAsync(user);

        var model = new EditUserViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            District = user.District,
            IsActive = user.IsActive,
            Role = currentRoles.FirstOrDefault()
        };

        ViewBag.Roles = await _roles.Roles.Select(r => r.Name).ToListAsync();
        ViewBag.IsSelf = (await _users.GetUserAsync(User))?.Id == id;

        return View(model);
    }

    // POST: /Users/Edit
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Privileges.Admin.Users)]
    public async Task<IActionResult> Edit(EditUserViewModel model)
    {
        var user = await _users.FindByIdAsync(model.Id);
        if (user == null) return NotFound();

        if (!ModelState.IsValid)
        {
            ViewBag.Roles = await _roles.Roles.Select(r => r.Name).ToListAsync();
            ViewBag.IsSelf = (await _users.GetUserAsync(User))?.Id == model.Id;
            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(model.Role) && !await _roles.RoleExistsAsync(model.Role))
        {
            TempData["ErrorMessage"] = $"Role '{model.Role}' does not exist.";
            return RedirectToAction(nameof(Edit), new { id = model.Id });
        }

        var currentUser = await _users.GetUserAsync(User);
        var isSelf = currentUser?.Id == model.Id;

        // Prevent deactivating your own account
        if (isSelf && !model.IsActive)
        {
            TempData["ErrorMessage"] = "You cannot deactivate your own account.";
            return RedirectToAction(nameof(Edit), new { id = model.Id });
        }

        // --- Profile fields ---
        var emailChanged = !string.Equals(user.Email, model.Email, StringComparison.OrdinalIgnoreCase);
        if (emailChanged)
        {
            var existing = await _users.FindByEmailAsync(model.Email);
            if (existing != null && existing.Id != user.Id)
            {
                TempData["ErrorMessage"] = $"A user with email {model.Email} already exists.";
                return RedirectToAction(nameof(Edit), new { id = model.Id });
            }
            user.Email = model.Email;
            user.UserName = model.Email;
        }

        user.FullName = model.FullName;
        user.District = model.District;
        user.IsActive = model.IsActive;

        var updateResult = await _users.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            TempData["ErrorMessage"] = string.Join(" ", updateResult.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Edit), new { id = model.Id });
        }

        // --- Role (read/written against AspNetUserRoles via Identity) ---
        var currentRoles = await _users.GetRolesAsync(user);
        if (!currentRoles.Contains(model.Role))
        {
            await _users.RemoveFromRolesAsync(user, currentRoles);
            if (!string.IsNullOrWhiteSpace(model.Role))
                await _users.AddToRoleAsync(user, model.Role);
        }

        // --- Password (optional) ---
        if (!string.IsNullOrWhiteSpace(model.NewPassword))
        {
            var token = await _users.GeneratePasswordResetTokenAsync(user);
            var pwResult = await _users.ResetPasswordAsync(user, token, model.NewPassword);
            if (!pwResult.Succeeded)
            {
                TempData["ErrorMessage"] = string.Join(" ", pwResult.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Edit), new { id = model.Id });
            }
        }

        TempData["SuccessMessage"] = $"{user.FullName} has been updated.";
        return RedirectToAction(nameof(Index));
    }
}