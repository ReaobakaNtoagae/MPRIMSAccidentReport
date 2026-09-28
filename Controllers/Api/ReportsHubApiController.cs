using CrashReport.Security;
using CrashReport.Services;
using CrashReport.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrashReport.Controllers.Api;


[Route("api/reports-hub")]
[ApiController]
[Authorize]
public sealed class ReportsHubApiController : ControllerBase
{
    private readonly StandbyReportDataService _standbyData;
    private readonly StandbyReportWordService _standbyDoc;
    private readonly MonthlyMemoDataService _monthlyData;
    private readonly MonthlyMemoDocService _monthlyDoc;
    private readonly QuarterlyReportDataService _quarterlyData;
    private readonly FiveYearReportDataService _fiveYearData;
    private readonly FiveYearReportDocService _fiveYearDoc;
    private readonly IReportsHubMappingService _map;

    public ReportsHubApiController(
        StandbyReportDataService standbyData,
        StandbyReportWordService standbyDoc,
        MonthlyMemoDataService monthlyData,
        MonthlyMemoDocService monthlyDoc,
        QuarterlyReportDataService quarterlyData,
        FiveYearReportDataService fiveYearData,
        FiveYearReportDocService fiveYearDoc,
        IReportsHubMappingService map)
    {
        _standbyData = standbyData;
        _standbyDoc = standbyDoc;
        _monthlyData = monthlyData;
        _monthlyDoc = monthlyDoc;
        _quarterlyData = quarterlyData;
        _fiveYearData = fiveYearData;
        _fiveYearDoc = fiveYearDoc;
        _map = map;
    }

    // GET api/reports-hub - which report types this user may use
    [HttpGet]
    public IActionResult Index() => Ok(new ReportsHubIndexViewModel
    {
        CanStandby = _map.CanUse(User, ReportsHubType.Standby),
        CanMonthly = _map.CanUse(User, ReportsHubType.Monthly),
        CanQuarterly = _map.CanUse(User, ReportsHubType.Quarterly),
        CanSixMonth = _map.CanUse(User, ReportsHubType.SixMonth),
        CanAnnual = _map.CanUse(User, ReportsHubType.Annual),
        CanFiveYear = _map.CanUse(User, ReportsHubType.FiveYear)
    });

    // POST api/reports-hub/preview
    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] ReportsHubRequest? request)
    {
        if (request is null) return BadRequest(new { error = "The report request could not be read." });
        if (!_map.CanUse(User, request.ReportType)) return Forbid();
        var error = _map.Validate(request);
        if (error is not null) return BadRequest(new { error });

        return Ok(request.ReportType switch
        {
            ReportsHubType.Monthly => _map.MapMonthly(await _monthlyData.BuildAsync(_map.ToMonthly(request))),
            ReportsHubType.Quarterly => _map.MapMonthly(await _quarterlyData.BuildAsync(_map.ToQuarterly(request))),
            ReportsHubType.SixMonth => _map.MapMonthly(await _monthlyData.BuildAsync(_map.ToFixedPeriod(request, 6))),
            ReportsHubType.Annual => _map.MapMonthly(await _monthlyData.BuildAsync(_map.ToFixedPeriod(request, 12))),
            ReportsHubType.FiveYear => _map.MapFiveYear(await _fiveYearData.BuildAsync(_map.ToFiveYear(request))),
            _ => _map.MapStandby(await _standbyData.BuildAsync(
                request.DateFrom!.Value, request.DateTo!.Value, request.CompareFrom, request.CompareTo))
        });
    }

    // POST api/reports-hub/download
    [HttpPost("download")]
    public async Task<IActionResult> Download([FromBody] ReportsHubRequest? request)
    {
        if (request is null) return BadRequest(new { error = "The report request could not be read." });
        if (!_map.CanUse(User, request.ReportType)) return Forbid();
        var error = _map.Validate(request);
        if (error is not null) return BadRequest(new { error });

        byte[] bytes;
        string fileName;
        switch (request.ReportType)
        {
            case ReportsHubType.Monthly:
                var monthly = await _monthlyData.BuildAsync(_map.ToMonthly(request));
                bytes = await _monthlyDoc.GenerateAsync(monthly);
                fileName = $"Monthly_Memo_{request.DateFrom:MMMM_yyyy}.docx";
                break;
            case ReportsHubType.Quarterly:
                var quarterly = await _quarterlyData.BuildAsync(_map.ToQuarterly(request));
                bytes = await _monthlyDoc.GenerateAsync(quarterly);
                fileName = $"Quarterly_Report_Q{request.Quarter}_{request.Year}.docx";
                break;
            case ReportsHubType.SixMonth:
                var sixMonth = await _monthlyData.BuildAsync(_map.ToFixedPeriod(request, 6));
                bytes = await _monthlyDoc.GenerateAsync(sixMonth);
                fileName = $"Six_Month_Report_January_to_June_{request.Year}.docx";
                break;
            case ReportsHubType.Annual:
                var annual = await _monthlyData.BuildAsync(_map.ToFixedPeriod(request, 12));
                bytes = await _monthlyDoc.GenerateAsync(annual);
                fileName = $"Annual_Report_{request.Year}.docx";
                break;
            case ReportsHubType.FiveYear:
                var fiveYear = await _fiveYearData.BuildAsync(_map.ToFiveYear(request));
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
}
