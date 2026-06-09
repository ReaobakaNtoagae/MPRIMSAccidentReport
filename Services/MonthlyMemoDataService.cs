using CrashReport.Data;
using CrashReport.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Services;

public class MonthlyMemoDataService
{
    private readonly AppDbContext _context;

    private static readonly (string key, string name, HashSet<string> stations)[] Districts =
    [
        ("EhlanzeniSouth", "EHLANZENI SOUTH", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TONGA","WHITE RIVER","NELSPRUIT","MASOYI","MATSULU","NGODWANA",
            "BARBERTON","KABOKWENI","KANYAMAZANE","KANAYAMAZANE","KOMATIPOORT",
            "MALALANE","SCHOEMANSDAL","KAMHLUSHWA"
        }),
        ("EhlanzeniNorth", "EHLANZENI NORTH", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ACORNHOEK","BUSHBUCKRIDGE","MHALA","GRASKOP","SABIE","HAZYVIEW","KLASERIE"
        }),
        ("GertSibande", "GERT SIBANDE", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ERMELO","SECUNDA","STANDERTON","BETHAL","BALFOUR","VOLKSRUST",
            "PIET RETIEF","WAKKERSTROOM","MORGENZON","AMSTERDAM"
        }),
        ("Nkangala", "NKANGALA", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "WITBANK","MIDDELBURG","DELMAS","OGIES","KRIEL","BELFAST",
            "CAROLINA","LEANDRA","KWAMHLANGA","BRONKHORSTSPRUIT"
        })
    ];

    public MonthlyMemoDataService(AppDbContext context) => _context = context;

    public async Task<MonthlyMemoViewModel> BuildAsync(MemoReportRequest req)
    {
        var from = req.DateFrom;
        var to = req.DateTo;
        var pFrom = req.CompareFrom;
        var pTo = req.CompareTo;
        var days = (to.DayNumber - from.DayNumber) + 1;

        var vm = new MonthlyMemoViewModel
        {
            MonthYear = FormatPeriodLabel(from, to),
            MonthName = from.Month == to.Month
                               ? from.ToString("MMMM").ToUpper()
                               : from.ToString("MMM").ToUpper() + "–" + to.ToString("MMM").ToUpper(),
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

        // 5-year history — same calendar month for each year
        if (from.Month == to.Month)
        {
            for (int y = from.Year - 4; y <= from.Year; y++)
            {
                var yF = new DateOnly(y, from.Month, 1);
                var yT = yF.AddMonths(1).AddDays(-1);
                var yC = await LoadAsync(yF, yT);
                vm.FiveYearHistory.Add(new YearHistory
                {
                    Year = y,
                    Crashes = yC.Count,
                    Fatalities = yC.Sum(r => r.Fatalities)
                });
            }
        }

        // Districts
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

        // Provincial routes
        vm.ProvincialRoutes = BuildRoutes(current, prior)
            .OrderByDescending(r => r.FatalCurr)
            .ThenByDescending(r => r.CrashesCurr)
            .Take(6).ToList();

        vm.CrashTypes = BuildCrashTypes(current, prior);
        vm.VehicleCategories = BuildVehicleCats(current, prior);
        vm.TimeSlots = BuildTimeSlots(current, prior);

        vm.DaysOfWeek["Provincial"] = BuildDays(current, prior);

        // Populate a DayStats entry for every district — always, even if all zeros
        // This ensures the JS renderer always finds a matching key
        foreach (var (key, name, stations) in Districts)
        {
            var dC = current.Where(r => stations.Contains(r.Station)).ToList();
            var dP = prior.Where(r => stations.Contains(r.Station)).ToList();
            vm.DaysOfWeek[key] = BuildDays(dC, dP);
        }

        return vm;
    }

    // ─────────────────────────────────────────────────────────
    private async Task<List<Row>> LoadAsync(DateOnly from, DateOnly to)
    {
        var crashes = await _context.Crashes
            .Include(c => c.CrashConditions)
            .Include(c => c.CrashVehicles).ThenInclude(cv => cv.Vehicle)
            .Include(c => c.CrashPeople)
            .Where(c => c.CrashDate >= from && c.CrashDate <= to)
            .ToListAsync();

        return crashes.Select(c =>
        {
            var people = c.CrashPeople.ToList();
            var cond = c.CrashConditions.FirstOrDefault();
            var vCats = c.CrashVehicles
                            .Select(v => v.Vehicle?.VehicleCategory ?? "")
                            .ToList();

            bool Has(string role, string sev) =>
                people.Any(p =>
                    string.Equals(p.Role, role, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.SeverityOfInjury, sev, StringComparison.OrdinalIgnoreCase));

            int Count(string role, string sev) =>
                people.Count(p =>
                    string.Equals(p.Role, role, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.SeverityOfInjury, sev, StringComparison.OrdinalIgnoreCase));

            return new Row
            {
                Station = ExtractStation(c.CrNo),
                Date = c.CrashDate,
                Time = c.CrashTime,
                Route = c.RoadNumber ?? "",
                CrashType = cond?.CrashType ?? "",
                VehicleCats = vCats,

                Fatalities = people.Count(p => p.SeverityOfInjury == "Fatal"),
                Serious = people.Count(p => p.SeverityOfInjury == "Serious"),
                Slight = people.Count(p => p.SeverityOfInjury == "Slight"),

                FatalDrivers = Count("Driver", "Fatal"),
                FatalPassengers = Count("Passenger", "Fatal"),
                FatalPedestrians = Count("Pedestrian", "Fatal"),
                FatalCyclists = Count("Bicyclist", "Fatal"),
                SeriousDrivers = Count("Driver", "Serious"),
                SeriousPassengers = Count("Passenger", "Serious"),
                SeriousPedestrians = Count("Pedestrian", "Serious"),
                SeriousCyclists = Count("Bicyclist", "Serious"),
                SlightDrivers = Count("Driver", "Slight"),
                SlightPassengers = Count("Passenger", "Slight"),
                SlightPedestrians = Count("Pedestrian", "Slight"),
                SlightCyclists = Count("Bicyclist", "Slight"),
            };
        }).ToList();
    }

    private static PeriodStatsBlock Agg(List<Row> r) => new()
    {
        Crashes = r.Count,
        Fatalities = r.Sum(x => x.Fatalities),
        Serious = r.Sum(x => x.Serious),
        Slight = r.Sum(x => x.Slight),
        FatalDrivers = r.Sum(x => x.FatalDrivers),
        FatalPassengers = r.Sum(x => x.FatalPassengers),
        FatalPedestrians = r.Sum(x => x.FatalPedestrians),
        FatalCyclists = r.Sum(x => x.FatalCyclists),
        SeriousDrivers = r.Sum(x => x.SeriousDrivers),
        SeriousPassengers = r.Sum(x => x.SeriousPassengers),
        SeriousPedestrians = r.Sum(x => x.SeriousPedestrians),
        SeriousCyclists = r.Sum(x => x.SeriousCyclists),
        SlightDrivers = r.Sum(x => x.SlightDrivers),
        SlightPassengers = r.Sum(x => x.SlightPassengers),
        SlightPedestrians = r.Sum(x => x.SlightPedestrians),
        SlightCyclists = r.Sum(x => x.SlightCyclists)
    };

    private static List<RouteStats> BuildRoutes(List<Row> curr, List<Row> prior)
    {
        var c = curr.Where(r => !string.IsNullOrEmpty(r.Route))
                    .GroupBy(r => r.Route)
                    .ToDictionary(g => g.Key,
                        g => (g.Count(), g.Sum(x => x.Fatalities)));
        var p = prior.Where(r => !string.IsNullOrEmpty(r.Route))
                     .GroupBy(r => r.Route)
                     .ToDictionary(g => g.Key,
                         g => (g.Count(), g.Sum(x => x.Fatalities)));

        return c.Keys.Union(p.Keys)
            .Select(route =>
            {
                c.TryGetValue(route, out var cv);
                p.TryGetValue(route, out var pv);
                return new RouteStats
                {
                    Route = route,
                    CrashesCurr = cv.Item1,
                    FatalCurr = cv.Item2,
                    CrashesPrev = pv.Item1,
                    FatalPrev = pv.Item2
                };
            })
            .Where(r => r.CrashesCurr >= 2 || r.FatalCurr >= 1)
            .OrderByDescending(r => r.FatalCurr)
            .ToList();
    }

    private static List<CrashTypeStats> BuildCrashTypes(List<Row> curr, List<Row> prior)
    {
        var types = new[]
        {
            "PEDESTRIAN","HEAD ON","LOST CONTROL","SIDESWIPE",
            "OVERTURNED","FIXED OBJECT","HEAD REAR","REAR END"
        };
        return types.Select(t =>
        {
            var c = curr.Where(r => string.Equals(r.CrashType, t, StringComparison.OrdinalIgnoreCase));
            var p = prior.Where(r => string.Equals(r.CrashType, t, StringComparison.OrdinalIgnoreCase));
            return new CrashTypeStats
            {
                Type = t,
                CrashesCurr = c.Count(),
                FatalCurr = c.Sum(r => r.Fatalities),
                CrashesPrev = p.Count(),
                FatalPrev = p.Sum(r => r.Fatalities)
            };
        })
        .Where(x => x.CrashesCurr > 0 || x.CrashesPrev > 0)
        .OrderByDescending(x => x.FatalCurr).ToList();
    }

    private static List<VehicleCategoryStats> BuildVehicleCats(List<Row> curr, List<Row> prior)
    {
        var cats = new[]
        {
            ("SEDANS","Passenger"), ("LDV","Goods"), ("TAXI'S","Taxi"),
            ("TRUCKS","Truck"), ("MOTORCYCLES","Motorcycle"),
            ("BICYCLE","Bicycle"), ("ARTICULATED","Articulated"), ("BUSSES","Bus")
        };
        return cats.Select(cat =>
        {
            bool Match(Row r) => r.VehicleCats.Any(v =>
                string.Equals(v, cat.Item2, StringComparison.OrdinalIgnoreCase));
            var c = curr.Where(Match);
            var p = prior.Where(Match);
            return new VehicleCategoryStats
            {
                Category = cat.Item1,
                CrashesCurr = c.Count(),
                FatalCurr = c.Sum(r => r.Fatalities),
                CrashesPrev = p.Count(),
                FatalPrev = p.Sum(r => r.Fatalities)
            };
        })
        .Where(x => x.CrashesCurr > 0 || x.CrashesPrev > 0).ToList();
    }

    private static List<TimeSlotStats> BuildTimeSlots(List<Row> curr, List<Row> prior)
    {
        var slots = new[]
        {
            ("06H00 - 14H00", 6,  14),
            ("14H00 – 22H00", 14, 22),
            ("22H00 – 06H00", 22, 6)
        };
        return slots.Select(s =>
        {
            var c = curr.Where(r => InSlot(r.Time, s.Item2, s.Item3));
            var p = prior.Where(r => InSlot(r.Time, s.Item2, s.Item3));
            return new TimeSlotStats
            {
                Slot = s.Item1,
                CrashesCurr = c.Count(),
                FatalCurr = c.Sum(r => r.Fatalities),
                CrashesPrev = p.Count(),
                FatalPrev = p.Sum(r => r.Fatalities)
            };
        }).ToList();
    }

    private static List<DayStats> BuildDays(List<Row> curr, List<Row> prior)
    {
        var days = new[]
        {
            ("MONDAYS",    DayOfWeek.Monday),  ("TUESDAYS",   DayOfWeek.Tuesday),
            ("WEDNESDAYS", DayOfWeek.Wednesday),("THURSDAYS", DayOfWeek.Thursday),
            ("FRIDAYS",    DayOfWeek.Friday),   ("SATURDAYS", DayOfWeek.Saturday),
            ("SUNDAYS",    DayOfWeek.Sunday)
        };
        return days.Select(d =>
        {
            var c = curr.Where(r => r.Date.DayOfWeek == d.Item2);
            var p = prior.Where(r => r.Date.DayOfWeek == d.Item2);
            return new DayStats
            {
                Day = d.Item1,
                CrashesCurr = c.Count(),
                FatalCurr = c.Sum(r => r.Fatalities),
                CrashesPrev = p.Count(),
                FatalPrev = p.Sum(r => r.Fatalities)
            };
        }).ToList();
    }

    private static bool InSlot(TimeOnly? t, int start, int end)
    {
        if (!t.HasValue) return false;
        var h = t.Value.Hour;
        return start < end ? h >= start && h < end : h >= start || h < end;
    }

    private static string ExtractStation(string? crNo)
        => string.IsNullOrEmpty(crNo) ? "" :
           crNo.Contains('-') ? crNo.Split('-')[0].Trim() : crNo.Trim();

    private static string FormatDate(DateOnly d)
    {
        var months = new[] { "","JANUARY","FEBRUARY","MARCH","APRIL","MAY","JUNE",
                             "JULY","AUGUST","SEPTEMBER","OCTOBER","NOVEMBER","DECEMBER" };
        return $"{d.Day} {months[d.Month]} {d.Year}";
    }

    private static string FormatPeriodLabel(DateOnly from, DateOnly to)
    {
        var months = new[] { "","January","February","March","April","May","June",
                             "July","August","September","October","November","December" };
        return from.Month == to.Month && from.Year == to.Year
            ? $"{months[from.Month]} {from.Year}"
            : $"{months[from.Month]}–{months[to.Month]} {to.Year}";
    }

    private class Row
    {
        public string Station { get; set; } = "";
        public DateOnly Date { get; set; }
        public TimeOnly? Time { get; set; }
        public string Route { get; set; } = "";
        public string CrashType { get; set; } = "";
        public List<string> VehicleCats { get; set; } = new();
        public int Fatalities { get; set; }
        public int Serious { get; set; }
        public int Slight { get; set; }
        public int FatalDrivers { get; set; }
        public int FatalPassengers { get; set; }
        public int FatalPedestrians { get; set; }
        public int FatalCyclists { get; set; }
        public int SeriousDrivers { get; set; }
        public int SeriousPassengers { get; set; }
        public int SeriousPedestrians { get; set; }
        public int SeriousCyclists { get; set; }
        public int SlightDrivers { get; set; }
        public int SlightPassengers { get; set; }
        public int SlightPedestrians { get; set; }
        public int SlightCyclists { get; set; }
    }
}