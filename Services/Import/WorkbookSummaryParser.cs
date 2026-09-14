using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using CrashReport.Models.Import.Models;

namespace CrashReport.Services.Import;

/// <summary>
/// Reads the optional totals and demographic blocks below the accident register,
/// then checks their reported values against totals recalculated from detail rows.
/// </summary>
public sealed partial class WorkbookSummaryParser : IWorkbookSummaryParser
{
    public WorkbookSummaryParseResult Parse(
        XLWorkbook workbook,
        WorkbookTemplateProfile profile,
        ImportBatch batch,
        IReadOnlyCollection<StagingCrashSummary> detailRows)
    {
        var sheet = workbook.Worksheet(profile.WorksheetName);
        var summaryStart = WorkbookSectionLocator.FindSummaryStart(sheet, profile);
        if (!summaryStart.HasValue)
            return new(null, []);

        var issues = new List<ImportDataQualityIssue>();
        CompareCrashCount(sheet, summaryStart.Value, batch, detailRows.Count, issues);
        CompareCasualtySummary(sheet, summaryStart.Value, profile, batch, detailRows, issues);

        var demographics = ParseDemographics(sheet, summaryStart.Value, batch);
        if (demographics is not null)
            CompareDemographics(demographics, batch, detailRows, issues);

        return new(demographics, issues);
    }

    private static StagingImportDemographics? ParseDemographics(
        IXLWorksheet sheet, int summaryStart, ImportBatch batch)
    {
        var ageRow = FindHeadingRow(sheet, summaryStart, "AGE");
        var genderRow = FindHeadingRow(sheet, summaryStart, "VICTIMGENDER");
        var raceRow = FindHeadingRow(sheet, summaryStart, "RACE");
        if (ageRow is null && genderRow is null && raceRow is null)
            return null;

        var result = new StagingImportDemographics
        {
            ImportBatchId = batch.ImportBatchId,
            WorksheetName = sheet.Name,
            PeriodFrom = new DateOnly(batch.ReportingYear, batch.ReportingMonth, 1),
            PeriodTo = new DateOnly(batch.ReportingYear, batch.ReportingMonth,
                DateTime.DaysInMonth(batch.ReportingYear, batch.ReportingMonth)),
            ProvinceCode = "MP",
            Region = batch.SelectedRegion,
            RawSectionJson = SerialiseSummaryRows(sheet, summaryStart),
            ValidationStatus = ImportValidationStatuses.Valid
        };

        if (ageRow.HasValue)
        {
            var valuesRow = NextRowWithNumbers(sheet, ageRow.Value + 1);
            result.Age0to7 = ReadUnderHeader(sheet, ageRow.Value, valuesRow, "07", "0TO7");
            result.Age8to12 = ReadUnderHeader(sheet, ageRow.Value, valuesRow, "0812", "8TO12");
            result.Age13to18 = ReadUnderHeader(sheet, ageRow.Value, valuesRow, "1318", "13TO18");
            result.Age19to35 = ReadUnderHeader(sheet, ageRow.Value, valuesRow, "1935", "19TO35");
            result.Age36Plus = ReadUnderHeader(sheet, ageRow.Value, valuesRow, "36", "36PLUS");
        }

        if (genderRow.HasValue)
            ParseGenderRows(sheet, genderRow.Value, result);

        if (raceRow.HasValue)
        {
            var valuesRow = NextRowWithNumbers(sheet, raceRow.Value + 1);
            result.RaceBlack = ReadUnderHeader(sheet, raceRow.Value, valuesRow, "B", "BLACK");
            result.RaceColoured = ReadUnderHeader(sheet, raceRow.Value, valuesRow, "C", "COLOURED");
            result.RaceWhite = ReadUnderHeader(sheet, raceRow.Value, valuesRow, "W", "WHITE");
            result.RaceIndian = ReadUnderHeader(sheet, raceRow.Value, valuesRow, "I", "INDIAN");
            result.RaceOther = ReadUnderHeader(sheet, raceRow.Value, valuesRow, "O", "OTHER");
        }

        return result;
    }

