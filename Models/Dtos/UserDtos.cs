using System.ComponentModel.DataAnnotations;

namespace CrashReport.Models.Dtos;

public class CreateUserRequest
{
    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? District { get; set; }

    public string? Role { get; set; }

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class UpdateUserRequest
{
    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? District { get; set; }

    public string? Role { get; set; }

    public bool IsActive { get; set; }

    // Optional — only reset the password if provided, same as UsersController.Edit.
    [DataType(DataType.Password)]
    public string? NewPassword { get; set; }
}
