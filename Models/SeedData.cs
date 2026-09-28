using CrashReport.Models;
using CrashReport.Security;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Security.Cryptography;


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

    // No fixed seed password anymore — a hardcoded one is a known credential in
    // source/git history regardless of what's deployed today. A fresh random
    // password is generated per install (GenerateSecurePassword, below),
    // satisfies the same policy the previous constant had to (12+ chars, upper,
    // lower, digit, non-alphanumeric, 4 unique characters — see Program.cs),
    // and is surfaced exactly once via App_Data/admin-initial-password.txt.
    // The account is flagged MustChangePassword so it can't be used for
    // anything until that password is retrieved and changed on first login.

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
            var generatedPassword = GenerateSecurePassword();

            admin = new ApplicationUser
            {
                UserName = AdminEmail,
                Email = AdminEmail,
                FullName = "System Administrator",
                Station = "HEAD OFFICE",
                District = "PROVINCIAL",
                IsActive = true,
                EmailConfirmed = true,
                MustChangePassword = true
            };
            var result = await userManager.CreateAsync(admin, generatedPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, SystemAdministratorRole);
                SurfaceInitialPassword(app, AdminEmail, generatedPassword);
            }
            else
            {
                // Without this, a failure here (e.g. the generator producing a
                // password that somehow doesn't meet the policy in Program.cs)
                // seeds no admin account at all and gives no indication why —
                // exactly what happened with the previous hardcoded password
                // once it was too short for a tightened policy.
                var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                                                   .CreateLogger("CrashReport.Data.SeedData");
                var errors = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
                logger.LogError("Failed to seed default admin user ({Email}): {Errors}", AdminEmail, errors);
            }
        }
    }

    // Writes the freshly generated password to a file once, outside wwwroot so it's
    // never web-servable, and logs a warning pointing at it. Deliberately a file, not
    // console output — under IIS in-process hosting, console output isn't visible
    // anywhere an operator can retrieve it.
    private static void SurfaceInitialPassword(WebApplication app, string email, string password)
    {
        var env = app.Services.GetRequiredService<IWebHostEnvironment>();
        var dir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "admin-initial-password.txt");

        var contents =
            $"Generated {DateTime.UtcNow:u} UTC{Environment.NewLine}" +
            $"Account:  {email}{Environment.NewLine}" +
            $"Temporary password: {password}{Environment.NewLine}{Environment.NewLine}" +
            "This account must change its password on first login before it can do " +
            "anything else. Retrieve this password, sign in, change it, then delete " +
            "this file — it is overwritten with a new password only if the account " +
            "row is ever deleted and re-seeded, so don't rely on it staying in sync." +
            Environment.NewLine;

        File.WriteAllText(path, contents);

        var logger = app.Services.GetRequiredService<ILoggerFactory>()
                                  .CreateLogger("CrashReport.Data.SeedData");
        logger.LogWarning(
            "Seeded a new {Role} account ({Email}) with a randomly generated password. " +
            "Retrieve it from {Path}, sign in, change it, then delete that file.",
            SystemAdministratorRole, email, path);
    }

    // Cryptographically random, guaranteed to satisfy AddIdentity's password policy in
    // Program.cs (12+ chars, upper, lower, digit, non-alphanumeric, 4+ unique chars) by
    // construction rather than by chance: one character from each required class is
    // placed explicitly, the rest are drawn from the combined set, then the whole thing
    // is shuffled so the required characters aren't always in the same positions.
    // Ambiguous-looking characters (I/O/0/1 etc.) are excluded since a human has to
    // read and retype this one.
    private static string GenerateSecurePassword(int length = 16)
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string special = "!@#$%^&*?-_=+";
        string all = upper + lower + digits + special;

        char[] pw = new char[length];
        pw[0] = PickRandom(upper);
        pw[1] = PickRandom(lower);
        pw[2] = PickRandom(digits);
        pw[3] = PickRandom(special);
        for (int i = 4; i < length; i++)
            pw[i] = PickRandom(all);

        // Fisher–Yates shuffle using a cryptographically secure source.
        for (int i = pw.Length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (pw[i], pw[j]) = (pw[j], pw[i]);
        }

        return new string(pw);
    }

    private static char PickRandom(string chars) => chars[RandomNumberGenerator.GetInt32(chars.Length)];
}

