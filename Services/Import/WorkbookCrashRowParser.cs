using ClosedXML.Excel;
using CrashReport.Models.Import;
using CrashReport.Models.Import.Models;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace CrashReport.Services.Import;

public sealed partial class WorkbookCrashRowParser : IWorkbookCrashRowParser
{
    public IReadOnlyList<StagingCrashSummary> Parse(XLWorkbook workbook, WorkbookTemplateProfile profile, int importBatchId, int reportingMonth, int reportingYear)
    {
        var sheet = workbook.Worksheet(profile.WorksheetName); var rows = new List<StagingCrashSummary>();
        var last = sheet.LastRowUsed()?.RowNumber() ?? profile.HeaderRow;
        // Summary tables are not accident records. Stop before their first recognised
        // heading instead of relying on a fixed row number for every month.
        var summaryStart = WorkbookSectionLocator.FindSummaryStart(sheet, profile);
        var detailEnd = summaryStart.HasValue ? summaryStart.Value - 1 : last;
        for (var rowNumber = profile.FirstDataRow; rowNumber <= detailEnd; rowNumber++)
        {
            var row = ParseRow(sheet, rowNumber, profile, importBatchId, reportingMonth, reportingYear);
            if (row != null) rows.Add(row);
        }
        return rows;
    }

    private static StagingCrashSummary? ParseRow(IXLWorksheet sheet, int n, WorkbookTemplateProfile p, int batchId, int month, int year)
    {
        string Read(string key) => p.Columns.TryGetValue(key, out var col) ? sheet.Cell(n, col).GetFormattedString().Trim() : "";
        var station = Read("Station"); var date = Read("Date"); var location = Read("Location");
        if (string.IsNullOrWhiteSpace(station + date + location) || IsSummary(station)) return null;
        var injuryStart = p.TemplateCode == ImportTemplateCodes.EhlanzeniCas ? 10 : 9;
        var raw = Enumerable.Range(1, sheet.LastColumnUsed()?.ColumnNumber() ?? 1).ToDictionary(c => sheet.Cell(p.HeaderRow, c).Address.ColumnLetter + c, c => sheet.Cell(n, c).GetFormattedString());
        var crashDate = ParseDate(sheet.Cell(n, p.Columns["Date"]), month, year);
        return new StagingCrashSummary
        {
            ImportBatchId = batchId,
            WorksheetName = sheet.Name,
            SourceRowNumber = n,
            RawRowJson = JsonSerializer.Serialize(raw),
            OriginalStation = Null(station),
            Station = Clean(station, 50, stripCas: true),
            OriginalArNumber = Null(Read("ArNumber")),
            ArNumber = Clean(Read("ArNumber"), 50),
            OriginalCasNumber = Null(Read("CasNumber")),
            CasNumber = Clean(Read("CasNumber"), 50),
            OriginalDate = Null(date),
            CrashDate = crashDate,
            OriginalDay = Null(Read("Day")),
            CalculatedDay = crashDate?.DayOfWeek.ToString(),
            OriginalTime = Null(Read("Time")),
            CrashTime = ParseTime(sheet.Cell(n, p.Columns["Time"])),
            OriginalRoute = Null(Read("Route")),
            Route = Clean(Read("Route"), 20),
            OriginalLocation = Null(location),
            Location = Clean(location, 150),
            OriginalCrashType = Null(Read("CrashType")),
            CrashType = Clean(Read("CrashType"), 30),
            OriginalVehicles = Null(Read("VehiclesInvolved")),
            VehiclesString = Clean(Read("VehiclesInvolved"), 100),
            VehicleCount = CountVehicles(Read("VehiclesInvolved")),
            FatalDrivers = Byte(sheet, n, injuryStart),
            FatalPassengers = Byte(sheet, n, injuryStart + 1),
            FatalPedestrians = Byte(sheet, n, injuryStart + 2),
            FatalCyclists = Byte(sheet, n, injuryStart + 3),
            // The two columns after fatality roles contain the fatal gender split.
            // Keeping these values enables reconciliation with the bottom summary table.
            FatalMale = Byte(sheet, n, injuryStart + 4),
            FatalFemale = Byte(sheet, n, injuryStart + 5),
            SeriousDrivers = Byte(sheet, n, injuryStart + 6),
            SeriousPassengers = Byte(sheet, n, injuryStart + 7),
            SeriousPedestrians = Byte(sheet, n, injuryStart + 8),
            SeriousCyclists = Byte(sheet, n, injuryStart + 9),
            SlightDrivers = Byte(sheet, n, injuryStart + 10),
            SlightPassengers = Byte(sheet, n, injuryStart + 11),
            SlightPedestrians = Byte(sheet, n, injuryStart + 12),
            SlightCyclists = Byte(sheet, n, injuryStart + 13)
        };
    }

