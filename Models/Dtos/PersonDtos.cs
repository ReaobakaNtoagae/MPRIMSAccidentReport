namespace CrashReport.Models.Dtos;

/// <summary>
/// Request/response shapes for Controllers/Api/PersonsApiController.cs. The bindable
/// fields on PersonCreateDto/PersonUpdateDto intentionally match the [Bind(...)]
/// allow-list on PersonsController's MVC Create/Edit actions, so the API surface
/// can't be over-posted into columns the MVC form never exposed.
/// </summary>
public class PersonCreateDto
{
    public string IdType { get; set; } = "";
    public string? IdNumber { get; set; }
    public byte? Age { get; set; }
    public string Surname { get; set; } = "";
    public string FullNames { get; set; } = "";
    public string? CountryOfOrigin { get; set; }
    public string? Nationality { get; set; }
    public string? PopulationGroup { get; set; }
    public string? Gender { get; set; }
    public string? HomeAddress { get; set; }
    public string? CellPhone { get; set; }
    public string? OtherPhone { get; set; }
    public string? WorkContactAddress { get; set; }
}

public class PersonUpdateDto
{
    public string IdType { get; set; } = "";
    public string? IdNumber { get; set; }
    public byte? Age { get; set; }
    public string Surname { get; set; } = "";
    public string FullNames { get; set; } = "";
    public string? CountryOfOrigin { get; set; }
    public string? Nationality { get; set; }
    public string? PopulationGroup { get; set; }
    public string? Gender { get; set; }
    public string? HomeAddress { get; set; }
    public string? CellPhone { get; set; }
    public string? OtherPhone { get; set; }
    public string? WorkContactAddress { get; set; }
}

public class PersonListItemDto
{
    public int PersonId { get; set; }
    public string Surname { get; set; } = "";
    public string FullNames { get; set; } = "";
    public string? IdNumber { get; set; }
    public string IdType { get; set; } = "";
    public string? Gender { get; set; }
    public string? CellPhone { get; set; }
    public string? PopulationGroup { get; set; }
    public int CrashCount { get; set; }
}

public class PersonCrashInvolvementDto
{
    public int CrashId { get; set; }
    public string? CrNo { get; set; }
    public string Role { get; set; } = "";
    public string? SeverityOfInjury { get; set; }
}

public class PersonDetailDto
{
    public int PersonId { get; set; }
    public string IdType { get; set; } = "";
    public string? IdNumber { get; set; }
    public byte? Age { get; set; }
    public string Surname { get; set; } = "";
    public string FullNames { get; set; } = "";
    public string? CountryOfOrigin { get; set; }
    public string? Nationality { get; set; }
    public string? PopulationGroup { get; set; }
    public string? Gender { get; set; }
    public string? HomeAddress { get; set; }
    public string? CellPhone { get; set; }
    public string? OtherPhone { get; set; }
    public string? WorkContactAddress { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<PersonCrashInvolvementDto> CrashInvolvements { get; set; } = new();
    public List<DriversLicenceDto> DriversLicences { get; set; } = new();
}

public class DriversLicenceDto
{
    public int LicenceId { get; set; }
    public string? LicenceType { get; set; }
    public string? LicenceNumber { get; set; }
    public string? LicenceCode { get; set; }
    public DateOnly? DateOfIssue { get; set; }
    public string? PrdpCode { get; set; }
}
