using CrashReport.Security;
using CrashReport.Services;
using CrashReport.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrashReport.Controllers.Api;


[Route("api/reports/quarterly")]
[ApiController]
public class QuarterlyReportApiController : ControllerBase
{
    private readonly QuarterlyReportDataService _data;
    private readonly MonthlyMemoDocService _doc;

    public QuarterlyReportApiController(
        QuarterlyReportDataService data,
        MonthlyMemoDocService doc)
    {
        _data = data;
        _doc = doc;
    }

    // POST api/reports/quarterly/preview
    [HttpPost("preview")]
    [Authorize(Policy = Privileges.Reports.Quarterly)]
    public async Task<IActionResult> Preview([FromBody] QuarterlyReportRequest req)
    {
        if (req.Quarter is < 1 or > 4)
            return BadRequest(new { error = "Quarter must be between 1 and 4." });

        var vm = await _data.BuildAsync(req);
        return Ok(vm);
    }

    // POST api/reports/quarterly/download
    [HttpPost("download")]
    [Authorize(Policy = Privileges.Reports.Quarterly)]
    public async Task<IActionResult> Download([FromBody] QuarterlyReportRequest req)
    {
        if (req.Quarter is < 1 or > 4)
            return BadRequest(new { error = "Quarter must be between 1 and 4." });

        var vm = await _data.BuildAsync(req);
        var bytes = await _doc.GenerateAsync(vm);

        return File(bytes,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            $"Quarterly_Report_Q{req.Quarter}_{req.Year}.docx");
    }
}
