using CrashReport.Security;
using CrashReport.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using static CrashReport.Services.ExcelImportService;

namespace CrashReport.Controllers;

public class ImportController : Controller
{
    private readonly ExcelImportService _importService;
    private readonly ILogger<ImportController> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly IMemoryCache _cache;

    // Pending imports awaiting a review decision expire after 30 minutes --
    // long enough for someone to actually review a large duplicate list,
    // short enough that an abandoned session doesn't sit in memory forever.
    private static readonly TimeSpan PendingImportExpiry = TimeSpan.FromMinutes(30);

    public ImportController(ExcelImportService importService, ILogger<ImportController> logger,
        IWebHostEnvironment env, IMemoryCache cache)
    {
        _importService = importService;
        _logger = logger;
        _env = env;
        _cache = cache;
    }

    [Authorize(Policy = Privileges.Import.Excel)]
    public IActionResult Index() => View();


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile file, string province = "MP")
    {
        _logger.LogInformation("Upload action hit — file: {File}, province: {Province}",
            file?.FileName ?? "NULL", province);

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
        var pending = await _importService.PreviewAsync(stream, file.FileName, province);

        // Nothing to review — behaves exactly like the old single-shot
        // import, no extra click forced on the common case.
        if (pending.DuplicateCandidates.Count == 0)
        {
            var result = await _importService.ConfirmImportAsync(pending, new List<int>());
            TempData["ImportResult"] = System.Text.Json.JsonSerializer.Serialize(result);
            return RedirectToAction(nameof(Result));
        }

        var sessionId = Guid.NewGuid();
        _cache.Set(sessionId, pending, PendingImportExpiry);

        return RedirectToAction(nameof(ReviewDuplicates), new { id = sessionId });
    }


    [HttpGet]
    public IActionResult ReviewDuplicates(Guid id)
    {
        if (!_cache.TryGetValue(id, out PendingImport? pending) || pending == null)
        {
            TempData["ImportError"] = "This review session has expired. Please upload the file again.";
            return RedirectToAction(nameof(Index));
        }

        var vm = new ReviewDuplicatesViewModel
        {
            ImportSessionId = id,
            FileName = pending.FileName,
            ReadyToImportCount = pending.ReadyToImport.Count,
            Candidates = pending.DuplicateCandidates.Select((c, i) => new DuplicateCandidateDisplay
            {
                Index = i,
                CrNo = c.OriginalCrNo,
                Station = c.Summary.Station,
                CrashDate = c.Summary.CrashDate.ToString("yyyy-MM-dd"),
                CrashType = c.Summary.CrashType,
                Location = c.Summary.Location,
                Reason = c.Reason
            }).ToList()
        };

        return View(vm);
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmImport(Guid importSessionId, List<int>? keep)
    {
        if (!_cache.TryGetValue(importSessionId, out PendingImport? pending) || pending == null)
        {
            TempData["ImportError"] = "This review session has expired. Please upload the file again.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _importService.ConfirmImportAsync(pending, keep ?? new List<int>());
        _cache.Remove(importSessionId);

        TempData["ImportResult"] = System.Text.Json.JsonSerializer.Serialize(result);
        return RedirectToAction(nameof(Result));
    }


    public IActionResult Result()
    {
        var json = TempData["ImportResult"]?.ToString();
        if (string.IsNullOrEmpty(json))
            return RedirectToAction(nameof(Index));

        var result = System.Text.Json.JsonSerializer.Deserialize<ImportResult>(json);
        return View(result);
    }

    [HttpGet]
    public IActionResult DownloadTemplate()
    {
        var path = Path.Combine(_env.WebRootPath, "templates", "Crash_Import_Template_Modified.xlsx");

        if (!System.IO.File.Exists(path))
            return NotFound("Template file is not available. Please contact the system administrator.");

        var bytes = System.IO.File.ReadAllBytes(path);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Crash_Import_Template_Modified.xlsx");
    }
}


// Small display-only view models for the review page -- deliberately not
// the full EF entities (those stay server-side in the cached PendingImport).
public class ReviewDuplicatesViewModel
{
    public Guid ImportSessionId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int ReadyToImportCount { get; set; }
    public List<DuplicateCandidateDisplay> Candidates { get; set; } = new();
}

public class DuplicateCandidateDisplay
{
    public int Index { get; set; }
    public string CrNo { get; set; } = string.Empty;
    public string Station { get; set; } = string.Empty;
    public string CrashDate { get; set; } = string.Empty;
    public string? CrashType { get; set; }
    public string? Location { get; set; }
    public string Reason { get; set; } = string.Empty;
}