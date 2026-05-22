using System;
using System.Collections.Generic;

namespace CrashReport.Models;

public partial class CrashWeather
{
    public int CrashId { get; set; }

    public string WeatherCondition { get; set; } = null!;

    public virtual Crash Crash { get; set; } = null!;
}
