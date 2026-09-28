using System.Security.Claims;
using CrashReport.Data;
using CrashReport.Models.Dtos;
using CrashReport.Security;
using CrashReport.Services.Import;
using CrashReport.ViewModels.Import;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Controllers.Api;


[Route("api/import")]
[ApiController]
[Authorize(Policy = Privileges.Import.Excel)]
public sealed class ImportApiController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IImportWorkbookIntakeService _intakeService;
    private readonly IImportBatchProcessingService _processingService;
    private readonly IImportReviewService _reviewService;
    private readonly IImportCommitService _commitService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ImportApiController> _logger;

    public ImportApiController(
        AppDbContext context,
        IImportWorkbookIntakeService intakeService,
        IImportBatchProcessingService processingService,
        IImportReviewService reviewService,
        IImportCommitService commitService,
        IWebHostEnvironment environment,
        ILogger<ImportApiController> logger)
    {
        _context = context;
        _intakeService = intakeService;
        _processingService = processingService;
        _reviewService = reviewService;
        _commitService = commitService;
        _environment = environment;
        _logger = logger;
    }

    // GET api/import/verifications
    [HttpGet("verifications")]
    public async Task<IActionResult> Verifications(CancellationToken cancellationToken)
    {
        var issues = await _context.ImportDataQualityIssues.AsNoTracking()
            .Include(issue => issue.ImportBatch)
            .Include(issue => issue.StagingSummary)
            .Where(issue => issue.ResolutionStatus ==
                Models.Import.Models.ImportIssueResolutionStatuses.PendingDataOwner)
            .OrderBy(issue => issue.ResponseDueAt ?? DateTime.MaxValue)
            .ThenBy(issue => issue.ReferredAt)
            .ToArrayAsync(cancellationToken);

        return Ok(new DataVerificationViewModel
        {
            Items = issues.Select(DataVerificationItemViewModel.From).ToArray()
        });
    }

    // POST api/import/upload (multipart/form-data)
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile? file,
        [FromForm] string province,
        [FromForm] int reportingMonth,
        [FromForm] int reportingYear,
        [FromForm] string? notes,
        CancellationToken cancellationToken)
    {
        if (file is null)
            return BadRequest(new { error = "Please select an .xlsx workbook." });

        try
        {
            var userId = CurrentUserId();
            await using var content = file.OpenReadStream();

            var intake = await _intakeService.IntakeAsync(new ImportWorkbookIntakeCommand(
                content, file.FileName, file.Length, province, reportingMonth,
                reportingYear, userId, notes), cancellationToken);

            var processing = await _processingService.ProcessAsync(intake.ImportBatchId, cancellationToken);

            return Ok(new
            {
                message = intake.AlreadyExists
                    ? "This workbook was already uploaded. Its existing staged batch has been opened."
                    : "Workbook staged successfully. Review the rows before importing them.",
                intake,
                processing
            });
        }
        catch (ImportWorkbookIntakeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "The staged workbook upload failed.");
            return StatusCode(500, new { error = "The workbook could not be staged. No production records were changed." });
        }
    }

    // GET api/import/batches/{batchId}
    [HttpGet("batches/{batchId:int}")]
    public async Task<IActionResult> ReviewBatch(int batchId, CancellationToken cancellationToken)
    {
        var batch = await _context.ImportBatches.AsNoTracking()
            .Include(item => item.CrashRows)
                .ThenInclude(row => row.Issues)
            .Include(item => item.Issues)
            .SingleOrDefaultAsync(item => item.ImportBatchId == batchId, cancellationToken);

        return batch is null ? NotFound() : Ok(ImportBatchReviewViewModel.From(batch));
    }

    // PUT api/import/batches/{batchId}/rows/{stagingSummaryId}
    [HttpPut("batches/{batchId:int}/rows/{stagingSummaryId:long}")]
    public async Task<IActionResult> UpdateRow(int batchId, long stagingSummaryId,
        [FromBody] UpdateImportRowRequest model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var command = new UpdateStagingRowCommand(stagingSummaryId, model.Station,
            model.ArNumber, model.CasNumber, model.CrashDate, model.CrashTime,
            model.Route, model.Location, model.CrashType, model.Notes);
        return await RunReviewActionAsync(
            () => _reviewService.UpdateRowAsync(command, CurrentUserId(), cancellationToken),
            "The staged row was corrected and revalidated.");
    }

    // POST api/import/batches/{batchId}/issues/{issueId}/resolve
    [HttpPost("batches/{batchId:int}/issues/{issueId:long}/resolve")]
    public async Task<IActionResult> ResolveIssue(int batchId, long issueId,
        [FromBody] ResolveIssueRequest model, CancellationToken cancellationToken) =>
        await RunReviewActionAsync(
            () => _reviewService.ResolveIssueAsync(issueId, model.Decision, model.Notes, CurrentUserId(), cancellationToken),
            "The quality issue was updated.");

    // POST api/import/batches/{batchId}/issues/{issueId}/refer
    [HttpPost("batches/{batchId:int}/issues/{issueId:long}/refer")]
    public async Task<IActionResult> ReferIssue(int batchId, long issueId,
        [FromBody] ReferIssueRequest model, CancellationToken cancellationToken) =>
        await RunReviewActionAsync(
            () => _reviewService.ReferIssueAsync(
                new ReferImportIssueCommand(issueId, model.ReferredTo, model.Question, model.ResponseDueAt),
                CurrentUserId(), cancellationToken),
            "The finding is now awaiting clarification from the data owner.");

    // POST api/import/batches/{batchId}/issues/{issueId}/data-owner-response
    [HttpPost("batches/{batchId:int}/issues/{issueId:long}/data-owner-response")]
    public async Task<IActionResult> RecordDataOwnerResponse(int batchId, long issueId,
        [FromBody] DataOwnerResponseRequest model, CancellationToken cancellationToken) =>
        await RunReviewActionAsync(
            () => _reviewService.RecordDataOwnerResponseAsync(
                new RecordDataOwnerResponseCommand(issueId, model.Response),
                CurrentUserId(), cancellationToken),
            "The data-owner response was recorded. The finding is ready for your decision.");

    // POST api/import/issues/{issueId}/verification-response
    [HttpPost("issues/{issueId:long}/verification-response")]
    public async Task<IActionResult> RecordVerificationResponse(long issueId,
        [FromBody] DataOwnerResponseRequest model, CancellationToken cancellationToken)
    {
        int? batchId = await _context.ImportDataQualityIssues.AsNoTracking()
            .Where(issue => issue.IssueId == issueId)
            .Select(issue => (int?)issue.ImportBatchId)
            .SingleOrDefaultAsync(cancellationToken);

        try
        {
            await _reviewService.RecordDataOwnerResponseAsync(
                new RecordDataOwnerResponseCommand(issueId, model.Response),
                CurrentUserId(), cancellationToken);

            return Ok(new
            {
                message = "The response was recorded and returned to the reviewer for a final decision.",
                importBatchId = batchId
            });
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/import/batches/{batchId}/rows/{stagingSummaryId}/approve
    [HttpPost("batches/{batchId:int}/rows/{stagingSummaryId:long}/approve")]
    public async Task<IActionResult> ApproveRow(int batchId, long stagingSummaryId,
        [FromBody] RowDecisionRequest? model, CancellationToken cancellationToken) =>
        await RunReviewActionAsync(
            () => _reviewService.ApproveRowAsync(stagingSummaryId, model?.Notes, CurrentUserId(), cancellationToken),
            "The staged row was approved.");

    // POST api/import/batches/{batchId}/rows/{stagingSummaryId}/reject
    [HttpPost("batches/{batchId:int}/rows/{stagingSummaryId:long}/reject")]
    public async Task<IActionResult> RejectRow(int batchId, long stagingSummaryId,
        [FromBody] RowDecisionRequest? model, CancellationToken cancellationToken) =>
        await RunReviewActionAsync(
            () => _reviewService.RejectRowAsync(stagingSummaryId, model?.Notes, CurrentUserId(), cancellationToken),
            "The staged row was rejected and will not be imported.");

    // POST api/import/batches/{batchId}/approve
    [HttpPost("batches/{batchId:int}/approve")]
    public async Task<IActionResult> ApproveBatch(int batchId, CancellationToken cancellationToken) =>
        await RunReviewActionAsync(
            () => _reviewService.ApproveBatchAsync(batchId, CurrentUserId(), cancellationToken),
            "The batch is ready to import.");

    // POST api/import/batches/{batchId}/commit
    // The only action that crosses the staging-to-production boundary.
    [HttpPost("batches/{batchId:int}/commit")]
    public async Task<IActionResult> CommitBatch(int batchId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _commitService.CommitAsync(batchId, CurrentUserId(), cancellationToken);
            return Ok(result);
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET api/import/template
    [HttpGet("template")]
    public IActionResult DownloadTemplate()
    {
        var path = Path.Combine(_environment.WebRootPath, "templates", "Crash_Import_Template_Modified.xlsx");
        if (!System.IO.File.Exists(path))
            return NotFound(new { error = "Template file is not available. Please contact the system administrator." });

        return PhysicalFile(path,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Crash_Import_Template_Modified.xlsx");
    }

    private string CurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("The signed-in user could not be identified.");

    private async Task<IActionResult> RunReviewActionAsync(Func<Task> action, string successMessage)
    {
        try
        {
            await action();
            return Ok(new { message = successMessage });
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or ArgumentOutOfRangeException)
        {
            // Expected review-rule failures are useful and safe to show to the reviewer.
            return BadRequest(new { error = ex.Message });
        }
    }
}
