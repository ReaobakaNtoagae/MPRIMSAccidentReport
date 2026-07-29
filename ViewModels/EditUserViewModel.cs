using System.ComponentModel.DataAnnotations;

namespace CrashReport.Models;

public class EditUserViewModel
{
    public string Id { get; set; } = string.Empty;

    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;


    public string? District { get; set; }

    [Required]
    public string Role { get; set; } = string.Empty;

    public bool IsActive { get; set; }


    [DataType(DataType.Password)]
    public string? NewPassword { get; set; }
}