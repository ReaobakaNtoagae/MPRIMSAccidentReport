using ClosedXML.Excel;
using CrashReport.Models.Import;
using CrashReport.Models.Import.Models;
namespace CrashReport.Services.Import;

public interface IWorkbookCrashRowParser
{
    IReadOnlyList<StagingCrashSummary> Parse(XLWorkbook workbook, WorkbookTemplateProfile profile, int importBatchId, int reportingMonth, int reportingYear);

}

