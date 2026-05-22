using System;
using System.Collections.Generic;

namespace CrashReport.Models;

public partial class ContributoryFactor
{
    public int FactorId { get; set; }

    public int CrashId { get; set; }

    public string FactorCategory { get; set; } = null!;

    public string FactorDescription { get; set; } = null!;

    public bool IsMajorFactor { get; set; }

    public virtual Crash Crash { get; set; } = null!;
}
