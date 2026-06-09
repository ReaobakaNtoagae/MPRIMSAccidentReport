using CrashReport.Services;
using CrashReport.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CrashReport.Controllers;

public class MemoReportController : Controller
{
    private readonly MonthlyMemoDataService _data;
    private readonly MonthlyMemoDocService _doc;

    public MemoReportController(MonthlyMemoDataService data, MonthlyMemoDocService doc)
    {
        _data = data;
        _doc = doc;
    }

  
    public IActionResult Index() => View();

   
    [HttpPost]
    public async Task<IActionResult> Preview([FromBody] MemoReportRequest req)
    {
        if (req.DateFrom > req.DateTo || req.CompareFrom > req.CompareTo)
            return BadRequest(new { error = "Invalid date range." });

        var vm = await _data.BuildAsync(req);
        return Json(vm);
    }

    
    [HttpPost]
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