using System;
using System.Collections.Generic;

namespace CrashReport.Models;

public partial class Vehicle
{
    public int VehicleId { get; set; }

    public string CountryOfRegistration { get; set; } = null!;

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

    public virtual ICollection<CrashVehicle> CrashVehicles { get; set; } = new List<CrashVehicle>();
}
