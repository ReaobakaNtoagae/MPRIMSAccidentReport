using System;
using System.Collections.Generic;

namespace CrashReport.Models;

public partial class OfficialUse
{
    public int OfficialId { get; set; }

    public int CrashId { get; set; }

    public string? OfficeWhereOccurred { get; set; }

    public string? OccurrenceBookNo { get; set; }

    public string? AccidentRegisterNo { get; set; }

    public string? SapsCasNo { get; set; }

    public string? DepartmentNameOccurred { get; set; }

    public DateOnly? DateStamp { get; set; }

    public string? InspectedByInitials { get; set; }

    public string? InspectedByRank { get; set; }

    public string? InspectedBySurname { get; set; }

    public string? InspectedByServiceNumber { get; set; }

    public string? InspectedBySignature { get; set; }

    public string? OfficeWhereReported { get; set; }

    public string? DepartmentNameReported { get; set; }

    public string? CompletedBy { get; set; }

    public string? CompletedInitials { get; set; }

    public string? CompletedRank { get; set; }

    public string? CompletedSurname { get; set; }

    public string? CompletedServiceNumber { get; set; }

    public DateOnly? CompletedDate { get; set; }

    public TimeOnly? CompletedTime { get; set; }

    public string? CompletedSignature { get; set; }

    public string? CapturingNumber { get; set; }

    public string? Comments { get; set; }

    public virtual Crash Crash { get; set; } = null!;
}
