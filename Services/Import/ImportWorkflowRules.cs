using CrashReport.Models.Import.Models;

namespace CrashReport.Services.Import;

// One policy source keeps controllers, services and views from interpreting the
// same status differently. These methods contain no database code, so they are
// straightforward to unit test during the API migration.
public static class ImportWorkflowRules
{
    public static bool IsUnresolved(ImportDataQualityIssue issue) =>
        issue.ResolutionStatus is ImportIssueResolutionStatuses.Open
            or ImportIssueResolutionStatuses.PendingDataOwner;

    // A referral is intentionally deferred and therefore does not block other rows.
    public static bool BlocksBatchApproval(ImportDataQualityIssue issue) =>
        issue.ResolutionStatus == ImportIssueResolutionStatuses.Open;

    public static bool IsReviewedForBatchApproval(StagingCrashSummary row) =>
        (row.ReviewStatus is ImportReviewStatuses.Approved
            or ImportReviewStatuses.AwaitingDataOwner) &&
        !row.Issues.Any(BlocksBatchApproval);

    public static bool IsImportCandidate(StagingCrashSummary row) =>
        row.ReviewStatus == ImportReviewStatuses.Approved &&
        row.ImportStatus == ImportRecordStatuses.NotImported &&
        !row.ProductionSummaryId.HasValue;
}

// Production identifiers must be constructed identically during duplicate
// review and final commit. A shared implementation prevents late surprises.
public static class ImportProductionIdentity
{
    public static string BuildCrashNumber(StagingCrashSummary row)
    {
        var station = row.Station!.Trim().ToUpperInvariant();
        var value = !string.IsNullOrWhiteSpace(row.ArNumber)
            ? CrashReport.Services.CrashNumberFormatter.Format(station, row.ArNumber)
            : $"{station}-IMP{row.ImportBatchId:D6}-{row.SourceRowNumber:D4}";
        return value[..Math.Min(value.Length, 50)];
    }

    public static string Normalise(string? value) => string.Join(' ',
        (value ?? string.Empty).Trim().ToUpperInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
