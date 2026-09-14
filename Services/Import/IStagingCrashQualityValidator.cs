using CrashReport.Models.Import.Models;

namespace CrashReport.Services.Import;

/// <summary>
/// Applies business-quality rules after parsing. The parser answers “what does this cell
/// contain?” while this validator answers “is that value safe to import?”.
/// </summary>
public interface IStagingCrashQualityValidator
{
    IReadOnlyList<ImportDataQualityIssue> Validate(
        StagingCrashSummary row,
        ImportBatch batch);
}
