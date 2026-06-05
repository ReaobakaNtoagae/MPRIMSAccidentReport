using CrashReport.Services;
using CrashReport.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace CrashReport.Controllers;

public class StandbyReportController : Controller
{
    private readonly StandbyReportDataService _dataService;
    private readonly StandbyReportWordService _wordService;

    public StandbyReportController(
        StandbyReportDataService dataService,
        StandbyReportWordService wordService)
    {
        _dataService = dataService;
        _wordService = wordService;
    }

    // GET: /StandbyReport - Shows the form
    [HttpGet("/StandbyReport")]
    public IActionResult Index()
    {
        // Default: current Mon–Sun week, prior year auto-calculated
        var today = DateOnly.FromDateTime(DateTime.Today);

        // Calculate Monday of current week (Monday = 1, Sunday = 7)
        int daysUntilMonday = ((int)today.DayOfWeek == 0 ? 7 : (int)today.DayOfWeek) - 1;
        var weekStart = today.AddDays(-daysUntilMonday);
        var weekEnd = weekStart.AddDays(6);

        var model = new StandbyReportRequest
        {
            DateFrom = weekStart,
            DateTo = weekEnd,
            PriorYearFrom = weekStart.AddYears(-1),
            PriorYearTo = weekEnd.AddYears(-1),
        };

        return View("Index", model);
    }

    // POST: /StandbyReport/Preview - Shows the report preview
    [HttpPost("/StandbyReport/Preview")]
    public async Task<IActionResult> Preview(StandbyReportRequest request)
    {
        if (!ModelState.IsValid)
            return View("Index", request);

        var vm = await _dataService.BuildAsync(
            request.DateFrom, request.DateTo,
            request.PriorYearFrom, request.PriorYearTo);

  
        return View("StandbyReport", vm);
    }

    // GET: /StandbyReport/Export - Downloads Word document using OpenXML
    [HttpGet("/StandbyReport/Export")]
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

    
    [HttpGet("/StandbyReport/ExportHtml")]
    public async Task<IActionResult> ExportHtml(
        DateOnly dateFrom,
        DateOnly dateTo,
        DateOnly? priorYearFrom = null,
        DateOnly? priorYearTo = null)
    {
        var vm = await _dataService.BuildAsync(dateFrom, dateTo, priorYearFrom, priorYearTo);

        // Return the view as HTML but with .docx extension
        Response.Headers.Append("Content-Disposition", $"attachment; filename=Weekly_Standby_Report_{vm.DateFrom:yyyy-MM-dd}_to_{vm.DateTo:yyyy-MM-dd}.docx");
        Response.ContentType = "application/msword";

        return View("SrandbyReport", vm);
    }


}