    private static DateOnly? ParseDate(IXLCell cell, int month, int year)
    {
        if (cell.TryGetValue<DateTime>(out var dt)) return DateOnly.FromDateTime(dt.Year < 2000 ? new DateTime(year, month, dt.Day) : dt);
        // Excel dates sometimes contain accidental spaces around a separator, for
        // example "14/ 03". Remove whitespace for the cleaned value while retaining
        // OriginalDate above exactly as supplied for the audit trail.
        var value = MultiSpace().Replace(cell.GetFormattedString(), string.Empty);
        if (int.TryParse(value, out var day) && day >= 1 && day <= DateTime.DaysInMonth(year, month)) return new(year, month, day);

        // Dates without a year belong to the reporting period selected at upload.
        // Parsing the two parts ourselves avoids DateTime silently using today's year.
        var parts = value.Split('/');
        if (parts.Length == 2 && int.TryParse(parts[0], out day) &&
            int.TryParse(parts[1], out var parsedMonth) && parsedMonth is >= 1 and <= 12 &&
            day >= 1 && day <= DateTime.DaysInMonth(year, parsedMonth))
            return new DateOnly(year, parsedMonth, day);

        foreach (var format in new[] { "d/M/yyyy", "dd/MM/yyyy" })
            if (DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out dt))
                return DateOnly.FromDateTime(dt);
        return null;
    }
    private static TimeOnly? ParseTime(IXLCell cell)
    {
        // Some workbooks store a date and time in one Excel cell. Read that as a
        // DateTime first so its date portion is not mistaken for a multi-day duration.
        if (cell.TryGetValue<DateTime>(out var dateTime))
            return TimeOnly.FromDateTime(dateTime);

        if (cell.TryGetValue<TimeSpan>(out var span))
        {
            // TimeOnly represents one clock day only. Values such as 24:00, negative
            // durations, or Excel duration totals must remain unparsed so validation can
            // flag the source row instead of aborting the complete workbook import.
            if (span < TimeSpan.Zero || span >= TimeSpan.FromDays(1))
                return null;

            return TimeOnly.FromTimeSpan(span);
        }

        var value = cell.GetFormattedString().Trim().ToUpperInvariant().Replace('H', ':');
        return TimeOnly.TryParseExact(value, ["H:mm", "HH:mm", "Hmm", "HHmm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : null;
    }
    private static byte? Byte(IXLWorksheet sheet, int row, int col) => byte.TryParse(sheet.Cell(row, col).GetFormattedString().Trim(), out var value) ? value : null;
    private static byte? CountVehicles(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Contains("HIT AND RUN", StringComparison.OrdinalIgnoreCase)) return null;

        // P/D (Pedestrian) and M/C (Motorcycle) are single domain abbreviations.
        // Protect their internal slashes before counting the separators between vehicles.
        // For example, "M/C / SED" contains two involved types, not three.
        var protectedValue = PedestrianAbbreviation().Replace(value, "PEDESTRIAN");
        protectedValue = MotorcycleAbbreviation().Replace(protectedValue, "MOTORCYCLE");
        return checked((byte)Math.Min(255, protectedValue
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length));
    }
    private static string? Null(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static string? Clean(string value, int max, bool stripCas = false) { if (string.IsNullOrWhiteSpace(value)) return null; var cleaned = MultiSpace().Replace(value.Trim().ToUpperInvariant(), " "); if (stripCas) cleaned = CasSuffix().Replace(cleaned, "").Trim(); return cleaned[..Math.Min(max, cleaned.Length)]; }
    private static bool IsSummary(string station) => station.Contains("TOTAL", StringComparison.OrdinalIgnoreCase) || station.Contains("REPORTED ACCIDENT", StringComparison.OrdinalIgnoreCase);
    [GeneratedRegex(@"\s+")] private static partial Regex MultiSpace();
    [GeneratedRegex(@"\s+CAS(?:E)?\s*:.*$", RegexOptions.IgnoreCase)] private static partial Regex CasSuffix();
    [GeneratedRegex(@"\bP\s*/\s*D\b", RegexOptions.IgnoreCase)] private static partial Regex PedestrianAbbreviation();
    [GeneratedRegex(@"\bM\s*/\s*C\b", RegexOptions.IgnoreCase)] private static partial Regex MotorcycleAbbreviation();
}
