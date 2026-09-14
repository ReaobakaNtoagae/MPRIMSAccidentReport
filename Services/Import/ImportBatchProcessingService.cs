using ClosedXML.Excel;
using CrashReport.Data;
using CrashReport.Models.Import.Models;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Services.Import;

public sealed class ImportBatchProcessingService(
    AppDbContext context,
    IWebHostEnvironment environment,
    IWorkbookTemplateDetector templateDetector,
    IWorkbookCrashRowParser rowParser,
    IWorkbookSummaryParser summaryParser,
    IStagingCrashQualityValidator qualityValidator,
    ILogger<ImportBatchProcessingService> logger) : IImportBatchProcessingService
{
    public async Task<ImportBatchProcessingResult> ProcessAsync(
        int importBatchId,
        CancellationToken cancellationToken = default)
    {
        // Load the batch with tracking because this operation changes its status and adds rows.
        var batch = await context.ImportBatches
            .Include(item => item.CrashRows)
                .ThenInclude(row => row.Issues)
            .Include(item => item.Issues)
            .SingleOrDefaultAsync(item => item.ImportBatchId == importBatchId, cancellationToken)
            ?? throw new KeyNotFoundException($"Import batch {importBatchId} was not found.");

        // Processing the same batch twice could duplicate staging rows. Completed staging is
        // therefore idempotent: return its current summary rather than parsing again.
        if (batch.CrashRows.Count > 0 && batch.Status is
            ImportBatchStatuses.RequiresReview or ImportBatchStatuses.ReadyForImport or
            ImportBatchStatuses.PartiallyCompleted or ImportBatchStatuses.Completed)
        {
            // Existing staged batches pre-dating registry duplicate checks can be safely
            // enriched when reopened. Completed batches remain immutable.
            if (batch.Status != ImportBatchStatuses.Completed &&
                await AddProductionDuplicateIssuesAsync(batch.CrashRows.ToArray(), cancellationToken) > 0)
            {
                batch.Status = ImportBatchStatuses.RequiresReview;
                batch.ReviewedByUserId = null;
                batch.ReviewedAt = null;
                await context.SaveChangesAsync(cancellationToken);
            }
            return Summarise(batch);
        }

        if (batch.Status is ImportBatchStatuses.Importing or ImportBatchStatuses.Completed or ImportBatchStatuses.Cancelled)
            throw new InvalidOperationException($"Batch {importBatchId} cannot be staged while its status is {batch.Status}.");

        var workbookPath = ResolveStoredWorkbook(batch.StoredFileReference);
        batch.Status = ImportBatchStatuses.Processing;
        batch.FailureReason = null;
        await context.SaveChangesAsync(cancellationToken);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            using var workbook = new XLWorkbook(workbookPath);
            var profile = templateDetector.Detect(workbook);
            var rows = rowParser.Parse(workbook, profile, batch.ImportBatchId,
                batch.ReportingMonth, batch.ReportingYear).ToList();

            if (rows.Count == 0)
                throw new ImportWorkbookIntakeException("The recognised worksheet did not contain any accident rows.");

            batch.DetectedTemplate = profile.TemplateCode;
            batch.TemplateDetectionConfidence = profile.Confidence;

            foreach (var row in rows)
            {
                var issues = qualityValidator.Validate(row, batch).ToList();
                ApplyValidationState(row, issues);
                row.Issues = issues;
                batch.CrashRows.Add(row);
            }

            // Summary blocks are optional. When present, preserve their demographics and
            // compare their reported totals with values recalculated from the detail rows.
            var summary = summaryParser.Parse(workbook, profile, batch, rows);
            if (summary.Demographics is not null)
                batch.Demographics.Add(summary.Demographics);
            foreach (var issue in summary.Issues)
                batch.Issues.Add(issue);

            // Duplicate checks compare the complete cleaned row. AR numbers can be reused
            // for different crashes, so an identifier match alone is not a duplicate.
            AddWithinBatchDuplicateIssues(rows);
            await AddProductionDuplicateIssuesAsync(rows, cancellationToken);

            // Staging and approval are deliberately separate decisions. Even a perfectly
            // clean workbook must be confirmed by a reviewer before production import.
            // Previously a clean batch skipped that audit step and became ready here.
            batch.Status = ImportBatchStatuses.RequiresReview;

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Import batch {BatchId} staged {Rows} rows using template {Template}; status {Status}.",
                batch.ImportBatchId, rows.Count, profile.TemplateCode, batch.Status);

            return Summarise(batch);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(cancellationToken);

            // Save a useful failure state outside the rolled-back transaction. The uploaded
            // file remains available for an administrator to diagnose or retry safely.
            context.ChangeTracker.Clear();
            var failedBatch = await context.ImportBatches.SingleAsync(
                item => item.ImportBatchId == importBatchId, cancellationToken);
            failedBatch.Status = ImportBatchStatuses.Failed;
            failedBatch.FailureReason = Limit(ex.Message, 1000);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogError(ex, "Staging failed for import batch {BatchId}.", importBatchId);
            throw;
        }
    }

    private string ResolveStoredWorkbook(string storedReference)
    {
        // StoredFileReference is database data, so treat it as untrusted. Resolving and
        // checking the prefix prevents a manipulated value from reading outside the app.
        var contentRoot = Path.GetFullPath(environment.ContentRootPath);
        var candidate = Path.GetFullPath(Path.Combine(contentRoot,
            storedReference.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = contentRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The stored workbook path is outside the application data directory.");
        if (!File.Exists(candidate))
            throw new FileNotFoundException("The stored workbook could not be found.", candidate);
        return candidate;
    }

    private static void ApplyValidationState(
        StagingCrashSummary row,
        IReadOnlyCollection<ImportDataQualityIssue> issues)
    {
        row.ValidationStatus = issues.Any(issue => issue.IsBlocking)
            ? ImportValidationStatuses.Error
            : issues.Any(issue => issue.Severity == ImportIssueSeverities.Warning)
                ? ImportValidationStatuses.Warning
                : ImportValidationStatuses.Valid;

        row.ReviewStatus = issues.Any(issue =>
            issue.ResolutionStatus == ImportIssueResolutionStatuses.Open)
            ? ImportReviewStatuses.AwaitingReviewer
            : ImportReviewStatuses.Approved;
        row.DuplicateStatus = ImportDuplicateStatuses.None;
    }

    private static void AddWithinBatchDuplicateIssues(IReadOnlyCollection<StagingCrashSummary> rows)
    {
        // A genuine duplicate repeats the complete cleaned source row. Looking at only an
        // AR number—or even only date/time/location—can merge two distinct crashes.
        var duplicateGroups = rows
            .Where(row => row.CrashDate.HasValue && !string.IsNullOrWhiteSpace(row.Station))
            .GroupBy(BuildRowFingerprint, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1);

        foreach (var group in duplicateGroups)
        {
            // The first source occurrence is treated as the canonical record. Only later
            // copies need a review decision; flagging both sides made the user resolve the
            // same duplicate twice and could leave a batch unnecessarily blocked.
            var ordered = group
                .OrderBy(row => row.WorksheetName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.SourceRowNumber)
                .ToArray();
            var retained = ordered[0];

            foreach (var row in ordered.Skip(1))
            {
                row.DuplicateStatus = ImportDuplicateStatuses.Possible;
                row.ReviewStatus = ImportReviewStatuses.AwaitingReviewer;
                row.ValidationStatus = row.ValidationStatus == ImportValidationStatuses.Error
                    ? ImportValidationStatuses.Error
                    : ImportValidationStatuses.Warning;
                row.Issues.Add(new ImportDataQualityIssue
                {
                    ImportBatchId = row.ImportBatchId,
                    StagingSummary = row,
                    FieldName = "Incident",
                    IssueCode = "POSSIBLE_DUPLICATE_IN_WORKBOOK",
                    Severity = ImportIssueSeverities.Warning,
                    IsBlocking = false,
                    Description = $"Every imported value matches {retained.WorksheetName} row {retained.SourceRowNumber}. The first occurrence is retained; confirm or reject this later copy.",
                    OriginalValue = $"{row.Station} | {row.ArNumber} | {row.CrashDate:yyyy-MM-dd} | {row.CrashTime:HH:mm} | {row.Route} | {row.Location} | {row.CrashType}",
                    ResolutionStatus = ImportIssueResolutionStatuses.Open,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
    }

    private static string BuildRowFingerprint(StagingCrashSummary row) => string.Join('|',
        ImportProductionIdentity.Normalise(row.Station),
        ImportProductionIdentity.Normalise(row.ArNumber),
        ImportProductionIdentity.Normalise(row.CasNumber),
        row.CrashDate?.ToString("yyyyMMdd") ?? string.Empty,
        row.CrashTime?.ToString("HHmm") ?? string.Empty,
        ImportProductionIdentity.Normalise(row.Route),
        ImportProductionIdentity.Normalise(row.Location),
        ImportProductionIdentity.Normalise(row.CrashType),
        ImportProductionIdentity.Normalise(row.VehiclesString),
        row.VehicleCount, row.FatalDrivers, row.FatalPassengers, row.FatalPedestrians,
        row.FatalCyclists, row.FatalMale, row.FatalFemale,
        row.SeriousDrivers, row.SeriousPassengers, row.SeriousPedestrians, row.SeriousCyclists,
        row.SlightDrivers, row.SlightPassengers, row.SlightPedestrians, row.SlightCyclists);

    private async Task<int> AddProductionDuplicateIssuesAsync(
        IReadOnlyCollection<StagingCrashSummary> rows,
        CancellationToken cancellationToken)
    {
        var datedRows = rows.Where(row => row.CrashDate.HasValue &&
            row.ImportStatus == ImportRecordStatuses.NotImported).ToArray();
        if (datedRows.Length == 0) return 0;
        var added = 0;

        var from = datedRows.Min(row => row.CrashDate!.Value);
        var to = datedRows.Max(row => row.CrashDate!.Value);
        var crashNumbers = datedRows
            .Where(row => !string.IsNullOrWhiteSpace(row.Station))
            .Select(ImportProductionIdentity.BuildCrashNumber)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var production = await context.CrashSummaries.AsNoTracking()
            // Limit the lookup by date or base identifier, then compare every imported
            // field below. A reused AR number by itself is not a duplicate.
            .Where(row => (row.CrashDate >= from && row.CrashDate <= to) ||
                          crashNumbers.Contains(row.CrNo))
            .ToArrayAsync(cancellationToken);

        foreach (var row in datedRows.Where(row => !string.IsNullOrWhiteSpace(row.Station)))
        {
            var crashNumber = ImportProductionIdentity.BuildCrashNumber(row);
            var incidentMatch = production.FirstOrDefault(item => IsSameProductionRecord(item, row));
            if (incidentMatch is null) continue;
            if (row.Issues.Any(issue => issue.IssueCode == "POSSIBLE_DUPLICATE_IN_REGISTRY"))
                continue;

            row.DuplicateStatus = ImportDuplicateStatuses.Possible;
            row.ReviewStatus = ImportReviewStatuses.AwaitingReviewer;
            if (row.ValidationStatus != ImportValidationStatuses.Error)
                row.ValidationStatus = ImportValidationStatuses.Warning;
            row.Issues.Add(DuplicateIssue(row, "POSSIBLE_DUPLICATE_IN_REGISTRY", false,
                $"Every imported value matches registry record {incidentMatch.CrNo}. Confirm whether this is a repeated submission.",
                incidentMatch.CrNo));
            added++;
        }
        return added;
    }

    private static bool IsSameProductionRecord(
        CrashReport.Models.CrashSummary existing,
        StagingCrashSummary row) =>
        ImportProductionIdentity.Normalise(existing.Station) == ImportProductionIdentity.Normalise(row.Station) &&
        string.Equals(existing.CasNo?.Trim(), row.CasNumber?.Trim(), StringComparison.OrdinalIgnoreCase) &&
        existing.CrashDate == row.CrashDate && existing.CrashTime == row.CrashTime &&
        ImportProductionIdentity.Normalise(existing.Route) == ImportProductionIdentity.Normalise(row.Route) &&
        ImportProductionIdentity.Normalise(existing.Location) == ImportProductionIdentity.Normalise(row.Location) &&
        ImportProductionIdentity.Normalise(existing.CrashType) == ImportProductionIdentity.Normalise(row.CrashType) &&
        ImportProductionIdentity.Normalise(existing.VehiclesString) == ImportProductionIdentity.Normalise(row.VehiclesString) &&
        existing.VehicleCount == (row.VehicleCount ?? 0) &&
        existing.FatalDrivers == (row.FatalDrivers ?? 0) && existing.FatalPassengers == (row.FatalPassengers ?? 0) &&
        existing.FatalPedestrians == (row.FatalPedestrians ?? 0) && existing.FatalCyclists == (row.FatalCyclists ?? 0) &&
        existing.FatalMale == (row.FatalMale ?? 0) && existing.FatalFemale == (row.FatalFemale ?? 0) &&
        existing.SeriousDrivers == (row.SeriousDrivers ?? 0) && existing.SeriousPassengers == (row.SeriousPassengers ?? 0) &&
        existing.SeriousPedestrians == (row.SeriousPedestrians ?? 0) && existing.SeriousCyclists == (row.SeriousCyclists ?? 0) &&
        existing.SlightDrivers == (row.SlightDrivers ?? 0) && existing.SlightPassengers == (row.SlightPassengers ?? 0) &&
        existing.SlightPedestrians == (row.SlightPedestrians ?? 0) && existing.SlightCyclists == (row.SlightCyclists ?? 0);

    private static ImportDataQualityIssue DuplicateIssue(
        StagingCrashSummary row, string code, bool blocking,
        string description, string originalValue) => new()
    {
        ImportBatchId = row.ImportBatchId,
        StagingSummary = row,
        FieldName = "Incident",
        IssueCode = code,
        Severity = blocking ? ImportIssueSeverities.Error : ImportIssueSeverities.Warning,
        IsBlocking = blocking,
        Description = description,
        OriginalValue = originalValue,
        ResolutionStatus = ImportIssueResolutionStatuses.Open,
        CreatedAt = DateTime.UtcNow
    };

    private static ImportBatchProcessingResult Summarise(ImportBatch batch)
    {
        var rows = batch.CrashRows;
        var issues = rows.SelectMany(row => row.Issues).Concat(batch.Issues).Distinct().ToArray();
        return new ImportBatchProcessingResult(batch.ImportBatchId, batch.Status,
            batch.DetectedTemplate ?? "Unknown", rows.Count,
            rows.Count(row => row.ValidationStatus == ImportValidationStatuses.Valid),
            rows.Count(row => row.ValidationStatus == ImportValidationStatuses.Warning),
            rows.Count(row => row.ValidationStatus == ImportValidationStatuses.Error),
            issues.Count(issue => issue.ResolutionStatus is ImportIssueResolutionStatuses.Open
                or ImportIssueResolutionStatuses.PendingDataOwner),
            issues.Count(issue => issue.IsBlocking && issue.ResolutionStatus == ImportIssueResolutionStatuses.Open));
    }

    private static string Limit(string value, int length) =>
        value.Length <= length ? value : value[..length];
}
