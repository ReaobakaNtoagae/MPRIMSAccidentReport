namespace CrashReport.Models;

public enum PeriodReportType
{
    Monthly,
    Quarterly,
    SixMonths,
    NineMonths,
    Annual
}

public sealed class PeriodReportRequest
{
    public PeriodReportType ReportType { get; init; }
    public int Year { get; init; }
    public int? Month { get; init; }
    public int? Quarter { get; init; }
    public string ProvinceCode { get; init; } = "MP";
    public string ReportDate { get; init; } = string.Empty;
    public string RefNumber { get; init; } = "16/9/4";
    public string EnquiryName { get; init; } = "M C Mdhluli";
    public string EnquiryTel { get; init; } = "082 802 6966";
    public string ToName { get; init; } = "MR P NGOMANE (MPL)";
    public string ToTitle { get; init; } = "MEMBER OF THE EXECUTIVE COUNCIL";
    public string FromName { get; init; } = "MR W MTHOMBOTHI";
    public string FromTitle { get; init; } = "HEAD OF DEPARTMENT";
}

public sealed record DateRange(DateOnly From, DateOnly To)
{
    public int Days => To.DayNumber - From.DayNumber + 1;
}

public sealed record ResolvedReportPeriod(
    PeriodReportType Type,
    string Title,
    string PeriodLabel,
    DateRange Current,
    DateRange Comparison,
    IReadOnlyList<DateRange> FiveYearRanges);

