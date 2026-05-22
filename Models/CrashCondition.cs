using System;
using System.Collections.Generic;

namespace CrashReport.Models;

public partial class CrashCondition
{
    public int ConditionId { get; set; }

    public int CrashId { get; set; }

    public string? LightCondition { get; set; }

    public string? ObstructionType { get; set; }

    public string? TrafficControlType { get; set; }

    public string? RoadSignsCondition { get; set; }

    public string? RoadMarkingVisibility { get; set; }

    public string? OvertakingControl { get; set; }

    public string? RoadSegmentGrade { get; set; }

    public string? CrashType { get; set; }

    public bool? HitAndRun { get; set; }

    public string? TyreBurstObserved { get; set; }

    public string? VehicleLightsCondition { get; set; }

    public string? OtherObservations { get; set; }

    public virtual Crash Crash { get; set; } = null!;
}
