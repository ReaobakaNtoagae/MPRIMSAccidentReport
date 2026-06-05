using System;
using System.Collections.Generic;

namespace CrashReport.Models;

public partial class CrashVehicle
{
    public int CrashVehicleId { get; set; }

    public int CrashId { get; set; }

    public int VehicleId { get; set; }

    public int? DriverPersonId { get; set; }

    public string? VehicleType { get; set; }

    public string VehicleReference { get; set; } = null!;

    public string? SeatbeltUsed { get; set; }

    public string? AlcoholSuspected { get; set; }

    public string? AlcoholTestResult { get; set; }

    public string? DrugSuspected { get; set; }

    public string? DrugTestResult { get; set; }

    public string? VehicleManoeuvre { get; set; }

    public string? PositionBeforeCrash { get; set; }

    public string? PassengersForReward { get; set; }

    public string? BreakdownCompany { get; set; }

    public virtual Crash Crash { get; set; } = null!;

    public virtual ICollection<CrashPerson> CrashPeople { get; set; } = new List<CrashPerson>();

    public virtual Person? DriverPerson { get; set; }

    public virtual Vehicle Vehicle { get; set; } = null!;

    public virtual ICollection<VehicleDamage> VehicleDamages { get; set; } = new List<VehicleDamage>();
}
