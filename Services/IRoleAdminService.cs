using Microsoft.AspNetCore.Identity;

namespace CrashReport.Services;

public enum RoleAdminOutcome
{
    Success,
    NotFound,
    AlreadyExists,
    CoreRole,
    IdentityError
}

public class RoleAdminResult
{
    public RoleAdminOutcome Outcome { get; init; }
    public IdentityRole? Role { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public static RoleAdminResult Ok(IdentityRole role) =>
        new() { Outcome = RoleAdminOutcome.Success, Role = role };

    public static RoleAdminResult Fail(RoleAdminOutcome outcome, IdentityRole? role = null, IReadOnlyList<string>? errors = null) =>
        new() { Outcome = outcome, Role = role, Errors = errors ?? Array.Empty<string>() };
}


public interface IRoleAdminService
{
    bool IsCoreRole(string? roleName);
    Task<RoleAdminResult> CreateRoleAsync(string roleName);
    Task<RoleAdminResult> DeleteRoleAsync(string id);
    Task<RoleAdminOutcome> SetPrivilegesAsync(string roleId, IEnumerable<string> privileges);
}
