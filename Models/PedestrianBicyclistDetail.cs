using System;
using System.Collections.Generic;

namespace CrashReport.Models;

public partial class PedestrianBicyclistDetail
{
    public int DetailId { get; set; }

    public int CrashPersonId { get; set; }

    public string? PositionOnRoad { get; set; }

    public string? LocationReCrossing { get; set; }

    public string? Manoeuvre { get; set; }

    public string? PedestrianAction { get; set; }

    public string? ClothingColour { get; set; }

    public virtual CrashPerson CrashPerson { get; set; } = null!;
}
