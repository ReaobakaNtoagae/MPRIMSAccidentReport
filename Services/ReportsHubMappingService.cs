using System.Security.Claims;
using CrashReport.Security;
using CrashReport.ViewModels;
using static CrashReport.ViewModels.FiveYearReportRequest;

namespace CrashReport.Services;

/// <summary>
/// Request-mapping, privilege-checking and preview-shaping logic for the Reports
/// Hub, moved out of ReportsHubController so the new ReportsHubApiController can
/// reuse it without duplicating the MVC controller's private methods. This mirrors
/// ReportsHubController's existing private CanUse/Validate/To*/Map* methods exactly
/// (same behaviour) — the MVC controller itself is left untouched and still has its
/// own copies, per the migration-prep instructions not to modify existing MVC
/// controllers.
/// </summary>
public interface IReportsHubMappingService
{
    bool CanUse(ClaimsPrincipal user, ReportsHubType type);
    string? Validate(ReportsHubRequest request);
    MemoReportRequest ToMonthly(ReportsHubRequest r);
    QuarterlyReportRequest ToQuarterly(ReportsHubRequest r);
    MemoReportRequest ToFixedPeriod(ReportsHubRequest r, int months);
    FiveYearReportRequest ToFiveYear(ReportsHubRequest r);
    ReportsHubPreview MapStandby(StandbyReportViewModel vm);
    ReportsHubPreview MapMonthly(MonthlyMemoViewModel vm);
    ReportsHubPreview MapFiveYear(FiveYearReportViewModel vm);
}

public sealed class ReportsHubMappingService : IReportsHubMappingService
{
    private bool Has(ClaimsPrincipal user, string privilege) =>
        user.HasClaim(Privileges.ClaimType, privilege);

    public bool CanUse(ClaimsPrincipal user, ReportsHubType type) => type switch
    {
        ReportsHubType.Monthly => Has(user, Privileges.Reports.Monthly),
        ReportsHubType.Quarterly => Has(user, Privileges.Reports.Quarterly),
        ReportsHubType.SixMonth => Has(user, Privileges.Reports.SixMonth),
        ReportsHubType.Annual => Has(user, Privileges.Reports.Annual),
        ReportsHubType.FiveYear => Has(user, Privileges.Reports.FiveYear),
        _ => Has(user, Privileges.Reports.Standby)
    };

    public string? Validate(ReportsHubRequest request) => request.ReportType switch
    {
        ReportsHubType.Standby or ReportsHubType.Monthly
            when request.DateFrom is null || request.DateTo is null || request.DateFrom > request.DateTo
            => "Enter a valid current reporting period.",
        ReportsHubType.Monthly
            when request.CompareFrom is null || request.CompareTo is null || request.CompareFrom > request.CompareTo
            => "Enter a valid comparison period.",
        ReportsHubType.Quarterly when request.Quarter is < 1 or > 4 || request.Year is null
            => "Select a valid quarter and year.",
        ReportsHubType.SixMonth or ReportsHubType.Annual
            when request.Year is < 2000 or > 2100
            => "Select a valid reporting year.",
        ReportsHubType.FiveYear when request.Month is < 1 or > 12 || request.EndYear is null
            => "Select a valid month and end year.",
        _ => null
    };

    public MemoReportRequest ToMonthly(ReportsHubRequest r) => new()
    {
        DateFrom = r.DateFrom!.Value,
        DateTo = r.DateTo!.Value,
        CompareFrom = r.CompareFrom!.Value,
        CompareTo = r.CompareTo!.Value,
        ReportDate = r.ReportDate,
        RefNumber = r.RefNumber,
        EnquiryName = r.EnquiryName,
        EnquiryTel = r.EnquiryTel,
        ToName = r.ToName,
        ToTitle = r.ToTitle,
        FromName = r.FromName,
        FromTitle = r.FromTitle
    };

    public QuarterlyReportRequest ToQuarterly(ReportsHubRequest r) => new()
    {
        Quarter = r.Quarter!.Value,
        Year = r.Year!.Value,
        ReportDate = r.ReportDate,
        RefNumber = r.RefNumber,
        EnquiryName = r.EnquiryName,
        EnquiryTel = r.EnquiryTel,
        ToName = r.ToName,
        ToTitle = r.ToTitle,
        FromName = r.FromName,
        FromTitle = r.FromTitle
    };

