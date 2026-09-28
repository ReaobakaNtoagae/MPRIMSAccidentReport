using CrashReport.Models;

namespace CrashReport.Services;

/// <summary>
/// Extracted from AccountController.Login's inline "add or refresh the
/// FullName claim on sign-in" block — genuine custom logic (not a plain
/// UserManager passthrough: it has to read the existing claim, compare, and
/// decide whether to add or replace it) that AccountApiController's login
/// endpoint also needs, so it has one home instead of being duplicated.
/// AccountController (MVC) itself is left untouched and keeps its own inline
/// copy; only the new API controller consumes this.
/// </summary>
public interface IUserClaimsSyncService
{
    Task SyncFullNameClaimAsync(ApplicationUser user);
}
