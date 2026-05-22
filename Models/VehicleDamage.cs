using System;
using System.Collections.Generic;

namespace CrashReport.Models;

public partial class VehicleDamage
{
    public int DamageId { get; set; }

    public int CrashVehicleId { get; set; }

    public string DamagePoint { get; set; } = null!;

    public virtual CrashVehicle CrashVehicle { get; set; } = null!;
}