    public MemoReportRequest ToFixedPeriod(ReportsHubRequest r, int months)
    {
        var year = r.Year!.Value;
        var from = new DateOnly(year, 1, 1);
        var to = from.AddMonths(months).AddDays(-1);
        return new MemoReportRequest
        {
            DateFrom = from,
            DateTo = to,
            CompareFrom = from.AddYears(-1),
            CompareTo = to.AddYears(-1),
            ReportDate = r.ReportDate,
            RefNumber = r.RefNumber,
            EnquiryName = r.EnquiryName,
            EnquiryTel = r.EnquiryTel,
            ToName = r.ToName,
            ToTitle = r.ToTitle,
            FromName = r.FromName,
            FromTitle = r.FromTitle
        };
    }

    public FiveYearReportRequest ToFiveYear(ReportsHubRequest r) => new()
    {
        Month = r.Month!.Value,
        EndYear = r.EndYear!.Value,
        ReportDate = r.ReportDate,
        RefNumber = r.RefNumber,
        EnquiryName = r.EnquiryName,
        EnquiryTel = r.EnquiryTel,
        ToName = r.ToName,
        ToTitle = r.ToTitle,
        FromName = r.FromName,
        FromTitle = r.FromTitle
    };

    public ReportsHubPreview MapStandby(StandbyReportViewModel vm) => new()
    {
        Title = "Weekly Standby Report",
        PeriodLabel = $"{vm.DateFrom:dd MMM yyyy} – {vm.DateTo:dd MMM yyyy}",
        Metrics = Metrics(vm.CurrentProvince.Crashes, vm.CurrentProvince.Fatalities,
            vm.CurrentProvince.Serious, vm.CurrentProvince.Slight),
        Rows =
        [
            Row(vm.CurrentEhlanzeni), Row(vm.CurrentBohlabelo),
            Row(vm.CurrentGertSibande), Row(vm.CurrentNkangala)
        ]
    };

    public ReportsHubPreview MapMonthly(MonthlyMemoViewModel vm) => new()
    {
        Title = vm.MonthYear,
        PeriodLabel = $"{vm.PeriodFrom} – {vm.PeriodTo}",
        Metrics = Metrics(vm.Provincial.Current.Crashes, vm.Provincial.Current.Fatalities,
            vm.Provincial.Current.Serious, vm.Provincial.Current.Slight),
        Rows = vm.Districts.Select(d => new ReportsHubRow(d.Name, d.Current.Crashes,
            d.Current.Fatalities, d.Current.Serious, d.Current.Slight)).ToList()
    };

    public ReportsHubPreview MapFiveYear(FiveYearReportViewModel vm)
    {
        var province = vm.RegionSummaries.FirstOrDefault(r =>
            r.RegionName.Equals("PROVINCIAL", StringComparison.OrdinalIgnoreCase));
        int Total(string label) => province?.Stats.FirstOrDefault(s =>
            s.Label.Equals(label, StringComparison.OrdinalIgnoreCase))?.Total ?? 0;

        return new ReportsHubPreview
        {
            Title = vm.ReportTitle,
            PeriodLabel = $"{vm.MonthName} {vm.StartYear}–{vm.EndYear}",
            Caveat = vm.DemographicsHasGaps
                ? "Demographic data has gaps for one or more years and should be treated as indicative."
                : null,
            Metrics = Metrics(Total("CRASHES"), Total("FATALITIES"),
                Total("SERIOUS INJURIES"), Total("SLIGHT INJURIES")),
            Rows = vm.RegionSummaries.Where(r => !r.RegionName.Equals("PROVINCIAL", StringComparison.OrdinalIgnoreCase))
                .Select(r => new ReportsHubRow(r.RegionName,
                    Stat(r, "CRASHES"), Stat(r, "FATALITIES"),
                    Stat(r, "SERIOUS INJURIES"), Stat(r, "SLIGHT INJURIES"))).ToList()
        };
    }

    private static int Stat(RegionSummary region, string label) =>
        region.Stats.FirstOrDefault(s => s.Label.Equals(label, StringComparison.OrdinalIgnoreCase))?.Total ?? 0;

    private static ReportsHubRow Row(DistrictStats d) =>
        new(d.Name, d.Crashes, d.Fatalities, d.Serious, d.Slight);

    private static ReportsHubMetric[] Metrics(int crashes, int fatalities, int serious, int slight) =>
    [
        new("Crashes", crashes), new("Fatalities", fatalities),
        new("Serious injuries", serious), new("Slight injuries", slight)
    ];
}
