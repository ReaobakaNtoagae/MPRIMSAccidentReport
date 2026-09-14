namespace CrashReport.Services.Import;

/// <summary>
/// Turns an uploaded workbook into validated staging rows. Keeping this separate from
/// file intake means accepting a file never silently writes accident data to production.
/// </summary>
public interface IImportBatchProcessingService
{
    Task<ImportBatchProcessingResult> ProcessAsync(
        int importBatchId,
        CancellationToken cancellationToken = default);
}

public sealed record ImportBatchProcessingResult(
    int ImportBatchId,
    string Status,
    string TemplateCode,
    int TotalRows,
    int ValidRows,
    int WarningRows,
    int ErrorRows,
    int OpenIssues,
    int BlockingIssues);
