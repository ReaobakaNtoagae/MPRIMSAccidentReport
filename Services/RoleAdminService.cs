using System.Security.Claims;
using CrashReport.Security;
using Microsoft.AspNetCore.Identity;

namespace CrashReport.Services;

public class RoleAdminService : IRoleAdminService
{
    private readonly RoleManager<IdentityRole> _roles;

    public RoleAdminService(RoleManager<IdentityRole> roles) => _roles = roles;

    public bool IsCoreRole(string? roleName) =>
        roleName is "System Administrator" or "Provincial Staff" or "Regional Staff"
                 or "Cost Centre Administrator" or "SAPS Officer";

    public async Task<RoleAdminResult> CreateRoleAsync(string roleName)
    {
        if (await _roles.RoleExistsAsync(roleName))
            return RoleAdminResult.Fail(RoleAdminOutcome.AlreadyExists);

        var role = new IdentityRole(roleName.Trim());
        var result = await _roles.CreateAsync(role);
        if (!result.Succeeded)
            return RoleAdminResult.Fail(RoleAdminOutcome.IdentityError, errors: result.Errors.Select(e => e.Description).ToList());

        return RoleAdminResult.Ok(role);
    }

    public async Task<RoleAdminResult> DeleteRoleAsync(string id)
    {
        var role = await _roles.FindByIdAsync(id);
        if (role == null)
            return RoleAdminResult.Fail(RoleAdminOutcome.NotFound);

        if (IsCoreRole(role.Name))
            return RoleAdminResult.Fail(RoleAdminOutcome.CoreRole, role);

        var result = await _roles.DeleteAsync(role);
        if (!result.Succeeded)
            return RoleAdminResult.Fail(RoleAdminOutcome.IdentityError, role, result.Errors.Select(e => e.Description).ToList());

        return RoleAdminResult.Ok(role);
    }

    public async Task<RoleAdminOutcome> SetPrivilegesAsync(string roleId, IEnumerable<string> privileges)
    {
        var role = await _roles.FindByIdAsync(roleId);
        if (role == null)
            return RoleAdminOutcome.NotFound;

        
        var existing = await _roles.GetClaimsAsync(role);
        foreach (var claim in existing.Where(c => c.Type == Privileges.ClaimType))
            await _roles.RemoveClaimAsync(role, claim);

        
        foreach (var priv in privileges)
            await _roles.AddClaimAsync(role, new Claim(Privileges.ClaimType, priv));

        return RoleAdminOutcome.Success;
    }
}
