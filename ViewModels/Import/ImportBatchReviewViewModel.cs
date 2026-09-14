using CrashReport.Models.Import.Models;
using CrashReport.Services.Import;

namespace CrashReport.ViewModels.Import;

public sealed class ImportBatchReviewViewModel
{
    public int ImportBatchId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string ReportingPeriod { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Template { get; init; } = string.Empty;
    public decimal? TemplateConfidence { get; init; }
    public int TotalRows { get; init; }
    public int ValidRows { get; init; }
    public int WarningRows { get; init; }
    public int ErrorRows { get; init; }
    public int OpenIssues { get; init; }
    public int BlockingIssues { get; init; }
    public bool CanApproveBatch { get; init; }
    public IReadOnlyList<ImportReviewIssueViewModel> BatchIssues { get; init; } = [];
    public IReadOnlyList<ImportReviewRowViewModel> Rows { get; init; } = [];

    public static ImportBatchReviewViewModel From(ImportBatch batch)
    {
        var orderedRows = batch.CrashRows
            .OrderBy(row => row.WorksheetName)
            .ThenBy(row => row.SourceRowNumber)
            .ToArray();
        var rowIssues = orderedRows.SelectMany(row => row.Issues).ToArray();
        var batchIssues = batch.Issues.Where(issue => !issue.StagingSummaryId.HasValue).ToArray();
        var issues = rowIssues.Concat(batchIssues).Distinct().ToArray();

        return new ImportBatchReviewViewModel
        {
            ImportBatchId = batch.ImportBatchId,
            FileName = batch.OriginalFileName,
            Region = batch.SelectedRegion,
            ReportingPeriod = $"{new DateTime(batch.ReportingYear, batch.ReportingMonth, 1):MMMM yyyy}",
            Status = batch.Status,
            Template = batch.DetectedTemplate ?? "Not detected",
            TemplateConfidence = batch.TemplateDetectionConfidence,
            TotalRows = orderedRows.Length,
            ValidRows = orderedRows.Count(row => row.ValidationStatus == ImportValidationStatuses.Valid),
            WarningRows = orderedRows.Count(row => row.ValidationStatus == ImportValidationStatuses.Warning),
            ErrorRows = orderedRows.Count(row => row.ValidationStatus == ImportValidationStatuses.Error),
            OpenIssues = issues.Count(IsUnresolved),
            BlockingIssues = issues.Count(issue => issue.IsBlocking && IsApprovalBlocking(issue)),
            CanApproveBatch = orderedRows.Any(row => row.ReviewStatus != ImportReviewStatuses.Rejected) &&
                orderedRows.Where(row => row.ReviewStatus != ImportReviewStatuses.Rejected)
                    .All(IsRowApprovalReady) &&
                orderedRows.Any(row => row.ReviewStatus == ImportReviewStatuses.Approved &&
                    row.ImportStatus == ImportRecordStatuses.NotImported) &&
                !issues.Any(IsApprovalBlocking),
            BatchIssues = batchIssues
                .OrderByDescending(issue => issue.IsBlocking)
                .ThenBy(issue => issue.FieldName)
                .Select(ToIssueViewModel)
                .ToArray(),
            Rows = orderedRows.Select(ImportReviewRowViewModel.From).ToArray()
        };
    }

    private static ImportReviewIssueViewModel ToIssueViewModel(ImportDataQualityIssue issue) =>
        new(issue.IssueId, issue.FieldName ?? "Workbook summary", issue.IssueCode,
            issue.Severity, issue.IsBlocking, issue.Description, issue.OriginalValue,
            issue.SuggestedValue, issue.ResolutionStatus, issue.ResolutionNotes, issue.ReferredTo,
            issue.ReferralQuestion, issue.ResponseDueAt, issue.DataOwnerResponse,
            issue.ReferredAt, issue.RespondedAt);

    private static bool IsUnresolved(ImportDataQualityIssue issue) =>
        ImportWorkflowRules.IsUnresolved(issue);

    private static bool IsApprovalBlocking(ImportDataQualityIssue issue) =>
        ImportWorkflowRules.BlocksBatchApproval(issue);

    private static bool IsRowApprovalReady(StagingCrashSummary row) =>
        ImportWorkflowRules.IsReviewedForBatchApproval(row);
}

public sealed class ImportReviewRowViewModel
{
    public long StagingSummaryId { get; init; }
    public string Source { get; init; } = string.Empty;
    public string Station { get; init; } = string.Empty;
    public string ArNumber { get; init; } = string.Empty;
    public string CasNumber { get; init; } = string.Empty;
    public string Date { get; init; } = string.Empty;
    public string OriginalDay { get; init; } = string.Empty;
    public string CalculatedDay { get; init; } = string.Empty;
    public string Time { get; init; } = string.Empty;
    public string Route { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string CrashType { get; init; } = string.Empty;
    public string Vehicles { get; init; } = string.Empty;
    public byte? VehicleCount { get; init; }
    public byte? FatalDrivers { get; init; }
    public byte? FatalPassengers { get; init; }
    public byte? FatalPedestrians { get; init; }
    public byte? FatalCyclists { get; init; }
    public byte? FatalMale { get; init; }
    public byte? FatalFemale { get; init; }
    public byte? SeriousDrivers { get; init; }
    public byte? SeriousPassengers { get; init; }
    public byte? SeriousPedestrians { get; init; }
    public byte? SeriousCyclists { get; init; }
    public byte? SlightDrivers { get; init; }
    public byte? SlightPassengers { get; init; }
    public byte? SlightPedestrians { get; init; }
    public byte? SlightCyclists { get; init; }
    public string ValidationStatus { get; init; } = string.Empty;
    public string ReviewStatus { get; init; } = string.Empty;
    public string DuplicateStatus { get; init; } = string.Empty;
    public int OpenIssueCount { get; init; }
    public bool HasBlockingIssue { get; init; }
    public IReadOnlyList<ImportReviewIssueViewModel> Issues { get; init; } = [];

    public static ImportReviewRowViewModel From(StagingCrashSummary row) => new()
    {
        StagingSummaryId = row.StagingSummaryId,
        Source = $"{row.WorksheetName} · row {row.SourceRowNumber}",
        Station = row.Station ?? "Missing",
        ArNumber = row.ArNumber ?? string.Empty,
        CasNumber = row.CasNumber ?? string.Empty,
        Date = row.CrashDate?.ToString("yyyy-MM-dd") ?? "Missing",
        OriginalDay = row.OriginalDay ?? "Missing",
        CalculatedDay = row.CalculatedDay ?? "Missing",
        Time = row.CrashTime?.ToString("HH:mm") ?? "Missing",
        Route = row.Route ?? "Missing",
        Location = row.Location ?? "Missing",
        CrashType = row.CrashType ?? "Missing",
        Vehicles = row.VehiclesString ?? "Missing",
        VehicleCount = row.VehicleCount,
        FatalDrivers = row.FatalDrivers,
        FatalPassengers = row.FatalPassengers,
        FatalPedestrians = row.FatalPedestrians,
        FatalCyclists = row.FatalCyclists,
        FatalMale = row.FatalMale,
        FatalFemale = row.FatalFemale,
        SeriousDrivers = row.SeriousDrivers,
        SeriousPassengers = row.SeriousPassengers,
        SeriousPedestrians = row.SeriousPedestrians,
        SeriousCyclists = row.SeriousCyclists,
        SlightDrivers = row.SlightDrivers,
        SlightPassengers = row.SlightPassengers,
        SlightPedestrians = row.SlightPedestrians,
        SlightCyclists = row.SlightCyclists,
        ValidationStatus = row.ValidationStatus,
        ReviewStatus = row.ReviewStatus,
        DuplicateStatus = row.DuplicateStatus,
        OpenIssueCount = row.Issues.Count(issue => issue.ResolutionStatus is
            ImportIssueResolutionStatuses.Open or ImportIssueResolutionStatuses.PendingDataOwner),
        HasBlockingIssue = row.Issues.Any(issue => issue.IsBlocking &&
            issue.ResolutionStatus == ImportIssueResolutionStatuses.Open),
        Issues = row.Issues
            .OrderByDescending(issue => issue.IsBlocking)
            .ThenByDescending(issue => issue.Severity)
            .Select(issue => new ImportReviewIssueViewModel(
                issue.IssueId, issue.FieldName ?? "Row", issue.IssueCode, issue.Severity,
                issue.IsBlocking, issue.Description, issue.OriginalValue,
                issue.SuggestedValue, issue.ResolutionStatus, issue.ResolutionNotes, issue.ReferredTo,
                issue.ReferralQuestion, issue.ResponseDueAt, issue.DataOwnerResponse,
                issue.ReferredAt, issue.RespondedAt))
            .ToArray()
    };
}

public sealed record ImportReviewIssueViewModel(
    long IssueId,
    string Field,
    string Code,
    string Severity,
    bool IsBlocking,
    string Description,
    string? OriginalValue,
    string? SuggestedValue,
    string ResolutionStatus,
    // Expose the recorded rationale so the review page can show which source won.
    string? ResolutionNotes,
    string? ReferredTo,
    string? ReferralQuestion,
    DateTime? ResponseDueAt,
    string? DataOwnerResponse,
    DateTime? ReferredAt,
    DateTime? RespondedAt);
