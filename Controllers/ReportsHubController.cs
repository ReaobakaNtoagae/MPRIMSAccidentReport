using CrashReport.Security;
using CrashReport.Services;
using CrashReport.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static CrashReport.ViewModels.FiveYearReportRequest;

namespace CrashReport.Controllers;

[Authorize]
public sealed class ReportsHubController : Controller
{
    private readonly StandbyReportDataService _standbyData;
    private readonly StandbyReportWordService _standbyDoc;
    private readonly MonthlyMemoDataService _monthlyData;
    private readonly MonthlyMemoDocService _monthlyDoc;
    private readonly QuarterlyReportDataService _quarterlyData;
    private readonly FiveYearReportDataService _fiveYearData;
    private readonly FiveYearReportDocService _fiveYearDoc;

    public ReportsHubController(
        StandbyReportDataService standbyData,
        StandbyReportWordService standbyDoc,
        MonthlyMemoDataService monthlyData,
        MonthlyMemoDocService monthlyDoc,
        QuarterlyReportDataService quarterlyData,
        FiveYearReportDataService fiveYearData,
        FiveYearReportDocService fiveYearDoc)
    {
        _standbyData = standbyData;
        _standbyDoc = standbyDoc;
        _monthlyData = monthlyData;
        _monthlyDoc = monthlyDoc;
        _quarterlyData = quarterlyData;
        _fiveYearData = fiveYearData;
        _fiveYearDoc = fiveYearDoc;
    }

    [HttpGet]
    public IActionResult Index() => View(new ReportsHubIndexViewModel
    {
        CanStandby = Has(Privileges.Reports.Standby),
        CanMonthly = Has(Privileges.Reports.Monthly),
        CanQuarterly = Has(Privileges.Reports.Quarterly),
        CanSixMonth = Has(Privileges.Reports.SixMonth),
        CanAnnual = Has(Privileges.Reports.Annual),
        CanFiveYear = Has(Privileges.Reports.FiveYear)
    });

    [HttpPost]
    public async Task<IActionResult> Preview([FromBody] ReportsHubRequest? request)
    {
        if (request is null) return BadRequest(new { error = "The report request could not be read." });
        if (!CanUse(request.ReportType)) return Forbid();
        var error = Validate(request);
        if (error is not null) return BadRequest(new { error });

        return Json(request.ReportType switch
        {
            ReportsHubType.Monthly => MapMonthly(await _monthlyData.BuildAsync(ToMonthly(request))),
            ReportsHubType.Quarterly => MapMonthly(await _quarterlyData.BuildAsync(ToQuarterly(request))),
            ReportsHubType.SixMonth => MapMonthly(await _monthlyData.BuildAsync(ToFixedPeriod(request, 6))),
            ReportsHubType.Annual => MapMonthly(await _monthlyData.BuildAsync(ToFixedPeriod(request, 12))),
            ReportsHubType.FiveYear => MapFiveYear(await _fiveYearData.BuildAsync(ToFiveYear(request))),
            _ => MapStandby(await _standbyData.BuildAsync(
                request.DateFrom!.Value, request.DateTo!.Value, request.CompareFrom, request.CompareTo))
        });
    }

    [HttpPost]
    public async Task<IActionResult> Download([FromBody] ReportsHubRequest? request)
    {
        if (request is null) return BadRequest(new { error = "The report request could not be read." });
        if (!CanUse(request.ReportType)) return Forbid();
        var error = Validate(request);
        if (error is not null) return BadRequest(new { error });

        byte[] bytes;
        string fileName;
        switch (request.ReportType)
        {
            case ReportsHubType.Monthly:
                var monthly = await _monthlyData.BuildAsync(ToMonthly(request));
                bytes = await _monthlyDoc.GenerateAsync(monthly);
                fileName = $"Monthly_Memo_{request.DateFrom:MMMM_yyyy}.docx";
                break;
            case ReportsHubType.Quarterly:
                var quarterly = await _quarterlyData.BuildAsync(ToQuarterly(request));
                bytes = await _monthlyDoc.GenerateAsync(quarterly);
                fileName = $"Quarterly_Report_Q{request.Quarter}_{request.Year}.docx";
                break;
            case ReportsHubType.SixMonth:
                var sixMonth = await _monthlyData.BuildAsync(ToFixedPeriod(request, 6));
                bytes = await _monthlyDoc.GenerateAsync(sixMonth);
                fileName = $"Six_Month_Report_January_to_June_{request.Year}.docx";
                break;
            case ReportsHubType.Annual:
                var annual = await _monthlyData.BuildAsync(ToFixedPeriod(request, 12));
                bytes = await _monthlyDoc.GenerateAsync(annual);
                fileName = $"Annual_Report_{request.Year}.docx";
                break;
            case ReportsHubType.FiveYear:
                var fiveYear = await _fiveYearData.BuildAsync(ToFiveYear(request));
                bytes = await _fiveYearDoc.GenerateAsync(fiveYear);
                fileName = $"{fiveYear.MonthName}_ANALYSIS_{fiveYear.StartYear}-{fiveYear.EndYear}.docx";
                break;
            default:
                var standby = await _standbyData.BuildAsync(
                    request.DateFrom!.Value, request.DateTo!.Value, request.CompareFrom, request.CompareTo);
                bytes = _standbyDoc.Generate(standby);
                fileName = $"Weekly_Standby_Report_{request.DateFrom:yyyy-MM-dd}_to_{request.DateTo:yyyy-MM-dd}.docx";
                break;
        }

        return File(bytes,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            fileName);
    }

    private bool Has(string privilege) => User.HasClaim(Privileges.ClaimType, privilege);
    private bool CanUse(ReportsHubType type) => type switch
    {
        ReportsHubType.Monthly => Has(Privileges.Reports.Monthly),
        ReportsHubType.Quarterly => Has(Privileges.Reports.Quarterly),
        ReportsHubType.SixMonth => Has(Privileges.Reports.SixMonth),
        ReportsHubType.Annual => Has(Privileges.Reports.Annual),
        ReportsHubType.FiveYear => Has(Privileges.Reports.FiveYear),
        _ => Has(Privileges.Reports.Standby)
    };

    private static string? Validate(ReportsHubRequest request) => request.ReportType switch
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

    private static MemoReportRequest ToMonthly(ReportsHubRequest r) => new()
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

    private static QuarterlyReportRequest ToQuarterly(ReportsHubRequest r) => new()
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

    private static MemoReportRequest ToFixedPeriod(ReportsHubRequest r, int months)
    {
        // Six-month and annual reports use the established memo dataset and document
        // structure; only their date window changes. This avoids duplicating tested
        // aggregation logic for districts, routes, casualties and demographics.
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

    private static FiveYearReportRequest ToFiveYear(ReportsHubRequest r) => new()
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

    private static ReportsHubPreview MapStandby(StandbyReportViewModel vm) => new()
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

    private static ReportsHubPreview MapMonthly(MonthlyMemoViewModel vm) => new()
    {
        Title = vm.MonthYear,
        PeriodLabel = $"{vm.PeriodFrom} – {vm.PeriodTo}",
        Metrics = Metrics(vm.Provincial.Current.Crashes, vm.Provincial.Current.Fatalities,
            vm.Provincial.Current.Serious, vm.Provincial.Current.Slight),
        Rows = vm.Districts.Select(d => new ReportsHubRow(d.Name, d.Current.Crashes,
            d.Current.Fatalities, d.Current.Serious, d.Current.Slight)).ToList()
    };

    private static ReportsHubPreview MapFiveYear(FiveYearReportViewModel vm)
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
