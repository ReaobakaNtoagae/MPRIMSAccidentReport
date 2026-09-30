using CrashReport.Models;

namespace CrashReport.Services;


public interface IUserClaimsSyncService
{
    Task SyncFullNameClaimAsync(ApplicationUser user);
}
