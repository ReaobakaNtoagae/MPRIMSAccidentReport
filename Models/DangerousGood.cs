using System;
using System.Collections.Generic;

namespace CrashReport.Models;

public partial class DangerousGood
{
    public int DgId { get; set; }

    public int CrashId { get; set; }

    public string VehicleReference { get; set; } = null!;

    public string? GoodsCarried { get; set; }

    public string? SpillageObserved { get; set; }

    public string? VapourGasEmission { get; set; }

    public string? PlacardDisplayed { get; set; }

    public string? UnNumber { get; set; }

    public string? CompanyName { get; set; }

    public string? EmergencyServicesActivated { get; set; }

    public virtual Crash Crash { get; set; } = null!;
}
