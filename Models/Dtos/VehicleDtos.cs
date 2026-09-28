namespace CrashReport.Models.Dtos;

/// <summary>
/// Request/response shapes for Controllers/Api/VehiclesApiController.cs. The
/// bindable fields on VehicleCreateDto/VehicleUpdateDto intentionally match the
/// [Bind(...)] allow-list on VehiclesController's MVC Create/Edit actions.
/// </summary>
public class VehicleCreateDto
{
    public string CountryOfRegistration { get; set; } = "";
    public string? LicenceDiscNumber { get; set; }
    public string? Colour { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? VinNumber { get; set; }
    public string? TrailerLicenceNumber { get; set; }
    public string? VehicleCategory { get; set; }
    public string? VehicleTypeCode { get; set; }
    public string? SpecialFunction { get; set; }
    public string? PrivateOrBusiness { get; set; }
    public string? LicenceTypeFitting { get; set; }
}

public class VehicleUpdateDto
{
    public string CountryOfRegistration { get; set; } = "";
    public string? LicenceDiscNumber { get; set; }
    public string? Colour { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? VinNumber { get; set; }
    public string? TrailerLicenceNumber { get; set; }
    public string? VehicleCategory { get; set; }
    public string? VehicleTypeCode { get; set; }
    public string? SpecialFunction { get; set; }
    public string? PrivateOrBusiness { get; set; }
    public string? LicenceTypeFitting { get; set; }
}

public class VehicleListItemDto
{
    public int VehicleId { get; set; }
    public string? LicenceDiscNumber { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? Colour { get; set; }
    public string? VehicleCategory { get; set; }
    public string? SpecialFunction { get; set; }
    public string? PrivateOrBusiness { get; set; }
    public string? VinNumber { get; set; }
    public int CrashCount { get; set; }
}

public class VehicleCrashInvolvementDto
{
    public int CrashId { get; set; }
    public string? CrNo { get; set; }
    public string VehicleReference { get; set; } = "";
}

public class VehicleDetailDto
{
    public int VehicleId { get; set; }
    public string CountryOfRegistration { get; set; } = "";
    public string? LicenceDiscNumber { get; set; }
    public string? Colour { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? VinNumber { get; set; }
    public string? TrailerLicenceNumber { get; set; }
    public string? VehicleCategory { get; set; }
    public string? VehicleTypeCode { get; set; }
    public string? SpecialFunction { get; set; }
    public string? PrivateOrBusiness { get; set; }
    public string? LicenceTypeFitting { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<VehicleCrashInvolvementDto> CrashInvolvements { get; set; } = new();
}
