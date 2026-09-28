namespace CrashReport.Models.Dtos;

/// <summary>
/// Request/response shapes for Controllers/Api/ContributoryFactorsApiController.cs.
/// Fields match the [Bind(...)] allow-list on ContributoryFactorsController's MVC
/// Create action.
/// </summary>
public class ContributoryFactorCreateDto
{
    public int CrashId { get; set; }
    public string FactorCategory { get; set; } = "";
    public string FactorDescription { get; set; } = "";
    public bool IsMajorFactor { get; set; }
}

public class ContributoryFactorDto
{
    public int FactorId { get; set; }
    public int CrashId { get; set; }
    public string FactorCategory { get; set; } = "";
    public string FactorDescription { get; set; } = "";
    public bool IsMajorFactor { get; set; }
}
