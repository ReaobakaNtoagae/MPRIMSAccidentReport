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
