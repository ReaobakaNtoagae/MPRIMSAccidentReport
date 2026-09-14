using ClosedXML.Excel;
using CrashReport.Models.Import.Models;
using CrashReport.Services.Import;

// This small verification harness exercises the production parsers against the
// real supplied workbooks. It lives under outputs and is not part of the app.
var files = new[]
{
    @"C:\Users\ReaobakaNtoagae\OneDrive - atnetgroup.com\EHL MARCH 2026.xlsx",
    @"C:\Users\ReaobakaNtoagae\Downloads\Upload EHLANZENI 2026-03.xlsx",
    @"C:\Users\ReaobakaNtoagae\Downloads\Upload EHLANZENI 2026-03 (filled).xlsx"
};

var detector = new WorkbookTemplateDetector();
var rowParser = new WorkbookCrashRowParser();
var summaryParser = new WorkbookSummaryParser();

foreach (var file in files)
{
    using var workbook = new XLWorkbook(file);
    var profile = detector.Detect(workbook);
    var rows = rowParser.Parse(workbook, profile, 1, 3, 2026);
    var batch = new ImportBatch
    {
        ImportBatchId = 1,
        SelectedRegion = "EHLANZENI",
        ReportingMonth = 3,
        ReportingYear = 2026
    };
    var summary = summaryParser.Parse(workbook, profile, batch, rows);

    Console.WriteLine($"{Path.GetFileName(file)} | template={profile.TemplateCode} | " +
                      $"detailRows={rows.Count} | demographics={summary.Demographics is not null} | " +
                      $"summaryIssues={summary.Issues.Count}");
    foreach (var issue in summary.Issues.Take(8))
        Console.WriteLine($"  {issue.FieldName}: {issue.OriginalValue} -> {issue.SuggestedValue}");
}
