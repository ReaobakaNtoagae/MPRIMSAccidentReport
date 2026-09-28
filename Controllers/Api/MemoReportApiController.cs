using CrashReport.Security;
using CrashReport.Services;
using CrashReport.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrashReport.Controllers.Api;


[Route("api/reports/monthly")]
[ApiController]
public class MemoReportApiController : ControllerBase
{
    private readonly MonthlyMemoDataService _data;
    private readonly MonthlyMemoDocService _doc;

    public MemoReportApiController(MonthlyMemoDataService data, MonthlyMemoDocService doc)
    {
        _data = data;
        _doc = doc;
    }

    // POST api/reports/monthly/preview
    [HttpPost("preview")]
    [Authorize(Policy = Privileges.Reports.Monthly)]
    public async Task<IActionResult> Preview([FromBody] MemoReportRequest req)
    {
        if (req.DateFrom > req.DateTo || req.CompareFrom > req.CompareTo)
            return BadRequest(new { error = "Invalid date range." });

        var vm = await _data.BuildAsync(req);
        return Ok(vm);
    }

    // POST api/reports/monthly/download
    [HttpPost("download")]
    [Authorize(Policy = Privileges.Reports.Monthly)]
    public async Task<IActionResult> Download([FromBody] MemoReportRequest req)
    {
        if (req.DateFrom > req.DateTo || req.CompareFrom > req.CompareTo)
            return BadRequest(new { error = "Invalid date range." });

        var vm = await _data.BuildAsync(req);
        var bytes = await _doc.GenerateAsync(vm);

        var label = req.DateFrom.ToString("MMMM_yyyy").ToUpper();
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            $"Monthly_Memo_{label}.docx");
    }
}
