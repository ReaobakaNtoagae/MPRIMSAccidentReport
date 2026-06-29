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
    }

}
