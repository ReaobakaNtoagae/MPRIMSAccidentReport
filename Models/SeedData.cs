using CrashReport.Models;
using CrashReport.Security;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

 
namespace CrashReport.Data;

public static class SeedData
{
    // Role name constants match the actual AspNetRoles rows after
    // migrate_to_real_roles.sql — the 5 roles from the documented
    // requirements, not the earlier 3 placeholder names.
    public const string SystemAdministratorRole = "System Administrator";
    public const string ProvincialStaffRole = "Provincial Staff";
    public const string RegionalStaffRole = "Regional Staff";
    public const string CostCentreAdministratorRole = "Cost Centre Administrator";
    public const string SapsOfficerRole = "SAPS Officer";

    private const string AdminEmail = "admin@gmail.com";

    // Must satisfy the policy in Program.cs: 12+ chars, upper, lower, digit,
    // non-alphanumeric, and at least 4 unique characters. The previous
    // value ("Admin@123!") was only 10 characters and would have failed
    // CreateAsync silently succeeding=false, leaving no admin user seeded.
    private const string AdminPassword = "Admin@12345!";

    public static async Task InitialiseAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var roleManager = scope.ServiceProvider
                               .GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider
                               .GetRequiredService<UserManager<ApplicationUser>>();

        // ── Seed roles + their default privileges ──────────────
        var rolePrivileges = new Dictionary<string, string[]>
        {
            { SystemAdministratorRole,      Privileges.Defaults.SystemAdministrator     },
            { ProvincialStaffRole,          Privileges.Defaults.ProvincialStaff         },
            { RegionalStaffRole,            Privileges.Defaults.RegionalStaff           },
            { CostCentreAdministratorRole,  Privileges.Defaults.CostCentreAdministrator },
            { SapsOfficerRole,              Privileges.Defaults.SapsOfficer             },
        };

        foreach (var (roleName, privileges) in rolePrivileges)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new IdentityRole(roleName));

            var role = await roleManager.FindByNameAsync(roleName);
            if (role == null) continue;

            // Add any missing privilege claims — never removes existing ones
            // so manual changes made through the UI are preserved on restart.
            var existing = await roleManager.GetClaimsAsync(role);
            var existingValues = existing
                .Where(c => c.Type == Privileges.ClaimType)
                .Select(c => c.Value)
                .ToHashSet();

            foreach (var priv in privileges)
            {
                if (!existingValues.Contains(priv))
                    await roleManager.AddClaimAsync(
                        role, new Claim(Privileges.ClaimType, priv));
            }
        }

        // ── Seed default admin user ────────────────────────────
        var admin = await userManager.FindByEmailAsync(AdminEmail);
        if (admin == null)
        {
            admin = new ApplicationUser
            {
                UserName = AdminEmail,
                Email = AdminEmail,
                FullName = "System Administrator",
                Station = "HEAD OFFICE",
                District = "PROVINCIAL",
                IsActive = true,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(admin, AdminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, SystemAdministratorRole);
            }
            else
            {
                // Without this, a failure here (e.g. AdminPassword no longer
                // meeting the policy in Program.cs after a future change)
                // seeds no admin account at all and gives no indication why —
                // exactly what happened with the previous 10-character password.
                var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                                                   .CreateLogger("CrashReport.Data.SeedData");
                var errors = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
                logger.LogError("Failed to seed default admin user ({Email}): {Errors}", AdminEmail, errors);
            }
        }
    }
}

