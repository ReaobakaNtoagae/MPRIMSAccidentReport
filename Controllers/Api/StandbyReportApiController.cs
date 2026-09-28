using CrashReport.Security;
using CrashReport.Services;
using CrashReport.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrashReport.Controllers.Api;


[Route("api/reports/standby")]
[ApiController]
public class StandbyReportApiController : ControllerBase
{
    private readonly StandbyReportDataService _dataService;
    private readonly StandbyReportWordService _wordService;

    public StandbyReportApiController(
        StandbyReportDataService dataService,
        StandbyReportWordService wordService)
    {
        _dataService = dataService;
        _wordService = wordService;
    }

    // GET api/reports/standby/defaults - default current Mon-Sun week + prior year,
    [HttpGet("defaults")]
    [Authorize(Policy = Privileges.Reports.Standby)]
    public IActionResult Defaults()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        int daysUntilMonday = ((int)today.DayOfWeek == 0 ? 7 : (int)today.DayOfWeek) - 1;
        var weekStart = today.AddDays(-daysUntilMonday);
        var weekEnd = weekStart.AddDays(6);

        return Ok(new StandbyReportRequest
        {
            DateFrom = weekStart,
            DateTo = weekEnd,
            PriorYearFrom = weekStart.AddYears(-1),
            PriorYearTo = weekEnd.AddYears(-1),
        });
    }

    // POST api/reports/standby/preview
    [HttpPost("preview")]
    [Authorize(Policy = Privileges.Reports.Standby)]
    public async Task<IActionResult> Preview([FromBody] StandbyReportRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var vm = await _dataService.BuildAsync(
            request.DateFrom, request.DateTo,
            request.PriorYearFrom, request.PriorYearTo);

        return Ok(vm);
    }

    // GET api/reports/standby/export - downloads Word document using OpenXML
    [HttpGet("export")]
    [Authorize(Policy = Privileges.Reports.Standby)]
    public async Task<IActionResult> Export(
        DateOnly dateFrom,
        DateOnly dateTo,
        DateOnly? priorYearFrom = null,
        DateOnly? priorYearTo = null)
    {
        try
        {
            if (dateFrom > dateTo)
                return BadRequest("Start date must be before end date.");

            if (dateTo > DateOnly.FromDateTime(DateTime.Today))
                return BadRequest("Cannot generate report for future dates.");

            var vm = await _dataService.BuildAsync(dateFrom, dateTo, priorYearFrom, priorYearTo);
            var bytes = _wordService.Generate(vm);

            string fileName = $"Weekly_Standby_Report_{vm.DateFrom:yyyy-MM-dd}_to_{vm.DateTo:yyyy-MM-dd}.docx";

            return File(bytes,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                fileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error generating report: {ex.Message}");
        }
    }

    // GET api/reports/standby/export-html - kept for route parity with the MVC
    [HttpGet("export-html")]
    [Authorize(Policy = Privileges.Reports.Standby)]
    public async Task<IActionResult> ExportHtml(
        DateOnly dateFrom,
        DateOnly dateTo,
        DateOnly? priorYearFrom = null,
        DateOnly? priorYearTo = null) => await Export(dateFrom, dateTo, priorYearFrom, priorYearTo);
}
