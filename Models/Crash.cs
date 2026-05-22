using System;
using System.Collections.Generic;

namespace CrashReport.Models;

public partial class Crash
{
    public int CrashId { get; set; }

    public string? CasNo { get; set; }

    public string? CrNo { get; set; }

    public string? IncidentReportNo { get; set; }

    public string? CapturingNumber { get; set; }

    public DateOnly CrashDate { get; set; }

    public TimeOnly? CrashTime { get; set; }

    public byte? NoOfAppendices { get; set; }

    public byte? NoOfVehiclesInvolved { get; set; }

    public string? ProvinceCode { get; set; }

    public short? SpeedLimitKmh { get; set; }

    public string? RoadNumber { get; set; }

    public string? KmMarker { get; set; }

    public string? BriefDescription { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<ContributoryFactor> ContributoryFactors { get; set; } = new List<ContributoryFactor>();

    public virtual ICollection<CrashCondition> CrashConditions { get; set; } = new List<CrashCondition>();

    public virtual ICollection<CrashLocation> CrashLocations { get; set; } = new List<CrashLocation>();

    public virtual ICollection<CrashPerson> CrashPeople { get; set; } = new List<CrashPerson>();

    public virtual ICollection<CrashSketch> CrashSketches { get; set; } = new List<CrashSketch>();

    public virtual ICollection<CrashVehicle> CrashVehicles { get; set; } = new List<CrashVehicle>();

    public virtual ICollection<CrashWeather> CrashWeathers { get; set; } = new List<CrashWeather>();

    public virtual ICollection<DangerousGood> DangerousGoods { get; set; } = new List<DangerousGood>();

    public virtual ICollection<OfficialUse> OfficialUses { get; set; } = new List<OfficialUse>();

    public virtual ICollection<Witness> Witnesses { get; set; } = new List<Witness>();
}
