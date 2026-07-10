using CrashReport.Security;
using CrashReport.Services;
using CrashReport.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrashReport.Controllers;

public class FiveYearReportController : Controller
{
    private readonly FiveYearReportDataService _data;
    private readonly FiveYearReportDocService _doc;

    public FiveYearReportController(FiveYearReportDataService data, FiveYearReportDocService doc)
    {
        _data = data;
        _doc = doc;
    }

    [Authorize(Policy = Privileges.Reports.FiveYear)]
    public IActionResult Index() => View();

    [HttpPost]
    public async Task<IActionResult> Preview([FromBody] FiveYearReportRequest req)
    {
        if (req.Month is < 1 or > 12)
            return BadRequest(new { error = "Month must be between 1 and 12." });
        if (req.EndYear < 2000 || req.EndYear > DateTime.Today.Year + 1)
            return BadRequest(new { error = "End year is out of range." });

        var vm = await _data.BuildAsync(req);
        return Json(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Download([FromBody] FiveYearReportRequest req)
    {
        if (req.Month is < 1 or > 12)
            return BadRequest(new { error = "Month must be between 1 and 12." });
        if (req.EndYear < 2000 || req.EndYear > DateTime.Today.Year + 1)
            return BadRequest(new { error = "End year is out of range." });

        var vm = await _data.BuildAsync(req);
        var bytes = await _doc.GenerateAsync(vm);

        var monthName = new DateTime(2000, req.Month, 1).ToString("MMMM").ToUpper();
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            $"{monthName}_ANALYSIS_{vm.StartYear}-{vm.EndYear}.docx");
    }
}