using CrashReport.Models;

namespace CrashReport.Services;
public interface IReportRowSource
{
    Task<IReadOnlyList<CrashReportRow>> LoadAsync(DateRange period, CancellationToken cancellationToken = default);
}

public interface IPeriodReportService
{
    Task<PeriodReportModel> BuildAsync(PeriodReportRequest request, CancellationToken cancellationToken = default);
}

public sealed class PeriodReportService(
    IPeriodReportResolver resolver,
    IReportRowSource rows) : IPeriodReportService
{
    public async Task<PeriodReportModel> BuildAsync(
        PeriodReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var period = resolver.Resolve(request);
        var loadTasks = new[] { period.Current, period.Comparison }
            .Concat(period.FiveYearRanges)
            .Select(range => rows.LoadAsync(range, cancellationToken))
            .ToArray();
        await Task.WhenAll(loadTasks);

        var current = loadTasks[0].Result;
        var prior = loadTasks[1].Result;
        var districtNames = current.Select(x => x.District)
            .Concat(prior.Select(x => x.District))
            .Where(x => !string.IsNullOrWhiteSpace(x) && !x.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToArray();

        return new PeriodReportModel
        {
            ReportType = request.ReportType,
            ReportTitle = period.Title,
            PeriodLabel = period.PeriodLabel,
            CurrentPeriod = period.Current,
            ComparisonPeriod = period.Comparison,
            Current = Aggregate(current),
            Prior = Aggregate(prior),
            Districts = districtNames.Select(name =>
            {
                var districtCurrent = current.Where(x => x.District.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
                var districtPrior = prior.Where(x => x.District.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
                return new DistrictComparison(name, Aggregate(districtCurrent), Aggregate(districtPrior),
                    Compare(districtCurrent, districtPrior, x => x.Route).Take(6).ToArray());
            }).ToArray(),
            ProvincialRoutes = Compare(current, prior, x => x.Route).Take(6).ToArray(),
            CrashTypes = Compare(current, prior, x => x.CrashType).ToArray(),
            VehicleCategories = CompareMany(current, prior, x => x.VehicleCategories).ToArray(),
            TimeSlots = Compare(current, prior, x => TimeSlot(x.Time), includeEmpty: true).ToArray(),
            DaysOfWeek = Compare(current, prior, x => x.Date.DayOfWeek.ToString().ToUpperInvariant(), includeEmpty: true).ToArray(),
            FiveYearHistory = period.FiveYearRanges.Select((range, index) => new YearHistory(
                range.From.Year, loadTasks[index + 2].Result.Count, loadTasks[index + 2].Result.Sum(x => x.Fatalities))).ToArray(),
            Document = request
        };
    }

    private static PeriodTotals Aggregate(IEnumerable<CrashReportRow> source)
    {
        var rows = source.ToArray();
        return new(rows.Length, rows.Sum(x => x.Fatalities), rows.Sum(x => x.Serious), rows.Sum(x => x.Slight),
            rows.Sum(x => x.FatalDrivers), rows.Sum(x => x.FatalPassengers),
            rows.Sum(x => x.FatalPedestrians), rows.Sum(x => x.FatalCyclists));
    }

    private static IEnumerable<ComparisonBreakdown> Compare(
        IEnumerable<CrashReportRow> current,
        IEnumerable<CrashReportRow> prior,
        Func<CrashReportRow, string> key,
        bool includeEmpty = false)
    {
        var currentGroups = Group(current, key);
        var priorGroups = Group(prior, key);
        return currentGroups.Keys.Concat(priorGroups.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(label => includeEmpty || !string.IsNullOrWhiteSpace(label))
            .Select(label =>
            {
                currentGroups.TryGetValue(label, out var c);
                priorGroups.TryGetValue(label, out var p);
                return new ComparisonBreakdown(label, c?.Count ?? 0, p?.Count ?? 0,
                    c?.Sum(x => x.Fatalities) ?? 0, p?.Sum(x => x.Fatalities) ?? 0);
            })
            .OrderByDescending(x => x.CurrentFatalities)
            .ThenByDescending(x => x.CurrentCrashes)
            .ThenBy(x => x.Label);
    }

    private static IEnumerable<ComparisonBreakdown> CompareMany(
        IEnumerable<CrashReportRow> current,
        IEnumerable<CrashReportRow> prior,
        Func<CrashReportRow, IReadOnlyList<string>> keys)
    {
        var expandedCurrent = current.SelectMany(row => keys(row).Distinct(StringComparer.OrdinalIgnoreCase).Select(key => (key, row)));
        var expandedPrior = prior.SelectMany(row => keys(row).Distinct(StringComparer.OrdinalIgnoreCase).Select(key => (key, row)));
        var currentGroups = expandedCurrent.Where(x => !string.IsNullOrWhiteSpace(x.key))
            .GroupBy(x => x.key, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Select(x => x.row).ToList(), StringComparer.OrdinalIgnoreCase);
        var priorGroups = expandedPrior.Where(x => !string.IsNullOrWhiteSpace(x.key))
            .GroupBy(x => x.key, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Select(x => x.row).ToList(), StringComparer.OrdinalIgnoreCase);
        return currentGroups.Keys.Concat(priorGroups.Keys).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(label =>
            {
                currentGroups.TryGetValue(label, out var c); priorGroups.TryGetValue(label, out var p);
                return new ComparisonBreakdown(label, c?.Count ?? 0, p?.Count ?? 0,
                    c?.Sum(x => x.Fatalities) ?? 0, p?.Sum(x => x.Fatalities) ?? 0);
            }).OrderByDescending(x => x.CurrentFatalities).ThenByDescending(x => x.CurrentCrashes);
    }

    private static Dictionary<string, List<CrashReportRow>> Group(
        IEnumerable<CrashReportRow> source, Func<CrashReportRow, string> key) => source
        .GroupBy(row => key(row) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

    private static string TimeSlot(TimeOnly? time)
    {
        if (time is null) return "UNKNOWN";
        var hour = time.Value.Hour;
        return hour switch
        {
            >= 6 and < 14 => "06H00–14H00",
            >= 14 and < 22 => "14H00–22H00",
            _ => "22H00–06H00"
        };
    }
}
