namespace CrashReport.Models;
public interface IPeriodReportResolver
{
    ResolvedReportPeriod Resolve(PeriodReportRequest request);
}

public sealed class PeriodReportResolver : IPeriodReportResolver
{
    public ResolvedReportPeriod Resolve(PeriodReportRequest request)
    {
        if (request.Year is < 2000 or > 2100)
            throw new ArgumentOutOfRangeException(nameof(request.Year));

        var current = request.ReportType switch
        {
            PeriodReportType.Monthly => Month(request.Year, RequiredMonth(request.Month)),
            PeriodReportType.Quarterly => Quarter(request.Year, RequiredQuarter(request.Quarter)),
            PeriodReportType.SixMonths => MonthsFromJanuary(request.Year, 6),
            PeriodReportType.NineMonths => MonthsFromJanuary(request.Year, 9),
            PeriodReportType.Annual => MonthsFromJanuary(request.Year, 12),
            _ => throw new ArgumentOutOfRangeException(nameof(request.ReportType))
        };
        var comparison = ShiftYear(current, -1);
        var history = Enumerable.Range(request.Year - 4, 5)
            .Select(year => Rebase(current, year)).ToArray();

        return new ResolvedReportPeriod(
            request.ReportType,
            BuildTitle(request),
            BuildLabel(request),
            current,
            comparison,
            history);
    }

    private static DateRange Month(int year, int month)
    {
        var from = new DateOnly(year, month, 1);
        return new(from, from.AddMonths(1).AddDays(-1));
    }

    private static DateRange Quarter(int year, int quarter)
    {
        var from = new DateOnly(year, ((quarter - 1) * 3) + 1, 1);
        return new(from, from.AddMonths(3).AddDays(-1));
    }

    private static DateRange MonthsFromJanuary(int year, int months)
    {
        var from = new DateOnly(year, 1, 1);
        return new(from, from.AddMonths(months).AddDays(-1));
    }

    private static DateRange ShiftYear(DateRange range, int years) =>
        new(range.From.AddYears(years), range.To.AddYears(years));

    private static DateRange Rebase(DateRange range, int year)
    {
        var from = new DateOnly(year, range.From.Month, range.From.Day);
        var toDay = Math.Min(range.To.Day, DateTime.DaysInMonth(year, range.To.Month));
        return new(from, new DateOnly(year, range.To.Month, toDay));
    }

    private static int RequiredMonth(int? value) => value is >= 1 and <= 12
        ? value.Value : throw new ArgumentOutOfRangeException(nameof(value), "Month must be between 1 and 12.");

    private static int RequiredQuarter(int? value) => value is >= 1 and <= 4
        ? value.Value : throw new ArgumentOutOfRangeException(nameof(value), "Quarter must be between 1 and 4.");

    private static string BuildTitle(PeriodReportRequest request) => request.ReportType switch
    {
        PeriodReportType.Monthly => $"{new DateTime(request.Year, RequiredMonth(request.Month), 1):MMMM yyyy} MONTHLY MEMORANDUM".ToUpperInvariant(),
        PeriodReportType.Quarterly => $"QUARTER {RequiredQuarter(request.Quarter)} {request.Year} REPORT",
        PeriodReportType.SixMonths => $"SIX-MONTH REPORT: JANUARY–JUNE {request.Year}",
        PeriodReportType.NineMonths => $"NINE-MONTH REPORT: JANUARY–SEPTEMBER {request.Year}",
        PeriodReportType.Annual => $"ANNUAL REPORT: JANUARY–DECEMBER {request.Year}",
        _ => throw new ArgumentOutOfRangeException()
    };

    private static string BuildLabel(PeriodReportRequest request) => request.ReportType switch
    {
        PeriodReportType.Monthly => $"{new DateTime(request.Year, RequiredMonth(request.Month), 1):MMMM yyyy}",
        PeriodReportType.Quarterly => $"Q{RequiredQuarter(request.Quarter)} {request.Year}",
        PeriodReportType.SixMonths => $"January–June {request.Year}",
        PeriodReportType.NineMonths => $"January–September {request.Year}",
        PeriodReportType.Annual => $"January–December {request.Year}",
        _ => throw new ArgumentOutOfRangeException()
    };
}
