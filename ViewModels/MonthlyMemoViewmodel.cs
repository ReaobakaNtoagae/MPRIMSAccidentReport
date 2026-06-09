namespace CrashReport.ViewModels;

public class PeriodStatsBlock
{
    public int Crashes { get; set; }
    public int Fatalities { get; set; }
    public int Serious { get; set; }
    public int Slight { get; set; }
    public int FatalDrivers { get; set; }
    public int FatalPassengers { get; set; }
    public int FatalPedestrians { get; set; }
    public int FatalCyclists { get; set; }
    public int SeriousDrivers { get; set; }
    public int SeriousPassengers { get; set; }
    public int SeriousPedestrians { get; set; }
    public int SeriousCyclists { get; set; }
    public int SlightDrivers { get; set; }
    public int SlightPassengers { get; set; }
    public int SlightPedestrians { get; set; }
    public int SlightCyclists { get; set; }
}

public class ProvincialStats
{
    public PeriodStatsBlock Current { get; set; } = new();
    public PeriodStatsBlock Prior { get; set; } = new();
}

public class DistrictMemoStats
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PeriodStatsBlock Current { get; set; } = new();
    public PeriodStatsBlock Prior { get; set; } = new();
    public List<RouteStats> Routes { get; set; } = new();
}

public class RouteStats
{
    public string Route { get; set; } = string.Empty;
    public int CrashesPrev { get; set; }
    public int CrashesCurr { get; set; }
    public int FatalPrev { get; set; }
    public int FatalCurr { get; set; }
}

public class CrashTypeStats
{
    public string Type { get; set; } = string.Empty;
    public int CrashesPrev { get; set; }
    public int CrashesCurr { get; set; }
    public int FatalPrev { get; set; }
    public int FatalCurr { get; set; }
}

public class VehicleCategoryStats
{
    public string Category { get; set; } = string.Empty;
    public int CrashesPrev { get; set; }
    public int CrashesCurr { get; set; }
    public int FatalPrev { get; set; }
    public int FatalCurr { get; set; }
}

public class TimeSlotStats
{
    public string Slot { get; set; } = string.Empty;
    public int CrashesPrev { get; set; }
    public int CrashesCurr { get; set; }
    public int FatalPrev { get; set; }
    public int FatalCurr { get; set; }
}

public class DayStats
{
    public string Day { get; set; } = string.Empty;
    public int CrashesPrev { get; set; }
    public int CrashesCurr { get; set; }
    public int FatalPrev { get; set; }
    public int FatalCurr { get; set; }
}

public class YearHistory
{
    public int Year { get; set; }
    public int Crashes { get; set; }
    public int Fatalities { get; set; }
}

public class MonthlyMemoViewModel
{
    // ── Period labels (formatted for the document) ────────────
    public string MonthYear { get; set; } = string.Empty;
    public string MonthName { get; set; } = string.Empty;
    public string PeriodFrom { get; set; } = string.Empty;
    public string PeriodTo { get; set; } = string.Empty;
    public string PriorFrom { get; set; } = string.Empty;
    public string PriorTo { get; set; } = string.Empty;
    public int CurrentYear { get; set; }
    public int PriorYear { get; set; }
    public int DaysInPeriod { get; set; }

    // ── Memo header fields ────────────────────────────────────
    public string ReportDate { get; set; } = string.Empty;
    public string RefNumber { get; set; } = "16/9/4";
    public string EnquiryName { get; set; } = "M C Mdhluli";
    public string EnquiryTel { get; set; } = "082 802 6966";
    public string ToName { get; set; } = "MR P NGOMANE (MPL)";
    public string ToTitle { get; set; } = "MEMBER OF THE EXECUTIVE COUNCIL";
    public string FromName { get; set; } = "MR W MTHOMBOTHI";
    public string FromTitle { get; set; } = "HEAD OF DEPARTMENT";

    // ── Data ──────────────────────────────────────────────────
    public ProvincialStats Provincial { get; set; } = new();
    public List<YearHistory> FiveYearHistory { get; set; } = new();
    public List<DistrictMemoStats> Districts { get; set; } = new();
    public List<RouteStats> ProvincialRoutes { get; set; } = new();
    public List<CrashTypeStats> CrashTypes { get; set; } = new();
    public List<VehicleCategoryStats> VehicleCategories { get; set; } = new();
    public List<TimeSlotStats> TimeSlots { get; set; } = new();
    public Dictionary<string, List<DayStats>> DaysOfWeek { get; set; } = new();
}

// ── Form request ──────────────────────────────────────────────
public class MemoReportRequest
{
    public DateOnly DateFrom { get; set; }
    public DateOnly DateTo { get; set; }
    public DateOnly CompareFrom { get; set; }
    public DateOnly CompareTo { get; set; }
    public string ReportDate { get; set; } = string.Empty;
    public string RefNumber { get; set; } = "16/9/4";
    public string EnquiryName { get; set; } = "M C Mdhluli";
    public string EnquiryTel { get; set; } = "082 802 6966";
    public string ToName { get; set; } = "MR P NGOMANE (MPL)";
    public string ToTitle { get; set; } = "MEMBER OF THE EXECUTIVE COUNCIL";
    public string FromName { get; set; } = "MR W MTHOMBOTHI";
    public string FromTitle { get; set; } = "HEAD OF DEPARTMENT";
}