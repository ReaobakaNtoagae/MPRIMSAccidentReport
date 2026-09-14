namespace CrashReport.ViewModels;

public enum ReportsHubType
{
    Standby,
    Monthly,
    Quarterly,
    SixMonth,
    Annual,
    FiveYear
}

public sealed class ReportsHubRequest
{
    public ReportsHubType ReportType { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public DateOnly? CompareFrom { get; set; }
    public DateOnly? CompareTo { get; set; }
    public int? Quarter { get; set; }
    public int? Year { get; set; }
    public int? Month { get; set; }
    public int? EndYear { get; set; }
    public string ReportDate { get; set; } = string.Empty;
    public string RefNumber { get; set; } = "16/9/4";
    public string EnquiryName { get; set; } = "M C Mdhluli";
    public string EnquiryTel { get; set; } = "082 802 6966";
    public string ToName { get; set; } = "MR P NGOMANE (MPL)";
    public string ToTitle { get; set; } = "MEMBER OF THE EXECUTIVE COUNCIL";
    public string FromName { get; set; } = "MR W MTHOMBOTHI";
    public string FromTitle { get; set; } = "HEAD OF DEPARTMENT";
}

public sealed class ReportsHubIndexViewModel
{
    public bool CanStandby { get; init; }
    public bool CanMonthly { get; init; }
    public bool CanQuarterly { get; init; }
    public bool CanSixMonth { get; init; }
    public bool CanAnnual { get; init; }
    public bool CanFiveYear { get; init; }
}

public sealed record ReportsHubMetric(string Label, int Value);
public sealed record ReportsHubRow(string Region, int Crashes, int Fatalities, int Serious, int Slight);

public sealed class ReportsHubPreview
{
    public string Title { get; init; } = string.Empty;
    public string PeriodLabel { get; init; } = string.Empty;
    public string? Caveat { get; init; }
    public IReadOnlyList<ReportsHubMetric> Metrics { get; init; } = [];
    public IReadOnlyList<ReportsHubRow> Rows { get; init; } = [];
}
