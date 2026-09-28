using System.Security.Claims;
using CrashReport.Models;
using Microsoft.AspNetCore.Identity;

namespace CrashReport.Services;

public class UserClaimsSyncService : IUserClaimsSyncService
{
    private readonly UserManager<ApplicationUser> _users;

    public UserClaimsSyncService(UserManager<ApplicationUser> users) => _users = users;

    public async Task SyncFullNameClaimAsync(ApplicationUser user)
    {
        var existingClaims = await _users.GetClaimsAsync(user);
        var existing = existingClaims.FirstOrDefault(c => c.Type == "FullName");

        if (existing == null)
        {
            await _users.AddClaimAsync(user, new Claim("FullName", user.FullName));
        }
        else if (existing.Value != user.FullName)
        {
            await _users.RemoveClaimAsync(user, existing);
            await _users.AddClaimAsync(user, new Claim("FullName", user.FullName));
        }
    }
}