    private static void ParseGenderRows(
        IXLWorksheet sheet, int headingRow, StagingImportDemographics result)
    {
        var maleColumn = FindHeaderColumn(sheet, headingRow, "M", "MALE");
        var femaleColumn = FindHeaderColumn(sheet, headingRow, "F", "FEMALE");
        if (!maleColumn.HasValue && !femaleColumn.HasValue) return;

        var lastRow = Math.Min(sheet.LastRowUsed()?.RowNumber() ?? headingRow, headingRow + 12);
        for (var row = headingRow + 1; row <= lastRow; row++)
        {
            var label = WorkbookSectionLocator.Normalise(sheet.Cell(row, 1).GetFormattedString());
            if (label is "TOTAL" or "GRANDTOTAL" || label == "RACE") break;

            var male = maleColumn.HasValue ? ReadInt(sheet.Cell(row, maleColumn.Value)) : null;
            var female = femaleColumn.HasValue ? ReadInt(sheet.Cell(row, femaleColumn.Value)) : null;
            switch (label)
            {
                case "DRIVER": result.DriverMale = male; result.DriverFemale = female; break;
                case "PASSENGER": result.PassengerMale = male; result.PassengerFemale = female; break;
                case "PEDESTRIAN": result.PedestrianMale = male; result.PedestrianFemale = female; break;
                case "CYCLIST":
                case "CYLIST": result.CyclistMale = male; result.CyclistFemale = female; break;
            }
        }
    }

    private static void CompareCrashCount(
        IXLWorksheet sheet, int start, ImportBatch batch, int calculated,
        List<ImportDataQualityIssue> issues)
    {
        var lastRow = Math.Min(sheet.LastRowUsed()?.RowNumber() ?? start, start + 8);
        for (var row = start; row <= lastRow; row++)
        {
            foreach (var cell in sheet.Row(row).CellsUsed())
            {
                var match = CrashTotalPattern().Match(cell.GetFormattedString());
                if (!match.Success || !int.TryParse(match.Groups[1].Value, out var reported)) continue;
                Compare(issues, batch, "CrashCount", reported, calculated);
                return;
            }
        }
    }

    private static void CompareCasualtySummary(
        IXLWorksheet sheet, int start, WorkbookTemplateProfile profile, ImportBatch batch,
        IReadOnlyCollection<StagingCrashSummary> rows, List<ImportDataQualityIssue> issues)
    {
        var totalsRow = Enumerable.Range(start, Math.Min(5,
                (sheet.LastRowUsed()?.RowNumber() ?? start) - start + 1))
            .FirstOrDefault(row => sheet.Row(row).CellsUsed().Any(cell =>
                WorkbookSectionLocator.Normalise(cell.GetFormattedString()) == "TOTAL"));
        if (totalsRow == 0) return;

        var first = profile.TemplateCode == ImportTemplateCodes.EhlanzeniCas ? 10 : 9;
        CompareMetric("FatalDrivers", first, row => row.FatalDrivers);
        CompareMetric("FatalPassengers", first + 1, row => row.FatalPassengers);
        CompareMetric("FatalPedestrians", first + 2, row => row.FatalPedestrians);
        CompareMetric("FatalCyclists", first + 3, row => row.FatalCyclists);
        CompareMetric("SeriousDrivers", first + 6, row => row.SeriousDrivers);
        CompareMetric("SeriousPassengers", first + 7, row => row.SeriousPassengers);
        CompareMetric("SeriousPedestrians", first + 8, row => row.SeriousPedestrians);
        CompareMetric("SeriousCyclists", first + 9, row => row.SeriousCyclists);
        CompareMetric("SlightDrivers", first + 10, row => row.SlightDrivers);
        CompareMetric("SlightPassengers", first + 11, row => row.SlightPassengers);
        CompareMetric("SlightPedestrians", first + 12, row => row.SlightPedestrians);
        CompareMetric("SlightCyclists", first + 13, row => row.SlightCyclists);

        void CompareMetric(string name, int column,
            Func<StagingCrashSummary, byte?> selector)
        {
            var reported = ReadInt(sheet.Cell(totalsRow, column));
            if (reported.HasValue)
                Compare(issues, batch, name, reported.Value,
                    rows.Sum(row => selector(row) ?? 0));
        }
    }

