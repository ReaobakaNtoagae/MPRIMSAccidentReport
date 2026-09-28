using Microsoft.AspNetCore.Identity;

namespace CrashReport.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public string Station { get; set; } = string.Empty;   // e.g. NELSPRUIT
        public string District { get; set; } = string.Empty;   // e.g. EHLANZENI
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Set true for accounts seeded with a generated password (SeedData.cs) so
        // they can't do anything until they've picked their own. Checked fresh from
        // the database on every request by ForcePasswordChangeFilter — never trust
        // this as a claim on the auth cookie, since a claim baked in at sign-in
        // would stay stale for up to the full 8-hour sliding-expiration window
        // after the user actually changes their password.
        public bool MustChangePassword { get; set; }
    }

}
