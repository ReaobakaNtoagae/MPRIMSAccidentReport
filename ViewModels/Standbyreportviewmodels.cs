namespace CrashReport.ViewModels;

// ── Input — what the user selects ─────────────────────────────
public class StandbyReportRequest
{
    public DateOnly DateFrom { get; set; }
    public DateOnly DateTo { get; set; }
    public DateOnly? PriorYearFrom { get; set; }   // same period last year (optional)
    public DateOnly? PriorYearTo { get; set; }
}

// ── Individual fatal crash detail ────────────────────────────
public class FatalCrashDetail
{
    public string CrNo { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;   // HH:mm exact time
    public string? Route { get; set; }
    public string? Location { get; set; }
    public int Count { get; set; }
}

// ── District breakdown ────────────────────────────────────────
public class DistrictStats
{
    public string Name { get; set; } = string.Empty;
    public int Crashes { get; set; }
    public int Fatalities { get; set; }
    public int Serious { get; set; }
    public int Slight { get; set; }
    public int FatalTime1 { get; set; }   // 06:00–14:00
    public int FatalTime2 { get; set; }   // 14:00–22:00
    public int FatalTime3 { get; set; }   // 22:00–06:00
    public int FatalPedestrians { get; set; }
    // Full time detail for each fatal crash
    public List<FatalCrashDetail> FatalDetails { get; set; } = new();
}

// ── Route problem spot ────────────────────────────────────────
public class ProblematicRoute
{
    public string District { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public int Crashes { get; set; }
    public int Fatalities { get; set; }
    public string Locations { get; set; } = string.Empty;
}

// ── Valentine's / long-weekend sub-section ───────────────────
public class SubPeriodStats
{
    public string Label { get; set; } = string.Empty;
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public DistrictStats Province { get; set; } = new();
    public DistrictStats Ehlanzeni { get; set; } = new();
    public DistrictStats Bohlabelo { get; set; } = new();
    public DistrictStats GertSibande { get; set; } = new();
    public DistrictStats Nkangala { get; set; } = new();
}

// ── Victim demographics ───────────────────────────────────────
public class VictimDemographics
{
    public int TotalFatalities { get; set; }
    public int Age0to7 { get; set; }
    public int Age8to12 { get; set; }
    public int Age13to18 { get; set; }
    public int Age19to35 { get; set; }
    public int Age36Plus { get; set; }
    public int MaleTotal { get; set; }
    public int FemaleTotal { get; set; }
    public int MaleDriver { get; set; }
    public int FemaleDriver { get; set; }
    public int MalePassenger { get; set; }
    public int FemalePassenger { get; set; }
    public int MalePedestrian { get; set; }
    public int FemalePedestrian { get; set; }
    public int MaleCyclist { get; set; }
    public int FemaleCyclist { get; set; }
}

// ── Master report view model ──────────────────────────────────
public class StandbyReportViewModel
{
    public DateOnly DateFrom { get; set; }
    public DateOnly DateTo { get; set; }
    public string DayRange { get; set; } = string.Empty;  // e.g. "MONDAY TO SUNDAY"

    // ── Full week (current year vs prior year) ────────────────
    public DistrictStats CurrentProvince { get; set; } = new();
    public DistrictStats CurrentEhlanzeni { get; set; } = new();
    public DistrictStats CurrentBohlabelo { get; set; } = new();
    public DistrictStats CurrentGertSibande { get; set; } = new();
    public DistrictStats CurrentNkangala { get; set; } = new();

    public DistrictStats PriorProvince { get; set; } = new();
    public DistrictStats PriorEhlanzeni { get; set; } = new();
    public DistrictStats PriorBohlabelo { get; set; } = new();
    public DistrictStats PriorGertSibande { get; set; } = new();
    public DistrictStats PriorNkangala { get; set; } = new();

    // ── Problematic routes ────────────────────────────────────
    public List<ProblematicRoute> ProblematicRoutes { get; set; } = new();

    // ── Sub-period (e.g. Valentine's weekend) ────────────────
    public SubPeriodStats? SubPeriod { get; set; }

    // ── Victim demographics (sub-period or full week) ─────────
    public VictimDemographics Victims { get; set; } = new();
}