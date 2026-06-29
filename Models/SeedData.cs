using System.Security.Claims;
using CrashReport.Models;
using CrashReport.Security;
using Microsoft.AspNetCore.Identity;

namespace CrashReport.Data;

public static class SeedData
{
    public const string AdminRole = "Administrator";
    public const string SupervisorRole = "Supervisor";
    public const string DataCapturerRole = "DataCapturer";

    private const string AdminEmail = "admin@saps.local";
    private const string AdminPassword = "Admin@123!";

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
            { AdminRole,        Privileges.Defaults.Administrator },
            { SupervisorRole,   Privileges.Defaults.Supervisor    },
            { DataCapturerRole, Privileges.Defaults.DataCapturer  },
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
                await userManager.AddToRoleAsync(admin, AdminRole);
        }
    }
}