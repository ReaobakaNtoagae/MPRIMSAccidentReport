using System.ComponentModel.DataAnnotations;

namespace CrashReport.Models.Dtos;

public class CreateRoleRequest
{
    [Required]
    public string RoleName { get; set; } = string.Empty;
}

public class SetPrivilegesRequest
{
    public string[] Privileges { get; set; } = Array.Empty<string>();
}
