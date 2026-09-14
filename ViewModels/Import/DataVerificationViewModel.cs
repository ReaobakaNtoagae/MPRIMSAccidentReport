using CrashReport.Models.Import.Models;

namespace CrashReport.ViewModels.Import;

public sealed class DataVerificationViewModel
{
    public IReadOnlyList<DataVerificationItemViewModel> Items { get; init; } = [];
    public int Total => Items.Count;
    public int Blocking => Items.Count(item => item.IsBlocking);
    public int Overdue => Items.Count(item => item.IsOverdue);
    // Every item in this queue has already been deliberately deferred.
    public int ImportAllowed => Items.Count;
}

public sealed class DataVerificationItemViewModel
{
    public long IssueId { get; init; }
    public int ImportBatchId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ReportingPeriod { get; init; } = string.Empty;
    public string BatchStatus { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string RecordSummary { get; init; } = string.Empty;
    public string Field { get; init; } = string.Empty;
    public string IssueCode { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? OriginalValue { get; init; }
    public string ReferredTo { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public DateTime ReferredAt { get; init; }
    public DateTime? ResponseDueAt { get; init; }
    public bool IsBlocking { get; init; }
    public bool IsOverdue => ResponseDueAt.HasValue && ResponseDueAt.Value.Date < DateTime.Today;

    public static DataVerificationItemViewModel From(ImportDataQualityIssue issue)
    {
        var batch = issue.ImportBatch;
        var row = issue.StagingSummary;
        return new DataVerificationItemViewModel
        {
            IssueId = issue.IssueId,
            ImportBatchId = issue.ImportBatchId,
            FileName = batch.OriginalFileName,
            ReportingPeriod = new DateTime(batch.ReportingYear, batch.ReportingMonth, 1).ToString("MMMM yyyy"),
            BatchStatus = batch.Status,
            Source = row is null ? "Workbook summary" : $"{row.WorksheetName} · row {row.SourceRowNumber}",
            RecordSummary = row is null ? batch.SelectedRegion :
                $"{row.Station ?? "Unknown station"} · {row.CrashDate?.ToString("dd MMM yyyy") ?? "Date missing"} · {row.Route ?? "Route missing"}",
            Field = issue.FieldName ?? "Workbook summary",
            IssueCode = issue.IssueCode,
            Description = issue.Description,
            OriginalValue = issue.OriginalValue,
            ReferredTo = issue.ReferredTo ?? "Not specified",
            Question = issue.ReferralQuestion ?? string.Empty,
            ReferredAt = issue.ReferredAt ?? issue.CreatedAt,
            ResponseDueAt = issue.ResponseDueAt,
            IsBlocking = issue.IsBlocking
        };
    }
}
