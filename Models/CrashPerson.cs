using System;
using System.Collections.Generic;

namespace CrashReport.Models;

public partial class CrashPerson
{
    public int CrashPersonId { get; set; }

    public int CrashId { get; set; }

    public int PersonId { get; set; }

    public int? CrashVehicleId { get; set; }

    public string Role { get; set; } = null!;

    public string? VehicleReference { get; set; }

    public string? PersonReference { get; set; }

    public byte? PassengerNumber { get; set; }

    public string? SeatingPosition { get; set; }

    public string? SeverityOfInjury { get; set; }

    public string? SeatbeltHelmetUsed { get; set; }

    public string? ChildRestraintUsed { get; set; }

    public string? LiquorDrugSuspected { get; set; }

    public string? LiquorDrugTestDone { get; set; }

    public string? AmbulanceServiceRef { get; set; }

    public string? Hospital { get; set; }

    public virtual Crash Crash { get; set; } = null!;

    public virtual CrashVehicle? CrashVehicle { get; set; }

    public virtual ICollection<PedestrianBicyclistDetail> PedestrianBicyclistDetails { get; set; } = new List<PedestrianBicyclistDetail>();

    public virtual Person Person { get; set; } = null!;
}
