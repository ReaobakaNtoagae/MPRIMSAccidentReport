using CrashReport.Models;
using Microsoft.AspNetCore.Identity;

namespace CrashReport.Services;

public class UserAdminService : IUserAdminService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole> _roles;

    public UserAdminService(UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles)
    {
        _users = users;
        _roles = roles;
    }

    public async Task<UserAdminResult> CreateUserAsync(string fullName, string email, string? district, string? role, string password)
    {
        var existing = await _users.FindByEmailAsync(email);
        if (existing != null)
            return UserAdminResult.Fail(UserAdminOutcome.EmailTaken);

        if (!string.IsNullOrWhiteSpace(role) && !await _roles.RoleExistsAsync(role))
            return UserAdminResult.Fail(UserAdminOutcome.RoleInvalid);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            District = district ?? string.Empty,
            IsActive = true,
            EmailConfirmed = true
        };

        var result = await _users.CreateAsync(user, password);
        if (!result.Succeeded)
            return UserAdminResult.Fail(UserAdminOutcome.IdentityError, result.Errors.Select(e => e.Description).ToList());

        if (!string.IsNullOrWhiteSpace(role))
            await _users.AddToRoleAsync(user, role);

        return UserAdminResult.Ok(user);
    }

    public async Task<UserAdminResult> UpdateUserAsync(
        string id,
        string fullName,
        string email,
        string? district,
        string? role,
        bool isActive,
        string? newPassword,
        string? currentUserId)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null)
            return UserAdminResult.Fail(UserAdminOutcome.NotFound);

        if (!string.IsNullOrWhiteSpace(role) && !await _roles.RoleExistsAsync(role))
            return UserAdminResult.Fail(UserAdminOutcome.RoleInvalid);

        var isSelf = currentUserId != null && currentUserId == id;

        // Prevent deactivating your own account — same rule as UsersController.Edit.
        if (isSelf && !isActive)
            return UserAdminResult.Fail(UserAdminOutcome.CannotDeactivateSelf);

        // --- Profile fields ---
        var emailChanged = !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase);
        if (emailChanged)
        {
            var existing = await _users.FindByEmailAsync(email);
            if (existing != null && existing.Id != user.Id)
                return UserAdminResult.Fail(UserAdminOutcome.EmailTaken);

            user.Email = email;
            user.UserName = email;
        }

        user.FullName = fullName;
        user.District = district ?? string.Empty;
        user.IsActive = isActive;

        var updateResult = await _users.UpdateAsync(user);
        if (!updateResult.Succeeded)
            return UserAdminResult.Fail(UserAdminOutcome.IdentityError, updateResult.Errors.Select(e => e.Description).ToList());

        // --- Role (read/written against AspNetUserRoles via Identity) ---
        var currentRoles = await _users.GetRolesAsync(user);
        if (!currentRoles.Contains(role))
        {
            await _users.RemoveFromRolesAsync(user, currentRoles);
            if (!string.IsNullOrWhiteSpace(role))
                await _users.AddToRoleAsync(user, role);
        }

        // --- Password (optional) ---
        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            var token = await _users.GeneratePasswordResetTokenAsync(user);
            var pwResult = await _users.ResetPasswordAsync(user, token, newPassword);
            if (!pwResult.Succeeded)
                return UserAdminResult.Fail(UserAdminOutcome.IdentityError, pwResult.Errors.Select(e => e.Description).ToList());
        }

        return UserAdminResult.Ok(user);
    }
}
