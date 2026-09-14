using ClosedXML.Excel;
namespace CrashReport.Services.Import;

public sealed class WorkbookTemplateDetector : IWorkbookTemplateDetector
{
    private static readonly string[] Required = ["Station", "Date", "Day", "Time", "Route", "Location", "CrashType", "VehiclesInvolved"];
    public WorkbookTemplateProfile Detect(XLWorkbook workbook)
    {
        WorkbookTemplateProfile? best = null;
        foreach (var sheet in workbook.Worksheets)
            for (var row = 1; row <= Math.Min(sheet.LastRowUsed()?.RowNumber() ?? 0, 20); row++)
            {
                var candidate = Score(sheet, row);
                if (candidate != null && (best == null || candidate.Confidence > best.Confidence)) best = candidate;
            }
        if (best == null || best.Confidence < .80m) throw new ImportWorkbookIntakeException("The workbook layout was not recognised. No rows were staged.");
        return best;
    }

    private static WorkbookTemplateProfile? Score(IXLWorksheet sheet, int row)
    {
        var values = Enumerable.Range(1, Math.Min(sheet.LastColumnUsed()?.ColumnNumber() ?? 0, 40)).ToDictionary(x => x, x => Normalise(sheet.Cell(row, x).GetFormattedString()));
        if (!values.Values.Any(x => x.Contains("SAPS") || x.Contains("STATION"))) return null;
        var columns = new Dictionary<string, int>();
        Find(columns, "Station", values, "SAPS STATION", "SAPS"); Find(columns, "Date", values, "DATE"); Find(columns, "Day", values, "DAY");
        Find(columns, "Time", values, "TIME"); Find(columns, "Route", values, "ROUTE", "ROUTE NO"); Find(columns, "Location", values, "LOCATION");
        Find(columns, "CrashType", values, "TYPE"); Find(columns, "VehiclesInvolved", values, "INVOLVED", "VEHICLES"); Find(columns, "ArNumber", values, "AR NO", "A R NO", "NO");
        var hasCas = values.Values.Any(x => x == "CAS"); if (hasCas) Find(columns, "CasNumber", values, "CAS");
        var confidence = Required.Count(columns.ContainsKey) / (decimal)Required.Length;
        return confidence < .50m ? null : new(hasCas ? ImportTemplateCodes.EhlanzeniCas : ImportTemplateCodes.StandardDistrict, sheet.Name, row, row + 1, confidence, columns);
    }
    private static void Find(Dictionary<string, int> output, string name, Dictionary<int, string> cells, params string[] aliases)
    { var match = cells.FirstOrDefault(x => aliases.Any(a => x.Value == a || x.Value.Contains(a))); if (match.Key > 0) output[name] = match.Key; }
    private static string Normalise(string value) => string.Join(' ', value.Trim().ToUpperInvariant().Replace("/", " ").Replace(".", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
