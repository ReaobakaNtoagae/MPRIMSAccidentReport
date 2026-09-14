using CrashReport.Data;
using CrashReport.Models.Import.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CrashReport.Services.Import;

public sealed class ImportReviewService(
    AppDbContext context,
    IStagingCrashQualityValidator qualityValidator,
    ILogger<ImportReviewService> logger) : IImportReviewService
{
    public async Task ReferIssueAsync(ReferImportIssueCommand command, string userId,
        CancellationToken cancellationToken = default)
    {
        var issue = await LoadIssueAsync(command.IssueId, cancellationToken);
        if (!IsOpen(issue))
            throw new InvalidOperationException("Only an unresolved finding can be referred.");

        issue.RequiresDataOwner = true;
        issue.ResolutionStatus = ImportIssueResolutionStatuses.PendingDataOwner;
        issue.ReferredTo = Required(command.ReferredTo, "Data owner", 200);
        issue.ReferralQuestion = Required(command.Question, "Question", 2000);
        issue.ResponseDueAt = command.ResponseDueAt;
        issue.ReferredByUserId = userId;
        issue.ReferredAt = DateTime.UtcNow;

        if (issue.StagingSummary is not null) RefreshRowState(issue.StagingSummary, userId);
        await RefreshBatchStateAsync(issue.ImportBatchId, cancellationToken);
        await SaveChangesAsync(cancellationToken);
        logger.LogInformation("User {UserId} referred issue {IssueId} to {DataOwner}.",
            userId, issue.IssueId, issue.ReferredTo);
    }

    public async Task RecordDataOwnerResponseAsync(RecordDataOwnerResponseCommand command,
        string userId, CancellationToken cancellationToken = default)
    {
        var issue = await LoadIssueAsync(command.IssueId, cancellationToken);
        if (issue.ResolutionStatus != ImportIssueResolutionStatuses.PendingDataOwner)
            throw new InvalidOperationException("This finding is not awaiting a data-owner response.");

        // A response supplies evidence; it does not silently decide the finding.
        issue.DataOwnerResponse = Required(command.Response, "Data-owner response", 2000);
        issue.RespondedAt = DateTime.UtcNow;
        issue.ResponseRecordedByUserId = userId;
        issue.ResolutionStatus = ImportIssueResolutionStatuses.Open;

        if (issue.StagingSummary is not null) RefreshRowState(issue.StagingSummary, userId);
        await RefreshBatchStateAsync(issue.ImportBatchId, cancellationToken);
        await SaveChangesAsync(cancellationToken);
        logger.LogInformation("User {UserId} recorded a response for issue {IssueId}.",
            userId, issue.IssueId);
    }

    public async Task UpdateRowAsync(UpdateStagingRowCommand command, string userId,
        CancellationToken cancellationToken = default)
    {
        var row = await context.StagingCrashSummaries
            .Include(item => item.ImportBatch)
            .Include(item => item.Issues)
            .SingleOrDefaultAsync(item => item.StagingSummaryId == command.StagingSummaryId, cancellationToken)
            ?? throw new KeyNotFoundException($"Staging row {command.StagingSummaryId} was not found.");

        if (row.ProductionSummaryId.HasValue || row.ImportStatus == ImportRecordStatuses.Imported)
            throw new InvalidOperationException("An imported row can no longer be edited in staging.");

        // Parse everything before changing the entity. If one input is invalid, the user
        // gets a useful message and EF has no half-edited row to save.
        var date = ParseDate(command.CrashDate);
        var time = ParseTime(command.CrashTime);
        if (date.Year != row.ImportBatch.ReportingYear || date.Month != row.ImportBatch.ReportingMonth)
            throw new InvalidOperationException($"The date must fall inside {row.ImportBatch.ReportingMonth:00}/{row.ImportBatch.ReportingYear}.");

        row.Station = Required(command.Station, "Station", 50);
        row.ArNumber = Optional(command.ArNumber, 50);
        row.CasNumber = Optional(command.CasNumber, 50);
        row.CrashDate = date;
        row.CalculatedDay = date.DayOfWeek.ToString();
        row.CrashTime = time;
        row.Route = Optional(command.Route, 20);
        row.Location = Required(command.Location, "Location", 150);
        row.CrashType = Optional(command.CrashType, 30);
        row.ReviewNotes = CleanNotes(command.Notes);

        // Re-run the same validator used during staging. Merely checking that a field is
        // non-empty is not enough evidence that an edited value satisfies its rule.
        ReconcileValidationIssues(row, qualityValidator.Validate(row, row.ImportBatch), userId,
            CleanNotes(command.Notes));
        await RefreshDuplicateIssuesAsync(row, userId, cancellationToken);

        RefreshRowState(row, userId);
        await RefreshBatchStateAsync(row.ImportBatchId, cancellationToken);
        await SaveChangesAsync(cancellationToken);
        logger.LogInformation("User {UserId} edited staging row {RowId}.", userId, row.StagingSummaryId);
    }
    public async Task ResolveIssueAsync(long issueId, string decision, string? notes,
        string userId, CancellationToken cancellationToken = default)
    {
        var issue = await context.ImportDataQualityIssues
            .Include(item => item.StagingSummary)
                .ThenInclude(row => row!.Issues)
            .Include(item => item.ImportBatch)
                .ThenInclude(batch => batch.Demographics)
            .Include(item => item.ImportBatch)
                .ThenInclude(batch => batch.Issues)
            .SingleOrDefaultAsync(item => item.IssueId == issueId, cancellationToken)
            ?? throw new KeyNotFoundException($"Quality issue {issueId} was not found.");

        if (issue.ResolutionStatus != ImportIssueResolutionStatuses.Open)
            throw new InvalidOperationException("This issue has already been resolved.");

        switch (decision)
        {
            case ImportIssueDecisions.ApplySuggestion:
                if (issue.StagingSummary is null)
                {
                    // Workbook reconciliation is a choice of authority, not an edit to
                    // the uploaded file. Production crash/casualty values are built from
                    // the detailed staged rows, so this decision records that their
                    // recalculated total was deliberately selected by the reviewer.
                    if (issue.IssueCode != "SUMMARY_TOTAL_MISMATCH" ||
                        string.IsNullOrWhiteSpace(issue.SuggestedValue))
                        throw new InvalidOperationException("This finding does not have a recalculated value that can be selected.");

                    notes = CleanNotes(notes) ??
                        $"Recalculated value {issue.SuggestedValue} selected instead of reported value {issue.OriginalValue}.";
                }
                else
                {
                    ApplySuggestedValue(issue);
                }
                issue.ResolutionStatus = ImportIssueResolutionStatuses.Corrected;
                break;

            case ImportIssueDecisions.Accept:
                // A blocking error cannot be accepted “as is”; doing so would defeat the
                // purpose of marking it blocking. It must be corrected or the row rejected.
                if (issue.IsBlocking)
                    throw new InvalidOperationException("A blocking issue must be corrected, or the row must be rejected.");
                if (issue.StagingSummary is null && issue.IssueCode == "SUMMARY_TOTAL_MISMATCH")
                    notes = CleanNotes(notes) ??
                        $"Reported value {issue.OriginalValue} retained instead of recalculated value {issue.SuggestedValue}.";
                issue.ResolutionStatus = ImportIssueResolutionStatuses.Accepted;
                break;

            case ImportIssueDecisions.Dismiss:
                // Dismiss means the reviewer believes this advisory finding is not applicable.
                // It is deliberately unavailable for blocking errors.
                if (issue.IsBlocking)
                    throw new InvalidOperationException("A blocking issue cannot be dismissed.");
                if (string.IsNullOrWhiteSpace(notes))
                    throw new InvalidOperationException("Explain why the finding is being dismissed.");
                issue.ResolutionStatus = ImportIssueResolutionStatuses.Dismissed;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(decision), "Unknown review decision.");
        }

        issue.ResolutionNotes = CleanNotes(notes);
        issue.ResolvedByUserId = userId;
        issue.ResolvedAt = DateTime.UtcNow;

        if (issue.FieldName?.StartsWith("Demographics.", StringComparison.Ordinal) == true)
        {
            // The demographic section is healthy once its last reconciliation finding
            // has a decision. Pending referrals and open findings keep it in Warning.
            var hasRemainingDemographicFinding = issue.ImportBatch.Issues.Any(other =>
                other.IssueId != issue.IssueId &&
                other.FieldName?.StartsWith("Demographics.", StringComparison.Ordinal) == true &&
                ImportWorkflowRules.IsUnresolved(other));
            foreach (var demographics in issue.ImportBatch.Demographics)
                demographics.ValidationStatus = hasRemainingDemographicFinding
                    ? ImportValidationStatuses.Warning
                    : ImportValidationStatuses.Valid;
        }

        if (issue.IssueCode.Contains("DUPLICATE", StringComparison.OrdinalIgnoreCase) &&
            issue.StagingSummary is not null)
        {
            // Resolving the duplicate warning means the reviewer chose to retain this row.
            // RejectRow is the separate choice when the row is actually the duplicate.
            issue.StagingSummary.DuplicateStatus = ImportDuplicateStatuses.ResolvedKeep;
        }

        if (issue.StagingSummary is not null)
            RefreshRowState(issue.StagingSummary, userId);
        await RefreshBatchStateAsync(issue.ImportBatchId, cancellationToken);
        await SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {UserId} resolved import issue {IssueId} as {Decision}.",
            userId, issueId, issue.ResolutionStatus);
    }

    public async Task ApproveRowAsync(long stagingSummaryId, string? notes, string userId,
        CancellationToken cancellationToken = default)
    {
        var row = await LoadRowAsync(stagingSummaryId, cancellationToken);
        EnsureRowCanBeChanged(row);
        var open = row.Issues.Where(BlocksApproval).ToArray();

        if (open.Any(issue => issue.IsBlocking))
            throw new InvalidOperationException("Resolve all blocking issues before approving this row.");
        if (open.Length > 0)
            throw new InvalidOperationException("Accept, correct or dismiss all open warnings before approving this row.");
        row.ReviewStatus = ImportReviewStatuses.Approved;
        row.ReviewNotes = CleanNotes(notes);
        row.ReviewedByUserId = userId;
        row.ReviewedAt = DateTime.UtcNow;

        await RefreshBatchStateAsync(row.ImportBatchId, cancellationToken);
        await SaveChangesAsync(cancellationToken);
        logger.LogInformation("User {UserId} approved staging row {RowId}.", userId, stagingSummaryId);
    }

    public async Task RejectRowAsync(long stagingSummaryId, string? notes, string userId,
        CancellationToken cancellationToken = default)
    {
        var row = await LoadRowAsync(stagingSummaryId, cancellationToken);
        EnsureRowCanBeChanged(row);
        row.ReviewStatus = ImportReviewStatuses.Rejected;
        row.DuplicateStatus = row.DuplicateStatus == ImportDuplicateStatuses.None
            ? ImportDuplicateStatuses.None
            : ImportDuplicateStatuses.ResolvedSkip;
        row.ReviewNotes = CleanNotes(notes) ?? "Rejected during data-quality review.";
        row.ReviewedByUserId = userId;
        row.ReviewedAt = DateTime.UtcNow;

        // Preserve every issue for audit, but close them because a rejected row will never
        // be committed. This keeps the batch from being permanently blocked by discarded data.
        foreach (var issue in row.Issues.Where(IsOpen))
        {
            issue.ResolutionStatus = ImportIssueResolutionStatuses.Dismissed;
            issue.ResolutionNotes = "Closed because the staging row was rejected.";
            issue.ResolvedByUserId = userId;
            issue.ResolvedAt = DateTime.UtcNow;
        }

        await RefreshBatchStateAsync(row.ImportBatchId, cancellationToken);
        await SaveChangesAsync(cancellationToken);
        logger.LogInformation("User {UserId} rejected staging row {RowId}.", userId, stagingSummaryId);
    }

    public async Task ApproveBatchAsync(int importBatchId, string userId,
        CancellationToken cancellationToken = default)
    {
        var batch = await context.ImportBatches
            .Include(item => item.CrashRows)
                .ThenInclude(row => row.Issues)
            .Include(item => item.Issues)
            .SingleOrDefaultAsync(item => item.ImportBatchId == importBatchId, cancellationToken)
            ?? throw new KeyNotFoundException($"Import batch {importBatchId} was not found.");

        var retainedRows = batch.CrashRows.Where(row => row.ReviewStatus != ImportReviewStatuses.Rejected).ToArray();
        // Older review attempts may contain the former Station + AR collision finding.
        // Reused AR numbers are valid when the full crash rows differ, so retire those
        // obsolete findings before recalculating row readiness.
        foreach (var obsolete in retainedRows.SelectMany(row => row.Issues).Where(issue =>
                     (issue.IssueCode == "IDENTIFIER_COLLISION_IN_WORKBOOK" ||
                      issue.IssueCode == "IDENTIFIER_COLLISION_IN_REGISTRY") && IsOpen(issue)))
        {
            obsolete.ResolutionStatus = ImportIssueResolutionStatuses.Corrected;
            obsolete.ResolutionNotes = "Closed after full-row duplicate comparison; distinct crashes may reuse an AR number.";
            obsolete.ResolvedByUserId = userId;
            obsolete.ResolvedAt = DateTime.UtcNow;
        }
        foreach (var row in retainedRows.Where(row =>
                     row.DuplicateStatus == ImportDuplicateStatuses.IdentifierCollision &&
                     !row.Issues.Any(issue => IsOpen(issue) &&
                         (issue.IssueCode.Contains("DUPLICATE", StringComparison.OrdinalIgnoreCase) ||
                          issue.IssueCode.Contains("COLLISION", StringComparison.OrdinalIgnoreCase)))))
            row.DuplicateStatus = ImportDuplicateStatuses.None;
        // Re-evaluate rows created under the earlier rule where every referral blocked.
        // This lets existing non-blocking referrals move forward without re-referral.
        foreach (var row in retainedRows) RefreshRowState(row, userId);
        if (retainedRows.Length == 0)
            throw new InvalidOperationException("A batch with no retained rows cannot be approved.");
        if (retainedRows.Any(row => !IsReviewedForBatchApproval(row)))
            throw new InvalidOperationException("Every retained row must be reviewed before approving the batch.");
        if (retainedRows.SelectMany(row => row.Issues).Any(BlocksApproval))
            throw new InvalidOperationException("The batch still contains unresolved quality issues.");
        if (batch.Issues.Any(BlocksApproval))
            throw new InvalidOperationException("Resolve the workbook summary differences before approving the batch.");
        if (!retainedRows.Any(row => row.ReviewStatus == ImportReviewStatuses.Approved &&
                                    row.ImportStatus == ImportRecordStatuses.NotImported))
            throw new InvalidOperationException("There are no newly approved rows to import. Deferred rows must wait for verification.");

        batch.Status = ImportBatchStatuses.ReadyForImport;
        batch.ReviewedByUserId = userId;
        batch.ReviewedAt = DateTime.UtcNow;
        await SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {UserId} approved import batch {BatchId} for commit.", userId, importBatchId);
    }

    private async Task<StagingCrashSummary> LoadRowAsync(long id, CancellationToken cancellationToken) =>
        await context.StagingCrashSummaries.Include(row => row.Issues)
            .SingleOrDefaultAsync(row => row.StagingSummaryId == id, cancellationToken)
        ?? throw new KeyNotFoundException($"Staging row {id} was not found.");

    private async Task<ImportDataQualityIssue> LoadIssueAsync(long id, CancellationToken cancellationToken) =>
        await context.ImportDataQualityIssues
            .Include(issue => issue.StagingSummary)
                .ThenInclude(row => row!.Issues)
            .SingleOrDefaultAsync(issue => issue.IssueId == id, cancellationToken)
        ?? throw new KeyNotFoundException($"Quality issue {id} was not found.");

    private static void ApplySuggestedValue(ImportDataQualityIssue issue)
    {
        if (string.IsNullOrWhiteSpace(issue.SuggestedValue))
            throw new InvalidOperationException("This issue does not have a suggested value.");

        var row = issue.StagingSummary!;
        switch (issue.FieldName)
        {
            // CalculatedDay is the trusted field used downstream; OriginalDay remains untouched
            // so an auditor can still see exactly what appeared in Excel.
            case "Day": row.CalculatedDay = issue.SuggestedValue; break;
            case "Station": row.Station = Limit(issue.SuggestedValue, 50); break;
            case "Route": row.Route = Limit(issue.SuggestedValue, 20); break;
            case "Location": row.Location = Limit(issue.SuggestedValue, 150); break;
            case "CrashType": row.CrashType = Limit(issue.SuggestedValue, 30); break;
            default:
                throw new InvalidOperationException($"Automatic correction is not supported for {issue.FieldName}.");
        }
    }

    private static void ReconcileValidationIssues(
        StagingCrashSummary row,
        IReadOnlyList<ImportDataQualityIssue> currentFindings,
        string userId,
        string? notes)
    {
        var duplicateCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "POSSIBLE_DUPLICATE_IN_WORKBOOK",
            "POSSIBLE_DUPLICATE_IN_REGISTRY",
            "IDENTIFIER_COLLISION_IN_WORKBOOK",
            "IDENTIFIER_COLLISION_IN_REGISTRY"
        };
        var findingsByKey = currentFindings.ToDictionary(
            issue => (issue.IssueCode, issue.FieldName ?? string.Empty),
            issue => issue,
            new IssueKeyComparer());

        foreach (var existing in row.Issues
                     .Where(issue => issue.ResolutionStatus == ImportIssueResolutionStatuses.Open)
                     .Where(issue => !duplicateCodes.Contains(issue.IssueCode)))
        {
            var key = (existing.IssueCode, existing.FieldName ?? string.Empty);
            var stillFails = findingsByKey.ContainsKey(key);

            // CalculatedDay is always regenerated from the edited date. The original Excel
            // weekday remains in the audit fields, so its historical mismatch need not stay open.
            if (stillFails && existing.IssueCode != "DAY_DATE_MISMATCH") continue;

            existing.ResolutionStatus = ImportIssueResolutionStatuses.Corrected;
            existing.ResolutionNotes = notes ?? "Corrected and revalidated in the staged-row editor.";
            existing.ResolvedByUserId = userId;
            existing.ResolvedAt = DateTime.UtcNow;
        }

        // Add genuinely new failures. A previously accepted finding is not recreated simply
        // because its source value remains visible for audit.
        foreach (var finding in currentFindings.Where(item => item.ResolutionStatus == ImportIssueResolutionStatuses.Open))
        {
            var alreadyRecorded = row.Issues.Any(existing =>
                existing.IssueCode.Equals(finding.IssueCode, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.FieldName, finding.FieldName, StringComparison.OrdinalIgnoreCase));
            if (!alreadyRecorded) row.Issues.Add(finding);
        }
    }

    private async Task RefreshDuplicateIssuesAsync(
        StagingCrashSummary row, string userId, CancellationToken cancellationToken)
    {
        if (!row.CrashDate.HasValue || !row.CrashTime.HasValue || string.IsNullOrWhiteSpace(row.Station))
            return;

        var crashNumber = ImportProductionIdentity.BuildCrashNumber(row);
        var date = row.CrashDate.Value;
        var production = await context.CrashSummaries.AsNoTracking()
            .Where(item => item.CrNo == crashNumber || item.CrashDate == date)
            .ToArrayAsync(cancellationToken);
        var siblingRows = await context.StagingCrashSummaries
            .Include(item => item.Issues)
            .Where(item => item.ImportBatchId == row.ImportBatchId &&
                           item.StagingSummaryId != row.StagingSummaryId &&
                           item.ReviewStatus != ImportReviewStatuses.Rejected)
            .ToArrayAsync(cancellationToken);

        var registryMatch = production.Any(item => IsSameProductionRecord(item, row));
        var batchRows = siblingRows.Append(row).ToArray();
        var matchingRows = batchRows
            .Where(item => IsSameImportedRecord(item, row))
            .OrderBy(item => item.WorksheetName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.SourceRowNumber)
            .ThenBy(item => item.StagingSummaryId)
            .ToArray();
        var firstOccurrence = matchingRows.FirstOrDefault();
        var workbookMatch = matchingRows.Length > 1 &&
            firstOccurrence?.StagingSummaryId != row.StagingSummaryId;
        // Retire findings created by the former identifier-only rule. A registry warning
        // now requires every imported value to match.
        ReconcileDuplicateFinding(row, "IDENTIFIER_COLLISION_IN_REGISTRY", false,
            true, "", crashNumber, userId);
        ReconcileDuplicateFinding(row, "POSSIBLE_DUPLICATE_IN_REGISTRY", registryMatch,
            false, "Every imported value matches an existing registry record.", crashNumber, userId);
        ReconcileDuplicateFinding(row, "POSSIBLE_DUPLICATE_IN_WORKBOOK", workbookMatch,
            false, "Another row in this workbook has the same station, date, time and location.",
            $"{row.Station} | {row.CrashDate:yyyy-MM-dd} | {row.CrashTime:HH:mm} | {row.Location}", userId);

        // Recalculate the complete duplicate group after an edit. Only occurrences after
        // the first are flagged; the canonical first row is cleared automatically.
        foreach (var sibling in siblingRows)
        {
            var siblingMatches = batchRows
                .Where(other => IsSameImportedRecord(other, sibling))
                .OrderBy(other => other.WorksheetName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(other => other.SourceRowNumber)
                .ThenBy(other => other.StagingSummaryId)
                .ToArray();
            var stillMatches = siblingMatches.Length > 1 &&
                siblingMatches[0].StagingSummaryId != sibling.StagingSummaryId;
            // Imported staging rows remain useful comparison evidence but are immutable.
            if (sibling.ImportStatus == ImportRecordStatuses.Imported) continue;
            ReconcileDuplicateFinding(sibling, "POSSIBLE_DUPLICATE_IN_WORKBOOK", stillMatches,
                false, "Another row in this workbook has the same station, date, time and location.",
                $"{sibling.Station} | {sibling.CrashDate:yyyy-MM-dd} | {sibling.CrashTime:HH:mm} | {sibling.Location}", userId);
            var siblingHasOpenDuplicate = sibling.Issues.Any(issue =>
                ImportWorkflowRules.IsUnresolved(issue) &&
                issue.IssueCode.Contains("DUPLICATE", StringComparison.OrdinalIgnoreCase));
            sibling.DuplicateStatus = siblingHasOpenDuplicate
                ? ImportDuplicateStatuses.Possible
                : ImportDuplicateStatuses.None;
            RefreshRowState(sibling, userId);
        }

        var openDuplicates = row.Issues.Where(ImportWorkflowRules.IsUnresolved)
            .Where(issue => issue.IssueCode.Contains("DUPLICATE", StringComparison.OrdinalIgnoreCase) ||
                            issue.IssueCode.Contains("COLLISION", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var duplicateStillApplies = registryMatch || workbookMatch;
        row.DuplicateStatus = !duplicateStillApplies
            ? ImportDuplicateStatuses.None
            : openDuplicates.Any(issue => issue.IssueCode.Contains("COLLISION"))
            ? ImportDuplicateStatuses.IdentifierCollision
            : openDuplicates.Length > 0
                ? ImportDuplicateStatuses.Possible
                : row.Issues.Any(issue => issue.ResolutionStatus is ImportIssueResolutionStatuses.Accepted
                    or ImportIssueResolutionStatuses.Dismissed &&
                    issue.IssueCode.Contains("DUPLICATE", StringComparison.OrdinalIgnoreCase))
                    ? ImportDuplicateStatuses.ResolvedKeep
                    : ImportDuplicateStatuses.None;
    }

    private static void ReconcileDuplicateFinding(
        StagingCrashSummary row, string code, bool applies, bool blocking,
        string description, string originalValue, string userId)
    {
        var unresolved = row.Issues.FirstOrDefault(issue =>
            issue.IssueCode == code && ImportWorkflowRules.IsUnresolved(issue));
        if (!applies && unresolved is not null)
        {
            unresolved.ResolutionStatus = ImportIssueResolutionStatuses.Corrected;
            unresolved.ResolutionNotes = "The identifying fields changed and the duplicate check was rerun.";
            unresolved.ResolvedByUserId = userId;
            unresolved.ResolvedAt = DateTime.UtcNow;
            return;
        }
        var sameFindingAlreadyAudited = row.Issues.Any(issue =>
            issue.IssueCode == code &&
            string.Equals(issue.OriginalValue, originalValue, StringComparison.OrdinalIgnoreCase));
        if (!applies || unresolved is not null || sameFindingAlreadyAudited) return;

        row.Issues.Add(new ImportDataQualityIssue
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
        });
    }

    // Compare every cleaned column that crosses the staging boundary. Workflow fields,
    // source-row metadata and audit copies are intentionally excluded.
    private static bool IsSameImportedRecord(StagingCrashSummary left, StagingCrashSummary right) =>
        ImportProductionIdentity.Normalise(left.Station) == ImportProductionIdentity.Normalise(right.Station) &&
        ImportProductionIdentity.Normalise(left.ArNumber) == ImportProductionIdentity.Normalise(right.ArNumber) &&
        ImportProductionIdentity.Normalise(left.CasNumber) == ImportProductionIdentity.Normalise(right.CasNumber) &&
        left.CrashDate == right.CrashDate && left.CrashTime == right.CrashTime &&
        ImportProductionIdentity.Normalise(left.Route) == ImportProductionIdentity.Normalise(right.Route) &&
        ImportProductionIdentity.Normalise(left.Location) == ImportProductionIdentity.Normalise(right.Location) &&
        ImportProductionIdentity.Normalise(left.CrashType) == ImportProductionIdentity.Normalise(right.CrashType) &&
        ImportProductionIdentity.Normalise(left.VehiclesString) == ImportProductionIdentity.Normalise(right.VehiclesString) &&
        left.VehicleCount == right.VehicleCount &&
        left.FatalDrivers == right.FatalDrivers && left.FatalPassengers == right.FatalPassengers &&
        left.FatalPedestrians == right.FatalPedestrians && left.FatalCyclists == right.FatalCyclists &&
        left.FatalMale == right.FatalMale && left.FatalFemale == right.FatalFemale &&
        left.SeriousDrivers == right.SeriousDrivers && left.SeriousPassengers == right.SeriousPassengers &&
        left.SeriousPedestrians == right.SeriousPedestrians && left.SeriousCyclists == right.SeriousCyclists &&
        left.SlightDrivers == right.SlightDrivers && left.SlightPassengers == right.SlightPassengers &&
        left.SlightPedestrians == right.SlightPedestrians && left.SlightCyclists == right.SlightCyclists;

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

    private sealed class IssueKeyComparer : IEqualityComparer<(string Code, string Field)>
    {
        public bool Equals((string Code, string Field) x, (string Code, string Field) y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.Code, y.Code) &&
            StringComparer.OrdinalIgnoreCase.Equals(x.Field, y.Field);
        public int GetHashCode((string Code, string Field) value) =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(value.Code),
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.Field));
    }

    private static void RefreshRowState(StagingCrashSummary row, string userId)
    {
        if (row.ReviewStatus == ImportReviewStatuses.Rejected) return;
        var open = row.Issues.Where(IsOpen).ToArray();
        row.ValidationStatus = open.Any(issue => issue.IsBlocking)
            ? ImportValidationStatuses.Error
            : open.Any()
                ? ImportValidationStatuses.Warning
                : ImportValidationStatuses.Valid;
        var approvalBlockers = open.Where(BlocksApproval).ToArray();
        if (approvalBlockers.Any())
        {
            row.ReviewStatus = ImportReviewStatuses.AwaitingReviewer;
            return;
        }

        // Referred findings stay visible in the verification queue, but do not freeze
        // the whole monthly import while staff wait for an external response.
        if (open.Any(issue => issue.ResolutionStatus == ImportIssueResolutionStatuses.PendingDataOwner))
        {
            row.ReviewStatus = ImportReviewStatuses.AwaitingDataOwner;
            return;
        }

        // Resolving the final issue is the review decision. Requiring another
        // "Approve row" click adds work without adding another quality control.
        row.ReviewStatus = ImportReviewStatuses.Approved;
        row.ReviewedByUserId = userId;
        row.ReviewedAt = DateTime.UtcNow;
    }

    private async Task RefreshBatchStateAsync(int batchId, CancellationToken cancellationToken)
    {
        var batch = await context.ImportBatches
            .Include(item => item.CrashRows)
                .ThenInclude(row => row.Issues)
            .Include(item => item.Issues)
            .SingleAsync(item => item.ImportBatchId == batchId, cancellationToken);

        // A fully completed batch has no deferred work left, so it must never move
        // backwards if historical audit information is updated.
        if (batch.Status == ImportBatchStatuses.Completed) return;

        var hasUnresolvedFinding = batch.Issues.Any(IsOpen) ||
            batch.CrashRows.SelectMany(row => row.Issues).Any(IsOpen);
        var hasOutstandingRow = batch.CrashRows.Any(row =>
            row.ImportStatus == ImportRecordStatuses.NotImported &&
            row.ReviewStatus != ImportReviewStatuses.Rejected);
        var hasImportedRow = batch.CrashRows.Any(row =>
            row.ImportStatus == ImportRecordStatuses.Imported);

        // Resolving the last deferred finding may finish a previously partial batch
        // without another database import when every row is already imported or rejected.
        if (hasImportedRow && !hasOutstandingRow && !hasUnresolvedFinding)
        {
            batch.Status = ImportBatchStatuses.Completed;
            return;
        }

        // Any review change invalidates the previous sign-off. Readiness is granted only
        // by ApproveBatchAsync, never as a side effect of editing one finding.
        batch.Status = ImportBatchStatuses.RequiresReview;
        batch.ReviewedByUserId = null;
        batch.ReviewedAt = null;
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException ex)
        {
            // RowVersion detects two reviewers editing the same row at once. The second
            // reviewer must reload instead of silently overwriting the first decision.
            throw new InvalidOperationException("This review item changed while you were viewing it. Reload the page and try again.", ex);
        }
    }

    private static bool IsOpen(ImportDataQualityIssue issue) =>
        ImportWorkflowRules.IsUnresolved(issue);

    // Open findings still require a decision. Referring any finding is an explicit
    // decision to defer it, so it remains visible in verification without delaying import.
    // The commit service separately enforces the true database minimums (station and date).
    private static bool BlocksApproval(ImportDataQualityIssue issue) =>
        ImportWorkflowRules.BlocksBatchApproval(issue);

    private static bool IsReviewedForBatchApproval(StagingCrashSummary row) =>
        ImportWorkflowRules.IsReviewedForBatchApproval(row);

    private static void EnsureRowCanBeChanged(StagingCrashSummary row)
    {
        // Staging decisions become immutable after the row crosses into production.
        if (row.ProductionSummaryId.HasValue || row.ImportStatus == ImportRecordStatuses.Imported)
            throw new InvalidOperationException("An imported row can no longer be approved or rejected in staging.");
    }
    private static string? CleanNotes(string? notes) => string.IsNullOrWhiteSpace(notes)
        ? null : Limit(notes.Trim(), 1000);
    private static string Limit(string value, int max) => value[..Math.Min(value.Length, max)];

    private static string Required(string? value, string field, int max)
    {
        var cleaned = Optional(value, max);
        return cleaned ?? throw new InvalidOperationException($"{field} is required.");
    }

    private static string? Optional(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        // Splitting and joining also removes repeated spaces copied from Excel.
        return Limit(string.Join(' ', value.Split((char[]?)null,
            StringSplitOptions.RemoveEmptyEntries)), max);
    }

    private static DateOnly ParseDate(string? value)
    {
        // Apply the same harmless whitespace cleanup used during workbook ingestion.
        // This accepts values such as "14/ 03/2026" in the manual correction editor.
        var text = value is null ? null : string.Concat(value.Where(character => !char.IsWhiteSpace(character)));
        var formats = new[] { "yyyy-MM-dd", "d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "dd-MM-yyyy" };
        if (DateOnly.TryParseExact(text, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var date)) return date;
        throw new InvalidOperationException("Enter the crash date as yyyy-MM-dd or dd/MM/yyyy.");
    }

    private static TimeOnly ParseTime(string? value)
    {
        var text = value?.Trim().ToUpperInvariant().Replace("H", ":").Replace(".", ":");
        var formats = new[] { "H:mm", "HH:mm", "Hmm", "HHmm" };
        if (TimeOnly.TryParseExact(text, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var time)) return time;
        throw new InvalidOperationException("Enter the crash time as HH:mm, for example 07:30 or 22:00.");
    }

    private static bool CorrectedValueExists(StagingCrashSummary row, string field) => field switch
    {
        "Station" => !string.IsNullOrWhiteSpace(row.Station),
        "ArNumber" => !string.IsNullOrWhiteSpace(row.ArNumber),
        "CasNumber" => !string.IsNullOrWhiteSpace(row.CasNumber),
        "Route" => !string.IsNullOrWhiteSpace(row.Route),
        "Location" => !string.IsNullOrWhiteSpace(row.Location),
        "CrashType" => !string.IsNullOrWhiteSpace(row.CrashType),
        // These values are guaranteed by ParseDate/ParseTime above.
        "CrashDate" or "Date" or "Day" => row.CrashDate.HasValue,
        "CrashTime" or "Time" => row.CrashTime.HasValue,
        _ => false
    };
}
