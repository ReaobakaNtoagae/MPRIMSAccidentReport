using CrashReport.Models;

namespace CrashReport.Services;

public enum UserAdminOutcome
{
    Success,
    NotFound,
    EmailTaken,
    RoleInvalid,
    CannotDeactivateSelf,
    IdentityError
}

public class UserAdminResult
{
    public UserAdminOutcome Outcome { get; init; }
    public ApplicationUser? User { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public static UserAdminResult Ok(ApplicationUser user) =>
        new() { Outcome = UserAdminOutcome.Success, User = user };

    public static UserAdminResult Fail(UserAdminOutcome outcome, IReadOnlyList<string>? errors = null) =>
        new() { Outcome = outcome, Errors = errors ?? Array.Empty<string>() };
}

/// <summary>
/// Holds the user create/update business rules that used to live inline in
/// UsersController (email-uniqueness checks, role-existence validation, the
/// "can't deactivate your own account" guard, and diffing/re-applying a
/// user's single role) so the same rules can be shared by both the MVC
/// controller's flow and UsersApiController instead of re-implemented per
/// surface. UsersController (MVC) itself is left untouched and keeps its own
/// inline copy of this logic; only the new API controller consumes this.
/// </summary>
public interface IUserAdminService
{
    Task<UserAdminResult> CreateUserAsync(string fullName, string email, string? district, string? role, string password);

    Task<UserAdminResult> UpdateUserAsync(
        string id,
        string fullName,
        string email,
        string? district,
        string? role,
        bool isActive,
        string? newPassword,
        string? currentUserId);
}
