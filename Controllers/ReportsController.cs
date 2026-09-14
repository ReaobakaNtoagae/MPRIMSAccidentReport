using Microsoft.AspNetCore.Mvc;

namespace CrashReport.Controllers
{
    public sealed class ReportsController : Controller
    {

        public IActionResult Index()
        {
            return View();
        }
    }
}
