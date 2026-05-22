using CrashReport.Services;
using CrashReport.ViewModels;
using Microsoft.AspNetCore.Mvc;

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

    // GET /StandbyReport
    [HttpGet]
    public IActionResult Index()
    {
        // Default: current Mon–Sun week, prior year auto-calculated
        var today = DateOnly.FromDateTime(DateTime.Today);
        var weekStart = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
        if (weekStart > today) weekStart = weekStart.AddDays(-7);
        var weekEnd = weekStart.AddDays(6);

        var model = new StandbyReportRequest
        {
            DateFrom = weekStart,
            DateTo = weekEnd,
            PriorYearFrom = weekStart.AddYears(-1),
            PriorYearTo = weekEnd.AddYears(-1),
        };

        return View(model);
    }

    // POST /StandbyReport  — preview in browser
    [HttpPost]
    public async Task<IActionResult> Preview(StandbyReportRequest request)
    {
        if (!ModelState.IsValid)
            return View("Index", request);

        var vm = await _dataService.BuildAsync(
            request.DateFrom, request.DateTo,
            request.PriorYearFrom, request.PriorYearTo);

        return View("Report", vm);
    }

    // GET /StandbyReport/Export  — download .docx
    [HttpGet]
    public async Task<IActionResult> Export(
        DateOnly dateFrom, DateOnly dateTo,
        DateOnly? priorYearFrom, DateOnly? priorYearTo)
    {
        var vm = await _dataService.BuildAsync(dateFrom, dateTo, priorYearFrom, priorYearTo);
        var bytes = _wordService.Generate(vm);

        string fileName = $"StandbyReport_{dateFrom:yyyy-MM-dd}_to_{dateTo:yyyy-MM-dd}.docx";

        return File(bytes,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            fileName);
    }
}