using DevExpress.XtraEditors.Controls;

namespace CrashReport.Models;

public sealed record CrashReportRow(
    DateOnly Date,
    TimeOnly? Time,
    string Station,
    string District,
    string Route,
    string CrashType,
    IReadOnlyList<string> VehicleCategories,
    int Fatalities,
    int Serious,
    int Slight,
    int FatalDrivers = 0,
    int FatalPassengers = 0,
    int FatalPedestrians = 0,
    int FatalCyclists = 0);

public sealed record PeriodTotals(
    int Crashes, int Fatalities, int Serious, int Slight,
    int FatalDrivers, int FatalPassengers, int FatalPedestrians, int FatalCyclists);

public sealed record ComparisonBreakdown(string Label, int CurrentCrashes, int PriorCrashes, int CurrentFatalities, int PriorFatalities);
public sealed record DistrictComparison(string Name, PeriodTotals Current, PeriodTotals Prior, IReadOnlyList<ComparisonBreakdown> Routes);
public sealed record YearHistory(int Year, int Crashes, int Fatalities);


public sealed class PeriodReportModel
{
    public required PeriodReportType ReportType { get; init; }
    public required string ReportTitle { get; init; }
    public required string PeriodLabel { get; init; }
    public required DateRange CurrentPeriod { get; init; }
    public required DateRange ComparisonPeriod { get; init; }
    public required PeriodTotals Current { get; init; }
    public required PeriodTotals Prior { get; init; }
    public required IReadOnlyList<DistrictComparison> Districts { get; init; }
    public required IReadOnlyList<ComparisonBreakdown> ProvincialRoutes { get; init; }
    public required IReadOnlyList<ComparisonBreakdown> CrashTypes { get; init; }
    public required IReadOnlyList<ComparisonBreakdown> VehicleCategories { get; init; }
    public required IReadOnlyList<ComparisonBreakdown> TimeSlots { get; init; }
    public required IReadOnlyList<ComparisonBreakdown> DaysOfWeek { get; init; }
    public required IReadOnlyList<YearHistory> FiveYearHistory { get; init; }
    public required PeriodReportRequest Document { get; init; }
}
