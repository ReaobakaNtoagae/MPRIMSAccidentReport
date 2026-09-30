using CrashReport.Data;
using CrashReport.ViewModels;

namespace CrashReport.Services
{
    public class QuarterlyReportDataService : MonthlyMemoDataService
    {
        public QuarterlyReportDataService(AppDbContext context, IStationDistrictLookup stationDistrict)
            : base(context, stationDistrict) { }

        // Supports two quarter bases, selected per request via isFiscalYear:
        //  - Calendar (default): Q1=Jan-Mar(year)  Q2=Apr-Jun(year)
        //                        Q3=Jul-Sep(year)  Q4=Oct-Dec(year)
        //  - Fiscal: the department's year starts 1 April. "year" is the fiscal
        //    year's STARTING calendar year (e.g. year=2025 means FY2025/26,
        //    1 Apr 2025 - 31 Mar 2026):
        //      Q1 = Apr-Jun(year)      Q2 = Jul-Sep(year)
        //      Q3 = Oct-Dec(year)      Q4 = Jan-Mar(year + 1)
        // Labels are computed from the resolved dates rather than a static
        // lookup precisely because fiscal Q4 rolls into the following
        // calendar year — a fixed string table can't express that without
        // going stale.
        public static (DateOnly From, DateOnly To) GetQuarterRange(int year, int quarter, bool isFiscalYear = false)
        {
            if (quarter is < 1 or > 4)
                throw new ArgumentOutOfRangeException(nameof(quarter), "Quarter must be between 1 and 4.");

            var startMonth = isFiscalYear ? 4 : 1;
            var from = new DateOnly(year, startMonth, 1).AddMonths((quarter - 1) * 3);
            var to = from.AddMonths(3).AddDays(-1);
            return (from, to);
        }

        // Everything past computing the quarter's date range and labels is now shared with
        // Monthly/Six-Month/Annual via MonthlyMemoDataService.BuildCoreAsync — see that
        // method's remarks for what used to be duplicated (and drifted) here.
        public async Task<MonthlyMemoViewModel> BuildAsync(QuarterlyReportRequest req)
        {
            if (req.Quarter is < 1 or > 4)
                throw new ArgumentOutOfRangeException(nameof(req.Quarter), "Quarter must be between 1 and 4.");

            var (from, to) = GetQuarterRange(req.Year, req.Quarter, req.IsFiscalYear);
            var yearLabel = req.IsFiscalYear ? $"{req.Year}/{(req.Year + 1) % 100:D2}" : $"{req.Year}";
            var monthRangeLabel = $"{from:MMM}–{to:MMM} {to.Year}".ToUpperInvariant();

            return await BuildCoreAsync(
                from, to, from.AddYears(-1), to.AddYears(-1),
                monthYear: $"Q{req.Quarter} {yearLabel} ({monthRangeLabel})",
                monthName: $"QUARTER {req.Quarter} {yearLabel} ({monthRangeLabel})",
                province: req.ProvinceCode ?? "MP",
                reportDate: req.ReportDate, refNumber: req.RefNumber,
                enquiryName: req.EnquiryName, enquiryTel: req.EnquiryTel,
                toName: req.ToName, toTitle: req.ToTitle,
                fromName: req.FromName, fromTitle: req.FromTitle);
        }
    }
}
