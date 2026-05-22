using System;
using System.Collections.Generic;

namespace CrashReport.Models;

public partial class CrashSketch
{
    public int SketchId { get; set; }

    public int CrashId { get; set; }

    public string SketchType { get; set; } = null!;

    public string? FilePath { get; set; }

    public string? NorthDirection { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Crash Crash { get; set; } = null!;
}
