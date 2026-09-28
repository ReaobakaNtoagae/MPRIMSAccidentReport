namespace CrashReport.Models.Dtos;

/// <summary>
/// Request/response shapes for Controllers/Api/WitnessesApiController.cs. Fields
/// match the [Bind(...)] allow-list on WitnessesController's MVC Create action.
/// </summary>
public class WitnessCreateDto
{
    public int CrashId { get; set; }
    public string SurnameInitials { get; set; } = "";
    public string? IdType { get; set; }
    public string? IdNumber { get; set; }
    public string? WorkContactAddress { get; set; }
    public string? CellPhone { get; set; }
    public string? OtherPhone { get; set; }
}

public class WitnessDto
{
    public int WitnessId { get; set; }
    public int CrashId { get; set; }
    public string SurnameInitials { get; set; } = "";
    public string? IdType { get; set; }
    public string? IdNumber { get; set; }
    public string? WorkContactAddress { get; set; }
    public string? CellPhone { get; set; }
    public string? OtherPhone { get; set; }
}
