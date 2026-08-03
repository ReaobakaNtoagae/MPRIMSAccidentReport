using System.Security.Claims;
using CrashReport.Models;
using Microsoft.AspNetCore.Identity;

namespace CrashReport.Security;

/// <summary>
/// ASP.NET Core Identity's default claims principal factory adds a
/// ClaimTypes.Role claim for each role a user belongs to, but does NOT
/// pull in the claims stored ON those roles (AspNetRoleClaims) — the
/// privilege claims added via RoleManager.AddClaimAsync in SeedData /
/// RolesController.SetPrivileges. Without this override, every
/// RequireClaim(Privileges.ClaimType, ...) authorization policy silently
/// fails for every user, regardless of what's actually in the database,
/// because those claims never make it into the signed-in principal.
/// </summary>
public class AppUserClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    private readonly RoleManager<IdentityRole> _roleManager;

    public AppUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        Microsoft.Extensions.Options.IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, roleManager, optionsAccessor)
    {
        _roleManager = roleManager;
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        // Base implementation adds the user's own claims plus a
        // ClaimTypes.Role claim per role — everything Identity does by default.
        var identity = await base.GenerateClaimsAsync(user);

        var roleNames = await UserManager.GetRolesAsync(user);
        foreach (var roleName in roleNames)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role == null) continue;

            var roleClaims = await _roleManager.GetClaimsAsync(role);
            foreach (var claim in roleClaims)
            {
                // Avoid duplicate claims if a user has multiple roles that
                // happen to grant the same privilege.
                if (!identity.HasClaim(claim.Type, claim.Value))
                    identity.AddClaim(claim);
            }
        }

        // District/Station carried as claims too — lets the dashboard (and,
        // once built, the actual data-scoping queries) read a user's scope
        // straight off the signed-in principal instead of a DB lookup on
        // every request. NOT itself an access-control mechanism — these are
        // just labels for now; the real query-level filtering by these
        // values is a separate, not-yet-built piece.
        if (!string.IsNullOrWhiteSpace(user.District))
            identity.AddClaim(new Claim("District", user.District));
        if (!string.IsNullOrWhiteSpace(user.Station))
            identity.AddClaim(new Claim("Station", user.Station));

        return identity;
    }
}