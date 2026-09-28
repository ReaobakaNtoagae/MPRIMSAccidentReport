using CrashReport.Data;
using CrashReport.ViewModels;

namespace CrashReport.Services
{
    public class QuarterlyReportDataService : MonthlyMemoDataService
    {
        public QuarterlyReportDataService(AppDbContext context, IStationDistrictLookup stationDistrict)
            : base(context, stationDistrict) { }

        private static readonly string[] QuarterLabel =
        {
            "", "JAN–MAR", "APR–JUN", "JUL–SEP", "OCT–DEC"
        };

        public static (DateOnly From, DateOnly To) GetQuarterRange(int year, int quarter)
        {
            if (quarter is < 1 or > 4)
                throw new ArgumentOutOfRangeException(nameof(quarter), "Quarter must be between 1 and 4.");

            var startMonth = (quarter - 1) * 3 + 1;
            var from = new DateOnly(year, startMonth, 1);
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

            var (from, to) = GetQuarterRange(req.Year, req.Quarter);

            return await BuildCoreAsync(
                from, to, from.AddYears(-1), to.AddYears(-1),
                monthYear: $"Q{req.Quarter} {req.Year} ({QuarterLabel[req.Quarter]})",
                monthName: $"QUARTER {req.Quarter} ({QuarterLabel[req.Quarter]})",
                province: req.ProvinceCode ?? "MP",
                reportDate: req.ReportDate, refNumber: req.RefNumber,
                enquiryName: req.EnquiryName, enquiryTel: req.EnquiryTel,
                toName: req.ToName, toTitle: req.ToTitle,
                fromName: req.FromName, fromTitle: req.FromTitle);
        }
    }
}
