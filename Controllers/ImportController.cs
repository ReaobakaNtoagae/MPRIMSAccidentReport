using System.Security.Claims;
using CrashReport.Data;
using CrashReport.Security;
using CrashReport.Services.Import;
using CrashReport.ViewModels.Import;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Controllers;

// Every action requires the Excel-import privilege. Being signed in alone does
// not mean a user should be allowed to move data toward the production registry.
[Authorize(Policy = Privileges.Import.Excel)]
public sealed class ImportController : Controller
{
    private readonly AppDbContext _context;
    private readonly IImportWorkbookIntakeService _intakeService;
    private readonly IImportBatchProcessingService _processingService;
    private readonly IImportReviewService _reviewService;
    private readonly IImportCommitService _commitService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ImportController> _logger;

    public ImportController(
        AppDbContext context,
        IImportWorkbookIntakeService intakeService,
        IImportBatchProcessingService processingService,
        IImportReviewService reviewService,
        IImportCommitService commitService,
        IWebHostEnvironment environment,
        ILogger<ImportController> logger)
    {
        _context = context;
        _intakeService = intakeService;
        _processingService = processingService;
        _reviewService = reviewService;
        _commitService = commitService;
        _environment = environment;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> Verifications(CancellationToken cancellationToken)
    {
        // This queue is intentionally independent of one workbook, allowing staff to
        // follow up on referrals even after an otherwise safe batch has been imported.
        var issues = await _context.ImportDataQualityIssues.AsNoTracking()
            .Include(issue => issue.ImportBatch)
            .Include(issue => issue.StagingSummary)
            .Where(issue => issue.ResolutionStatus ==
                Models.Import.Models.ImportIssueResolutionStatuses.PendingDataOwner)
            .OrderBy(issue => issue.ResponseDueAt ?? DateTime.MaxValue)
            .ThenBy(issue => issue.ReferredAt)
            .ToArrayAsync(cancellationToken);

        return View(new DataVerificationViewModel
        {
            Items = issues.Select(DataVerificationItemViewModel.From).ToArray()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(
        IFormFile? file,
        string province,
        int reportingMonth,
        int reportingYear,
        string? notes,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            TempData["ImportError"] = "Please select an .xlsx workbook.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var userId = CurrentUserId();
            await using var content = file.OpenReadStream();

            // Intake validates and stores the workbook without changing production data.
            var intake = await _intakeService.IntakeAsync(new ImportWorkbookIntakeCommand(
                content, file.FileName, file.Length, province, reportingMonth,
                reportingYear, userId, notes), cancellationToken);

            // Processing detects the template, cleans rows and writes quality findings
            // to staging. Running it for an already-staged batch is safe and idempotent.
            await _processingService.ProcessAsync(intake.ImportBatchId, cancellationToken);

            TempData["ImportSuccess"] = intake.AlreadyExists
                ? "This workbook was already uploaded. Its existing staged batch has been opened."
                : "Workbook staged successfully. Review the rows before importing them.";

            return RedirectToAction(nameof(ReviewBatch), new { batchId = intake.ImportBatchId });
        }
        catch (ImportWorkbookIntakeException ex)
        {
            TempData["ImportError"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "The staged workbook upload failed.");
            TempData["ImportError"] = "The workbook could not be staged. No production records were changed.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public async Task<IActionResult> ReviewBatch(int batchId, CancellationToken cancellationToken)
    {
        var batch = await _context.ImportBatches.AsNoTracking()
            .Include(item => item.CrashRows)
                .ThenInclude(row => row.Issues)
            .Include(item => item.Issues)
            .SingleOrDefaultAsync(item => item.ImportBatchId == batchId, cancellationToken);

        return batch is null ? NotFound() : View(ImportBatchReviewViewModel.From(batch));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRow(UpdateImportRowViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["ImportError"] = "Check the required fields and their maximum lengths.";
            return RedirectToAction(nameof(ReviewBatch), new { batchId = model.BatchId });
        }

        // Map web input to a service command; the service remains independent of MVC.
        var command = new UpdateStagingRowCommand(model.StagingSummaryId, model.Station,
            model.ArNumber, model.CasNumber, model.CrashDate, model.CrashTime,
            model.Route, model.Location, model.CrashType, model.Notes);
        return await RunReviewActionAsync(model.BatchId,
            () => _reviewService.UpdateRowAsync(command, CurrentUserId(), cancellationToken),
            "The staged row was corrected and revalidated.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveIssue(
        long issueId, int batchId, string decision, string? notes,
        CancellationToken cancellationToken) =>
        await RunReviewActionAsync(batchId,
            () => _reviewService.ResolveIssueAsync(
                issueId, decision, notes, CurrentUserId(), cancellationToken),
            "The quality issue was updated.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReferIssue(long issueId, int batchId, string referredTo,
        string question, DateTime? responseDueAt, CancellationToken cancellationToken) =>
        await RunReviewActionAsync(batchId,
            () => _reviewService.ReferIssueAsync(
                new ReferImportIssueCommand(issueId, referredTo, question, responseDueAt),
                CurrentUserId(), cancellationToken),
            "The finding is now awaiting clarification from the data owner.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordDataOwnerResponse(long issueId, int batchId,
        string response, CancellationToken cancellationToken) =>
        await RunReviewActionAsync(batchId,
            () => _reviewService.RecordDataOwnerResponseAsync(
                new RecordDataOwnerResponseCommand(issueId, response),
                CurrentUserId(), cancellationToken),
            "The data-owner response was recorded. The finding is ready for your decision.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordVerificationResponse(long issueId, string response,
        CancellationToken cancellationToken)
    {
        int? batchId = await _context.ImportDataQualityIssues.AsNoTracking()
            .Where(issue => issue.IssueId == issueId)
            .Select(issue => (int?)issue.ImportBatchId)
            .SingleOrDefaultAsync(cancellationToken);
        try
        {
            await _reviewService.RecordDataOwnerResponseAsync(
                new RecordDataOwnerResponseCommand(issueId, response),
                CurrentUserId(), cancellationToken);
            TempData["ImportSuccess"] = "The response was recorded and returned to the reviewer for a final decision.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            TempData["ImportError"] = ex.Message;
        }

        return batchId.HasValue
            ? RedirectToAction(nameof(ReviewBatch), new { batchId = batchId.Value })
            : RedirectToAction(nameof(Verifications));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveRow(
        long stagingSummaryId, int batchId, string? notes,
        CancellationToken cancellationToken) =>
        await RunReviewActionAsync(batchId,
            () => _reviewService.ApproveRowAsync(
                stagingSummaryId, notes, CurrentUserId(), cancellationToken),
            "The staged row was approved.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectRow(
        long stagingSummaryId, int batchId, string? notes,
        CancellationToken cancellationToken) =>
        await RunReviewActionAsync(batchId,
            () => _reviewService.RejectRowAsync(
                stagingSummaryId, notes, CurrentUserId(), cancellationToken),
            "The staged row was rejected and will not be imported.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveBatch(int batchId, CancellationToken cancellationToken) =>
        await RunReviewActionAsync(batchId,
            () => _reviewService.ApproveBatchAsync(
                batchId, CurrentUserId(), cancellationToken),
            "The batch is ready to import.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CommitBatch(int batchId, CancellationToken cancellationToken)
    {
        try
        {
            // This is the only action that crosses the staging-to-production boundary.
            var result = await _commitService.CommitAsync(
                batchId, CurrentUserId(), cancellationToken);
            TempData["ImportSuccess"] =
                $"Imported {result.ImportedRows} crash rows; {result.RejectedRows} rejected rows stayed in staging.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            TempData["ImportError"] = ex.Message;
        }

        return RedirectToAction(nameof(ReviewBatch), new { batchId });
    }

    [HttpGet]
    public IActionResult DownloadTemplate()
    {
        var path = Path.Combine(_environment.WebRootPath, "templates", "Crash_Import_Template_Modified.xlsx");
        if (!System.IO.File.Exists(path))
            return NotFound("Template file is not available. Please contact the system administrator.");

        return PhysicalFile(path,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Crash_Import_Template_Modified.xlsx");
    }

    private string CurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("The signed-in user could not be identified.");

    private async Task<IActionResult> RunReviewActionAsync(
        int batchId, Func<Task> action, string successMessage)
    {
        try
        {
            await action();
            TempData["ImportSuccess"] = successMessage;
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or ArgumentOutOfRangeException)
        {
            // Expected review-rule failures are useful and safe to show to the reviewer.
            TempData["ImportError"] = ex.Message;
        }

        return RedirectToAction(nameof(ReviewBatch), new { batchId });
    }
}
