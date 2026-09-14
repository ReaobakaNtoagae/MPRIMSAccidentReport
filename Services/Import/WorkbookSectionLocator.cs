using ClosedXML.Excel;

namespace CrashReport.Services.Import;

/// <summary>
/// Finds the boundary between accident detail rows and the summary blocks that
/// follow them. The search uses labels rather than fixed row numbers because the
/// number of crashes changes every month.
/// </summary>
public static class WorkbookSectionLocator
{
    private static readonly string[] SummaryHeadings =
        ["TOTAL", "GRANDTOTAL", "VICTIMS", "VICTIMGENDER", "RACE", "AGE"];

    public static int? FindSummaryStart(IXLWorksheet sheet, WorkbookTemplateProfile profile)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? profile.HeaderRow;
        var lastColumn = Math.Min(sheet.LastColumnUsed()?.ColumnNumber() ?? 1, 30);

        for (var row = profile.FirstDataRow; row <= lastRow; row++)
        {
            for (var column = 1; column <= lastColumn; column++)
            {
                var text = Normalise(sheet.Cell(row, column).GetFormattedString());
                if (SummaryHeadings.Contains(text) || text.StartsWith("TOTAL:", StringComparison.Ordinal))
                    return row;
            }
        }

        return null;
    }

    public static string Normalise(string? value) =>
        new((value ?? string.Empty).Trim().ToUpperInvariant()
            .Where(character => char.IsLetterOrDigit(character) || character == ':')
            .ToArray());
}
