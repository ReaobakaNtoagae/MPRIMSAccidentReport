using System;
using System.Collections.Generic;

namespace CrashReport.Models;

public partial class Person
{
    public int PersonId { get; set; }

    public string IdType { get; set; } = null!;

    public string? IdNumber { get; set; }

    public byte? Age { get; set; }

    public string Surname { get; set; } = null!;

    public string FullNames { get; set; } = null!;

    public string? CountryOfOrigin { get; set; }

    public string? Nationality { get; set; }

    public string? PopulationGroup { get; set; }

    public string? Gender { get; set; }

    public string? HomeAddress { get; set; }

    public string? CellPhone { get; set; }

    public string? OtherPhone { get; set; }

    public string? WorkContactAddress { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<CrashPerson> CrashPeople { get; set; } = new List<CrashPerson>();

    public virtual ICollection<CrashVehicle> CrashVehicles { get; set; } = new List<CrashVehicle>();

    public virtual ICollection<DriversLicence> DriversLicences { get; set; } = new List<DriversLicence>();
}
