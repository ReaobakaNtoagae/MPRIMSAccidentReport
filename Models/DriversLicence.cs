using System;
using System.Collections.Generic;

namespace CrashReport.Models;

public partial class DriversLicence
{
    public int LicenceId { get; set; }

    public int PersonId { get; set; }

    public string? LicenceType { get; set; }

    public string? LicenceNumber { get; set; }

    public string? LicenceCode { get; set; }

    public DateOnly? DateOfIssue { get; set; }

    public string? PrdpCode { get; set; }

    public virtual Person Person { get; set; } = null!;
}
