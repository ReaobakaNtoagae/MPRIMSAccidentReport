using CrashReport.Data;
using CrashReport.ViewModels;

namespace CrashReport.Services
{
    public class QuarterlyReportDataService : MonthlyMemoDataService
    {
        public QuarterlyReportDataService(AppDbContext context) : base(context) { }

        private static readonly string[] QuarterLabel =
        {
            "", "JAN\u2013MAR", "APR\u2013JUN", "JUL\u2013SEP", "OCT\u2013DEC"
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

        public async Task<MonthlyMemoViewModel> BuildAsync(QuarterlyReportRequest req)
        {
            if (req.Quarter is < 1 or > 4)
                throw new ArgumentOutOfRangeException(nameof(req.Quarter), "Quarter must be between 1 and 4.");

            var (from, to) = GetQuarterRange(req.Year, req.Quarter);
            var pFrom = from.AddYears(-1);
            var pTo = to.AddYears(-1);
            var days = (to.DayNumber - from.DayNumber) + 1;

            var vm = new MonthlyMemoViewModel
            {
                MonthYear = $"Q{req.Quarter} {req.Year} ({QuarterLabel[req.Quarter]})",
                MonthName = $"QUARTER {req.Quarter} ({QuarterLabel[req.Quarter]})",
                PeriodFrom = FormatDate(from),
                PeriodTo = FormatDate(to),
                PriorFrom = FormatDate(pFrom),
                PriorTo = FormatDate(pTo),
                CurrentYear = from.Year,
                PriorYear = pFrom.Year,
                DaysInPeriod = days,
                ReportDate = string.IsNullOrEmpty(req.ReportDate)
                               ? DateTime.Today.ToString("dd MMMM yyyy").ToUpper()
                               : req.ReportDate.ToUpper(),
                RefNumber = req.RefNumber,
                EnquiryName = req.EnquiryName,
                EnquiryTel = req.EnquiryTel,
                ToName = req.ToName,
                ToTitle = req.ToTitle,
                FromName = req.FromName,
                FromTitle = req.FromTitle
            };

            var current = await LoadAsync(from, to);
            var prior = await LoadAsync(pFrom, pTo);

            vm.Provincial.Current = Agg(current);
            vm.Provincial.Prior = Agg(prior);

            for (int y = req.Year - 4; y <= req.Year; y++)
            {
                var (yFrom, yTo) = GetQuarterRange(y, req.Quarter);
                var yC = await LoadAsync(yFrom, yTo);
                vm.FiveYearHistory.Add(new YearHistory
                {
                    Year = y,
                    Crashes = yC.Count,
                    Fatalities = yC.Sum(c => c.Fatalities)
                });
            }

            foreach (var (key, name, stations) in Districts)
            {
                var dC = current.Where(r => stations.Contains(r.Station)).ToList();
                var dP = prior.Where(r => stations.Contains(r.Station)).ToList();
                vm.Districts.Add(new DistrictMemoStats
                {
                    Key = key,
                    Name = name,
                    Current = Agg(dC),
                    Prior = Agg(dP),
                    Routes = BuildRoutes(dC, dP)
                });
            }

            vm.ProvincialRoutes = BuildRoutes(current, prior)
                .OrderByDescending(r => r.FatalCurr)
                .ThenByDescending(r => r.CrashesCurr)
                .Take(6).ToList();

            vm.CrashTypes = BuildCrashTypes(current, prior);
            vm.VehicleCategories = BuildVehicleCats(current, prior);
            vm.TimeSlots = BuildTimeSlots(current, prior);

            vm.DaysOfWeek["Provincial"] = BuildDays(current, prior);

            foreach (var (key, name, stations) in Districts)
            {
                var dC = current.Where(r => stations.Contains(r.Station)).ToList();
                var dP = prior.Where(r => stations.Contains(r.Station)).ToList();
                vm.DaysOfWeek[key] = BuildDays(dC, dP);
            }
            return vm;

        }
    }
}
