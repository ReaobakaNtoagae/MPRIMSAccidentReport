using CrashReport.Security;
using CrashReport.Services;
using CrashReport.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrashReport.Controllers
{
    public class QuarterlyReportController : Controller
    {
        private readonly QuarterlyReportDataService _data;
        private readonly MonthlyMemoDocService _doc;

        public QuarterlyReportController(
            QuarterlyReportDataService data,
            MonthlyMemoDocService doc)
        {
            _data = data;
            _doc = doc;
        }

        [Authorize(Policy = Privileges.Reports.Quarterly)]
        public IActionResult Index() => View();

       
        [HttpPost]
        public async Task<IActionResult> Preview([FromBody] QuarterlyReportRequest req)
        {
            if (req.Quarter is < 1 or > 4)
                return BadRequest(new { error = "Quarter must be between 1 and 4." });

            var vm = await _data.BuildAsync(req);
            return Json(vm);
        }

       
        [HttpPost]
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
}