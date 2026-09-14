using CrashReport.Models.Import.Models;

namespace CrashReport.Services.Import;

public sealed class StagingCrashQualityValidator : IStagingCrashQualityValidator
{
    public IReadOnlyList<ImportDataQualityIssue> Validate(StagingCrashSummary row, ImportBatch batch)
    {
        var issues = new List<ImportDataQualityIssue>();

        // These fields identify when and where the crash happened. Importing without them
        // would create a record that cannot be reliably searched, reported or deduplicated.
        Required(issues, row, "Station", row.OriginalStation, row.Station);
        Required(issues, row, "CrashDate", row.OriginalDate, row.CrashDate?.ToString("yyyy-MM-dd"));
        Required(issues, row, "CrashTime", row.OriginalTime, row.CrashTime?.ToString("HH:mm"));
        Required(issues, row, "Location", row.OriginalLocation, row.Location);

        // Route and crash type are important for analysis, but some historical workbooks
        // genuinely omit them. They therefore need review without blocking the whole row.
        Recommended(issues, row, "Route", row.OriginalRoute, row.Route);
        Recommended(issues, row, "CrashType", row.OriginalCrashType, row.CrashType);

        if (row.CrashDate is { } date)
        {
            // A row uploaded for August must not quietly contain a July accident. This is
            // blocking because it would distort monthly and cumulative reports.
            if (date.Month != batch.ReportingMonth || date.Year != batch.ReportingYear)
            {
                Add(issues, row, "CrashDate", "DATE_OUTSIDE_REPORTING_PERIOD",
                    ImportIssueSeverities.Error, true,
                    $"Crash date {date:yyyy-MM-dd} falls outside the selected reporting period {batch.ReportingYear}-{batch.ReportingMonth:00}.",
                    row.OriginalDate, null);
            }

            // The weekday is derived from the date. We keep the original value for audit,
            // but the calculated value is safe to use as an automatic correction.
            if (!string.IsNullOrWhiteSpace(row.OriginalDay) &&
                !DayMatches(row.OriginalDay, date.DayOfWeek))
            {
                Add(issues, row, "Day", "DAY_DATE_MISMATCH",
                    ImportIssueSeverities.Warning, false,
                    "The weekday in Excel does not match the crash date. The calculated weekday is suggested.",
                    row.OriginalDay, date.DayOfWeek.ToString().ToUpperInvariant());
            }
        }

        if (!string.IsNullOrWhiteSpace(row.OriginalStation) && row.Station is not null &&
            !NormalisedEquals(row.OriginalStation, row.Station))
        {
            // This is informational because the parser only applies deterministic cleanup
            // here: trim, uppercase, collapse spaces and remove an appended CAS reference.
            Add(issues, row, "Station", "STATION_NORMALISED",
                ImportIssueSeverities.Information, false,
                "Station formatting was normalised automatically.", row.OriginalStation, row.Station,
                ImportIssueResolutionStatuses.Corrected);
        }

        if (string.IsNullOrWhiteSpace(row.VehiclesString))
        {
            Add(issues, row, "VehiclesString", "VEHICLES_MISSING",
                ImportIssueSeverities.Warning, false,
                "No vehicle description was supplied; vehicle analysis will be incomplete.",
                row.OriginalVehicles, null);
        }

        ValidateCasualtyTotals(issues, row);
        return issues;
    }

    private static void ValidateCasualtyTotals(List<ImportDataQualityIssue> issues, StagingCrashSummary row)
    {
        // byte? fields cannot be negative. This rule catches an unusually large total that
        // is more likely to be a shifted Excel column or typing error than a real crash.
        var total = Values(row).Sum(value => value ?? 0);
        if (total > 200)
        {
            Add(issues, row, "Casualties", "CASUALTY_TOTAL_IMPLAUSIBLE",
                ImportIssueSeverities.Warning, false,
                $"The row contains {total} casualties. Confirm that the injury columns were mapped correctly.",
                total.ToString(), null);
        }
    }

    private static IEnumerable<byte?> Values(StagingCrashSummary row)
    {
        yield return row.FatalDrivers; yield return row.FatalPassengers;
        yield return row.FatalPedestrians; yield return row.FatalCyclists;
        yield return row.SeriousDrivers; yield return row.SeriousPassengers;
        yield return row.SeriousPedestrians; yield return row.SeriousCyclists;
        yield return row.SlightDrivers; yield return row.SlightPassengers;
        yield return row.SlightPedestrians; yield return row.SlightCyclists;
    }

    private static void Required(List<ImportDataQualityIssue> issues, StagingCrashSummary row,
        string field, string? original, string? cleaned)
    {
        if (string.IsNullOrWhiteSpace(cleaned))
            Add(issues, row, field, $"{field.ToUpperInvariant()}_MISSING",
                ImportIssueSeverities.Error, true,
                $"{field} is required before this row can be imported.", original, null);
    }

    private static void Recommended(List<ImportDataQualityIssue> issues, StagingCrashSummary row,
        string field, string? original, string? cleaned)
    {
        if (string.IsNullOrWhiteSpace(cleaned))
            Add(issues, row, field, $"{field.ToUpperInvariant()}_MISSING",
                ImportIssueSeverities.Warning, false,
                $"{field} is missing; related analysis will be incomplete.", original, null);
    }

    private static void Add(List<ImportDataQualityIssue> issues, StagingCrashSummary row,
        string field, string code, string severity, bool blocking, string description,
        string? original, string? suggested,
        string resolution = ImportIssueResolutionStatuses.Open) => issues.Add(new ImportDataQualityIssue
        {
            ImportBatchId = row.ImportBatchId,
            StagingSummary = row,
            FieldName = field,
            IssueCode = code,
            Severity = severity,
            IsBlocking = blocking,
            Description = description,
            OriginalValue = original,
            SuggestedValue = suggested,
            ResolutionStatus = resolution,
            CreatedAt = DateTime.UtcNow
        });

    private static bool DayMatches(string value, DayOfWeek day)
    {
        // Source workbooks use a mixture of full names, three-letter names and
        // compact operational abbreviations. Convert all supported forms to the
        // same DayOfWeek value before comparing them with the crash date.
        var normalised = value.Trim()
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();

        var parsedDay = normalised switch
        {
            "SU" or "SUN" or "SUNDAY" => DayOfWeek.Sunday,
            "M" or "MO" or "MON" or "MONDAY" => DayOfWeek.Monday,
            "TU" or "TUE" or "TUES" or "TUESDAY" => DayOfWeek.Tuesday,
            "W" or "WE" or "WED" or "WEDNESDAY" => DayOfWeek.Wednesday,
            "TH" or "THU" or "THUR" or "THURS" or "THURSDAY" => DayOfWeek.Thursday,
            "F" or "FR" or "FRI" or "FRIDAY" => DayOfWeek.Friday,
            "SA" or "SAT" or "SATURDAY" => DayOfWeek.Saturday,
            _ => (DayOfWeek?)null
        };

        // Unknown day text still produces DAY_DATE_MISMATCH, which is important:
        // accepting known abbreviations must not silently accept typing errors.
        return parsedDay == day;
    }

    private static bool NormalisedEquals(string left, string right) =>
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}
