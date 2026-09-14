using ClosedXML.Excel;
using CrashReport.Models.Import.Models;

namespace CrashReport.Services.Import;

public interface IWorkbookSummaryParser
{
    WorkbookSummaryParseResult Parse(
        XLWorkbook workbook,
        WorkbookTemplateProfile profile,
        ImportBatch batch,
        IReadOnlyCollection<StagingCrashSummary> detailRows);
}

public sealed record WorkbookSummaryParseResult(
    StagingImportDemographics? Demographics,
    IReadOnlyList<ImportDataQualityIssue> Issues);
