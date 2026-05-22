using CrashReport.Services;
using Microsoft.AspNetCore.Mvc;

namespace CrashReport.Controllers;

public class ImportController : Controller
{
    private readonly ExcelImportService _importService;

    public ImportController(ExcelImportService importService)
    {
        _importService = importService;
    }

    // GET: /Import
    public IActionResult Index() => View();

    // POST: /Import/Upload
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile file, string province = "MP")
    {
        if (file == null || file.Length == 0)
        {
            TempData["ImportError"] = "Please select an Excel file to upload.";
            return RedirectToAction(nameof(Index));
        }

        var ext = Path.GetExtension(file.FileName).ToLower();
        if (ext != ".xlsx" && ext != ".xls")
        {
            TempData["ImportError"] = "Only .xlsx and .xls files are supported.";
            return RedirectToAction(nameof(Index));
        }

        using var stream = file.OpenReadStream();
        var result = await _importService.ImportAsync(stream, file.FileName, province);

        TempData["ImportResult"] = System.Text.Json.JsonSerializer.Serialize(result);
        return RedirectToAction(nameof(Result));
    }

    // GET: /Import/Result
    public IActionResult Result()
    {
        var json = TempData["ImportResult"]?.ToString();
        if (string.IsNullOrEmpty(json))
            return RedirectToAction(nameof(Index));

        var result = System.Text.Json.JsonSerializer.Deserialize<ImportResult>(json);
        return View(result);
    }
}