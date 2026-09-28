using CrashReport.Models;
using CrashReport.Models.Dtos;

namespace CrashReport.Services;

public class CrashGridService : ICrashGridService
{
    private readonly MonthlyMemoDataService _memoData;

    public CrashGridService(MonthlyMemoDataService memoData)
    {
        _memoData = memoData;
    }

    public async Task<CrashGridResult> BuildAsync(CrashGridFilter filter)
    {
        var rows = await _memoData.LoadAsync(filter.From, filter.To);

        if (!string.IsNullOrWhiteSpace(filter.District))
            rows = rows.Where(r => string.Equals(r.District, filter.District, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(filter.Severity))
            rows = rows.Where(r => string.Equals(r.OverallSeverity, filter.Severity, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(filter.Source))
            rows = rows.Where(r => string.Equals(r.Source, filter.Source, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(filter.Station))
            rows = rows.Where(r => r.Station.Contains(filter.Station.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(filter.Route))
            rows = rows.Where(r => r.Route.Contains(filter.Route.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(filter.CrashType))
            rows = rows.Where(r => r.CrashType.Contains(filter.CrashType.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            rows = rows.Where(r =>
                r.CrNo.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.CasNo.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.ArNo.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.Route.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.Station.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.Location.Contains(term, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        rows = Sort(rows, filter.SortBy, filter.SortDesc);

        var total = rows.Count;
        var page = Math.Max(filter.Page, 1);
        var pageSize = filter.PageSize <= 0 ? 50 : filter.PageSize;

        var paged = rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new CrashGridRowDto
            {
                CrashId = r.CrashId,
                SummaryId = r.SummaryId,
                CrNo = r.CrNo,
                CasNo = r.CasNo,
                ArNo = r.ArNo,
                Station = r.Station,
                District = r.District,
                Date = r.Date.ToString("yyyy-MM-dd"),
                Time = r.Time?.ToString("HH:mm"),
                Route = r.Route,
                Location = r.Location,
                CrashType = r.CrashType,
                VehicleCount = r.VehicleCount,
                Fatalities = r.Fatalities,
                Serious = r.Serious,
                Slight = r.Slight,
                Severity = r.OverallSeverity,
                Source = r.Source
            })
            .ToList();

        return new CrashGridResult { Total = total, Page = page, PageSize = pageSize, Rows = paged };
    }

    private static List<Row> Sort(List<Row> rows, string sortBy, bool desc)
    {
        Func<Row, object> key = sortBy switch
        {
            "CrNo" => r => r.CrNo,
            "District" => r => r.District,
            "Station" => r => r.Station,
            "Severity" => r => r.OverallSeverity,
            "Source" => r => r.Source,
            _ => r => r.Date
        };

        return desc
            ? rows.OrderByDescending(key).ToList()
            : rows.OrderBy(key).ToList();
    }
}