    private static void CompareDemographics(
        StagingImportDemographics demographics, ImportBatch batch,
        IReadOnlyCollection<StagingCrashSummary> rows, List<ImportDataQualityIssue> issues)
    {
        var calculatedFatalities = rows.Sum(row =>
            (row.FatalDrivers ?? 0) + (row.FatalPassengers ?? 0) +
            (row.FatalPedestrians ?? 0) + (row.FatalCyclists ?? 0));
        var calculatedMale = rows.Sum(row => row.FatalMale ?? 0);
        var calculatedFemale = rows.Sum(row => row.FatalFemale ?? 0);

        CompareOptionalTotal("Demographics.AgeTotal", Sum(
            demographics.Age0to7, demographics.Age8to12, demographics.Age13to18,
            demographics.Age19to35, demographics.Age36Plus), calculatedFatalities);
        CompareOptionalTotal("Demographics.RaceTotal", Sum(
            demographics.RaceBlack, demographics.RaceColoured, demographics.RaceWhite,
            demographics.RaceIndian, demographics.RaceOther), calculatedFatalities);
        CompareOptionalTotal("Demographics.MaleTotal", Sum(
            demographics.DriverMale, demographics.PassengerMale,
            demographics.PedestrianMale, demographics.CyclistMale), calculatedMale);
        CompareOptionalTotal("Demographics.FemaleTotal", Sum(
            demographics.DriverFemale, demographics.PassengerFemale,
            demographics.PedestrianFemale, demographics.CyclistFemale), calculatedFemale);

        if (issues.Any(issue => issue.FieldName?.StartsWith("Demographics.", StringComparison.Ordinal) == true))
            demographics.ValidationStatus = ImportValidationStatuses.Warning;

        void CompareOptionalTotal(string field, int? reported, int calculated)
        {
            if (reported.HasValue) Compare(issues, batch, field, reported.Value, calculated);
        }
    }

    private static void Compare(
        List<ImportDataQualityIssue> issues, ImportBatch batch,
        string field, int reported, int calculated)
    {
        if (reported == calculated) return;
        issues.Add(new ImportDataQualityIssue
        {
            ImportBatchId = batch.ImportBatchId,
            ImportBatch = batch,
            FieldName = field,
            IssueCode = "SUMMARY_TOTAL_MISMATCH",
            Severity = ImportIssueSeverities.Warning,
            IsBlocking = false,
            Description = $"The workbook summary reports {reported}, but the staged detail rows calculate {calculated}.",
            OriginalValue = reported.ToString(CultureInfo.InvariantCulture),
            SuggestedValue = calculated.ToString(CultureInfo.InvariantCulture),
            ResolutionStatus = ImportIssueResolutionStatuses.Open,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static int? FindHeadingRow(IXLWorksheet sheet, int start, string heading)
    {
        var last = sheet.LastRowUsed()?.RowNumber() ?? start;
        for (var row = start; row <= last; row++)
            if (sheet.Row(row).CellsUsed().Any(cell =>
                    WorkbookSectionLocator.Normalise(cell.GetFormattedString()) == heading))
                return row;
        return null;
    }

    private static int NextRowWithNumbers(IXLWorksheet sheet, int start)
    {
        var last = Math.Min(sheet.LastRowUsed()?.RowNumber() ?? start, start + 5);
        for (var row = start; row <= last; row++)
            if (sheet.Row(row).CellsUsed().Any(cell => ReadInt(cell).HasValue)) return row;
        return start;
    }

    private static int? ReadUnderHeader(
        IXLWorksheet sheet, int headerRow, int valuesRow, params string[] labels)
    {
        var column = FindHeaderColumn(sheet, headerRow, labels);
        return column.HasValue ? ReadInt(sheet.Cell(valuesRow, column.Value)) : null;
    }

    private static int? FindHeaderColumn(IXLWorksheet sheet, int row, params string[] labels)
    {
        foreach (var cell in sheet.Row(row).CellsUsed())
        {
            var value = WorkbookSectionLocator.Normalise(cell.GetFormattedString())
                .Replace("-", string.Empty, StringComparison.Ordinal);
            if (labels.Contains(value)) return cell.Address.ColumnNumber;
        }
        return null;
    }

    private static int? ReadInt(IXLCell cell)
    {
        if (cell.TryGetValue<double>(out var numeric) &&
            numeric >= 0 && numeric <= int.MaxValue && numeric == Math.Truncate(numeric))
            return (int)numeric;
        return int.TryParse(cell.GetFormattedString().Trim(), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static int? Sum(params int?[] values) =>
        values.Any(value => value.HasValue) ? values.Sum(value => value ?? 0) : null;

    private static string SerialiseSummaryRows(IXLWorksheet sheet, int start)
    {
        var last = sheet.LastRowUsed()?.RowNumber() ?? start;
        var rows = Enumerable.Range(start, last - start + 1).ToDictionary(
            row => row.ToString(CultureInfo.InvariantCulture),
            row => sheet.Row(row).CellsUsed().ToDictionary(
                cell => cell.Address.ColumnLetter,
                cell => cell.GetFormattedString()));
        return JsonSerializer.Serialize(rows);
    }

    [GeneratedRegex(@"TOTAL\s*:\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex CrashTotalPattern();
}
