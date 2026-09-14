using ClosedXML.Excel;

namespace CrashReport.Services.Import
{
    public interface IWorkbookTemplateDetector
    {
        WorkbookTemplateProfile Detect(XLWorkbook workbook);
    }
}
