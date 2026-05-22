using System;
using System.Collections.Generic;

namespace CrashReport.Models;

public partial class Witness
{
    public int WitnessId { get; set; }

    public int CrashId { get; set; }

    public string SurnameInitials { get; set; } = null!;

    public string? IdType { get; set; }

    public string? IdNumber { get; set; }

    public string? WorkContactAddress { get; set; }

    public string? CellPhone { get; set; }

    public string? OtherPhone { get; set; }

    public virtual Crash Crash { get; set; } = null!;
}
