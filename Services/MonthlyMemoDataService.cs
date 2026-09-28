using CrashReport.Data;
using CrashReport.ViewModels;
using CrashReport.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;

namespace CrashReport.Services;


public class MonthlyMemoDataService
{
    protected readonly AppDbContext _context;
    private readonly IStationDistrictLookup _stationDistrict;

    public MonthlyMemoDataService(AppDbContext context, IStationDistrictLookup stationDistrict){
        _context = context;
        _stationDistrict = stationDistrict;
    }

    public async Task<MonthlyMemoViewModel> BuildAsync(MemoReportRequest req)
    {
        var monthName = req.DateFrom.Month == req.DateTo.Month
            ? req.DateFrom.ToString("MMMM").ToUpper()
            : req.DateFrom.ToString("MMM").ToUpper() + "–" + req.DateTo.ToString("MMM").ToUpper();

        return await BuildCoreAsync(
            req.DateFrom, req.DateTo, req.CompareFrom, req.CompareTo,
            monthYear: FormatPeriodLabel(req.DateFrom, req.DateTo),
            monthName: monthName,
            province: req.ProvinceCode ?? "MP",
            reportDate: req.ReportDate, refNumber: req.RefNumber,
            enquiryName: req.EnquiryName, enquiryTel: req.EnquiryTel,
            toName: req.ToName, toTitle: req.ToTitle,
            fromName: req.FromName, fromTitle: req.FromTitle);
    }

    /// <summary>
    /// Shared orchestration behind every memo-style report — Monthly, Quarterly, and (via
    /// ReportsHubController's Jan–Jun/Jan–Dec date windows into MonthlyMemoDataService.BuildAsync)
    /// Six-Month and Annual. This used to be duplicated between this class's BuildAsync and
    /// QuarterlyReportDataService's own BuildAsync, and the two copies had already drifted:
    /// Quarterly never attached age/gender demographics; Monthly never populated
    /// vm.DaysOfWeek["Provincial"] or DistrictMemoStats.Key (so the "Days of the Week" section
    /// rendered for Quarterly but not Monthly/Six-Month/Annual); and Monthly's
    /// from.Month==to.Month guard on the 5-year history meant Six-Month/Annual silently got no
    /// Figure 2 at all. One method now gives all four report kinds the same behaviour.
    /// </summary>
    protected async Task<MonthlyMemoViewModel> BuildCoreAsync(
        DateOnly from, DateOnly to, DateOnly pFrom, DateOnly pTo,
        string monthYear, string monthName, string province,
        string reportDate, string refNumber, string enquiryName, string enquiryTel,
        string toName, string toTitle, string fromName, string fromTitle)
    {
        var days = (to.DayNumber - from.DayNumber) + 1;

        var vm = new MonthlyMemoViewModel
        {
            MonthYear = monthYear,
            MonthName = monthName,
            PeriodFrom = FormatDate(from),
            PeriodTo = FormatDate(to),
            PriorFrom = FormatDate(pFrom),
            PriorTo = FormatDate(pTo),
            CurrentYear = from.Year,
            PriorYear = pFrom.Year,
            DaysInPeriod = days,
            ReportDate = string.IsNullOrEmpty(reportDate)
                               ? DateTime.Today.ToString("dd MMMM yyyy").ToUpper()
                               : reportDate.ToUpper(),
            RefNumber = refNumber,
            EnquiryName = enquiryName,
            EnquiryTel = enquiryTel,
            ToName = toName,
            ToTitle = toTitle,
            FromName = fromName,
            FromTitle = fromTitle
        };

        // ── Load data for current and prior periods ──────────────
        var currentRows = await LoadAsync(from, to);
        var priorRows = await LoadAsync(pFrom, pTo);

        // ── Aggregate rows ──────────────────────────────────────
        var currentAgg = Agg(currentRows);
        var priorAgg = Agg(priorRows);

        // ── Fetch demographics from the dedicated table (previously Monthly-only —
        // Quarterly's separate copy of this method never called this, so Quarterly reports
        // never showed the AGE GROUP AND GENDER section) ─────────
        var currentDemo = await GetDemographicsAsync(from, to, province);
        var priorDemo = await GetDemographicsAsync(pFrom, pTo, province);
        currentAgg.FatalAgeGroups = BuildAgeGroupsFromDemographics(currentDemo);
        currentAgg.FatalGender = BuildGenderFromDemographics(currentDemo);
        priorAgg.FatalAgeGroups = BuildAgeGroupsFromDemographics(priorDemo);
        priorAgg.FatalGender = BuildGenderFromDemographics(priorDemo);

        vm.Provincial.Current = currentAgg;
        vm.Provincial.Prior = priorAgg;

        // ── Districts — discovered from whatever the data actually contains. LoadAsync
        // already resolves each row's District via IStationDistrictLookup, so grouping on
        // r.District (rather than re-querying the lookup here) needs no extra DB round trip.
        // Quarterly used to iterate a hardcoded Ehlanzeni/Bohlabela/GertSibande/Nkangala array
        // instead — the same class of hardcoded-district-list problem fixed elsewhere this
        // session, just missed in this one spot. Key is set to the district name itself, which
        // is all the per-district DaysOfWeek lookup below (and MonthlyMemoDocService's) needs.
        var districtGroups = currentRows.Concat(priorRows)
            .Select(r => r.District)
            .Where(d => !string.IsNullOrWhiteSpace(d) && !string.Equals(d, "Unknown", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .Select(name =>
            {
                var dC = currentRows.Where(r => string.Equals(r.District, name, StringComparison.OrdinalIgnoreCase)).ToList();
                var dP = priorRows.Where(r => string.Equals(r.District, name, StringComparison.OrdinalIgnoreCase)).ToList();
                return new DistrictMemoStats
                {
                    Key = name,
                    Name = name,
                    Current = Agg(dC),
                    Prior = Agg(dP),
                    Routes = BuildRoutes(dC, dP)
                };
            })
            .ToList();
        vm.Districts = districtGroups;

        // ── Provincial routes ──────────────────────────────────
        vm.ProvincialRoutes = BuildRoutes(currentRows, priorRows)
            .OrderByDescending(r => r.FatalCurr)
            .ThenByDescending(r => r.CrashesCurr)
            .Take(6).ToList();

        vm.CrashTypes = BuildCrashTypes(currentRows, priorRows);
        vm.VehicleCategories = BuildVehicleCats(currentRows, priorRows);
        vm.TimeSlots = BuildTimeSlots(currentRows, priorRows);

        // ── Days of the week — provincial row plus one per district. Quarterly used to set
        // both; Monthly used to set neither, which is why this section rendered only for
        // Quarterly reports before.
        vm.DaysOfWeek["Provincial"] = BuildDays(currentRows, priorRows);
        foreach (var dist in districtGroups)
        {
            var dC = currentRows.Where(r => string.Equals(r.District, dist.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            var dP = priorRows.Where(r => string.Equals(r.District, dist.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            vm.DaysOfWeek[dist.Key] = BuildDays(dC, dP);
        }

        // ── 5-year history — the same-length window shifted back 1 to 4 years. This replaces
        // two separate implementations (Monthly's from.Month==to.Month-gated month loop, which
        // silently produced nothing for Six-Month/Annual's Jan–Jun/Jan–Dec ranges; Quarterly's
        // own GetQuarterRange-based loop) with one formula that's correct for any period length.
        for (var yearsBack = 4; yearsBack >= 0; yearsBack--)
        {
            var yFrom = from.AddYears(-yearsBack);
            var yTo = to.AddYears(-yearsBack);
            var yRows = await LoadAsync(yFrom, yTo);
            vm.FiveYearHistory.Add(new CrashReport.ViewModels.YearHistory
            {
                Year = yFrom.Year,
                Crashes = yRows.Count,
                Fatalities = yRows.Sum(r => r.Fatalities)
            });
        }

        return vm;
    }
    public async Task<List<Row>> LoadAsync(DateOnly from, DateOnly to)
    {
        var districtMap = await _stationDistrict.GetAllAsync();
        string ResolveDistrict(string station) =>
            districtMap.TryGetValue(StationDistrictLookup.Normalize(station), out var d) ? d : "Unknown";

        // ── Source 1: real CR1 form captures ─────────────────────
        var crashes = await _context.Crashes
            .Include(c => c.CrashConditions)
            .Include(c => c.CrashVehicles).ThenInclude(cv => cv.Vehicle)
            .Include(c => c.CrashPeople)
            .Include(c => c.CrashLocations)
            .Where(c => c.CrashDate >= from && c.CrashDate <= to)
            .ToListAsync();

        var formRows = crashes.Select(c =>
        {
            var people = c.CrashPeople.ToList();
            var cond = c.CrashConditions.FirstOrDefault();
            var loc = c.CrashLocations.FirstOrDefault();
            var vCats = c.CrashVehicles
                            .Select(v => v.Vehicle?.VehicleCategory ?? "")
                            .ToList();

            var station = ExtractStation(c.CrNo);

            int Count(string role, string sev) =>
                people.Count(p =>
                    string.Equals(p.Role, role, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.SeverityOfInjury, sev, StringComparison.OrdinalIgnoreCase));

            return new Row
            {
                CrashId = c.CrashId,
                CrNo = c.CrNo ?? "",
                CasNo = c.CasNo ?? "",
                ArNo = "",
                Station = station,
                District = ResolveDistrict(station),
                ProvinceCode = c.ProvinceCode ?? "",
                Location = BuildLocation(loc),
                Source = "Manual",
                Date = c.CrashDate,
                Time = c.CrashTime,
                Route = c.RoadNumber ?? "",
                CrashType = cond?.CrashType ?? "",
                VehicleCats = vCats,
                VehicleCount = c.NoOfVehiclesInvolved ?? 0,

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

        // ── Source 2: Excel-imported summaries ────────────────────
        var summaries = await _context.CrashSummaries
            .Where(s => s.CrashDate >= from && s.CrashDate <= to)
            .ToListAsync();

        var formCrNos = formRows
            .Where(r => !string.IsNullOrEmpty(r.CrNo))
            .Select(r => r.CrNo)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var summaryRows = summaries
            .Where(s => !formCrNos.Contains(s.CrNo))
            .Select(s => new Row
            {
                CrashId = null,
                SummaryId = s.SummaryId,
                CrNo = s.CrNo,
                CasNo = s.CasNo ?? "",
                Station = s.Station,
                District = ResolveDistrict(s.Station),
                ProvinceCode = "",
                Location = s.Location ?? "",
                Source = "Import",
                Date = s.CrashDate,
                Time = s.CrashTime,
                Route = s.Route ?? "",
                CrashType = s.CrashType ?? "",
                VehicleCats = MapVehicleCodes(s.VehiclesString),
                VehicleCount = s.VehicleCount,

                Fatalities = s.Fatalities,
                Serious = s.Serious,
                Slight = s.Slight,

                FatalDrivers = s.FatalDrivers,
                FatalPassengers = s.FatalPassengers,
                FatalPedestrians = s.FatalPedestrians,
                FatalCyclists = s.FatalCyclists,
                SeriousDrivers = s.SeriousDrivers,
                SeriousPassengers = s.SeriousPassengers,
                SeriousPedestrians = s.SeriousPedestrians,
                SeriousCyclists = s.SeriousCyclists,
                SlightDrivers = s.SlightDrivers,
                SlightPassengers = s.SlightPassengers,
                SlightPedestrians = s.SlightPedestrians,
                SlightCyclists = s.SlightCyclists,
            });

        formRows.AddRange(summaryRows);
        return formRows;
    }
    private static string BuildLocation(CrashLocation? loc)
    {
        if (loc == null) return "";
        var parts = new[] { loc.StreetRoadName, loc.Suburb, loc.CityTown }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(", ", parts);
    }

    private static string ExtractStation(string? crNo)
    {
        if (string.IsNullOrWhiteSpace(crNo)) return "";
        var trimmed = crNo.Trim();
        var dashIndex = trimmed.IndexOf('-');
        return dashIndex > 0 ? trimmed.Substring(0, dashIndex).Trim() : trimmed;
    }
    
    protected static List<string> MapVehicleCodes(string? involved)
    {
        if (string.IsNullOrWhiteSpace(involved)) return new List<string>();

        var codeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SED"] = "Passenger", ["SEDAN"] = "Passenger", ["SUV"] = "Passenger",
            ["LDV"] = "Goods", ["BAKKIE"] = "Goods",
            ["TAXI"] = "Taxi",
            ["TRUCK"] = "Truck",
            ["M/C"] = "Motorcycle", ["MC"] = "Motorcycle",
            ["BUS"] = "Bus",
            ["ARTIC"] = "Articulated",
            ["CYCLE"] = "Bicycle", ["BICYCLE"] = "Bicycle",
        };

        return involved
            .Split('/')
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p)
                        && !p.Equals("P/D", StringComparison.OrdinalIgnoreCase)
                        && !p.Equals("HIT N RUN", StringComparison.OrdinalIgnoreCase)
                        && !p.Equals("HIT & RUN", StringComparison.OrdinalIgnoreCase))
            .Select(p => codeMap.TryGetValue(p, out var mapped) ? mapped : p)
            .ToList();
    }

    protected static PeriodStatsBlock Agg(List<Row> r) => new()
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

    private async Task<CrashDemographicRecord?> GetDemographicsAsync(DateOnly from, DateOnly to, string province)
    {
        return await _context.CrashDemographics
            .FirstOrDefaultAsync(d => d.PeriodFrom == from && d.PeriodTo == to && d.ProvinceCode == province);
    }

    private static Dictionary<string, int> BuildAgeGroupsFromDemographics(CrashDemographicRecord? demo)
    {
        if (demo == null) return new Dictionary<string, int>();
        return new Dictionary<string, int>
        {
            ["0-7"] = demo.Age0to7,
            ["8-12"] = demo.Age8to12,
            ["13-18"] = demo.Age13to18,
            ["19-35"] = demo.Age19to35,
            ["36+"] = demo.Age36Plus
        };
    }

    private static Dictionary<string, Dictionary<string, int>> BuildGenderFromDemographics(CrashDemographicRecord? demo)
    {
        if (demo == null) return new Dictionary<string, Dictionary<string, int>>();
        return new Dictionary<string, Dictionary<string, int>>
        {
            ["Drivers"] = new() { ["Male"] = demo.DriverMale, ["Female"] = demo.DriverFemale },
            ["Passengers"] = new() { ["Male"] = demo.PassengerMale, ["Female"] = demo.PassengerFemale },
            ["Pedestrians"] = new() { ["Male"] = demo.PedestrianMale, ["Female"] = demo.PedestrianFemale },
            ["Cyclists"] = new() { ["Male"] = demo.CyclistMale, ["Female"] = demo.CyclistFemale }
        };
    }

    protected static List<RouteStats> BuildRoutes(List<Row> curr, List<Row> prior)
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

    protected static List<CrashTypeStats> BuildCrashTypes(List<Row> curr, List<Row> prior)
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

    protected static List<VehicleCategoryStats> BuildVehicleCats(List<Row> curr, List<Row> prior)
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

    protected static List<TimeSlotStats> BuildTimeSlots(List<Row> curr, List<Row> prior)
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

    protected static List<DayStats> BuildDays(List<Row> curr, List<Row> prior)
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

    protected static bool InSlot(TimeOnly? t, int start, int end)
    {
        if (!t.HasValue) return false;
        var h = t.Value.Hour;
        return start < end ? h >= start && h < end : h >= start || h < end;
    }

 
    protected static string FormatDate(DateOnly d)
    {
        var months = new[] { "","JANUARY","FEBRUARY","MARCH","APRIL","MAY","JUNE",
                             "JULY","AUGUST","SEPTEMBER","OCTOBER","NOVEMBER","DECEMBER" };
        return $"{d.Day} {months[d.Month]} {d.Year}";
    }

    protected static string FormatPeriodLabel(DateOnly from, DateOnly to)
    {
        var months = new[] { "","January","February","March","April","May","June",
                             "July","August","September","October","November","December" };
        return from.Month == to.Month && from.Year == to.Year
            ? $"{months[from.Month]} {from.Year}"
            : $"{months[from.Month]}–{months[to.Month]} {to.Year}";
    }

    private class FatalPerson
    {
        public int Age { get; set; }
        public string Gender { get; set; } = "";
        public string Role { get; set; } = "";
    }

   
    protected static List<StationStats> BuildStations(List<Row> curr, List<Row> prior)
    {
        var c = curr.Where(r => !string.IsNullOrEmpty(r.Station))
                    .GroupBy(r => r.Station)
                    .ToDictionary(g => g.Key, g => (g.Count(), g.Sum(x => x.Fatalities)));
        var p = prior.Where(r => !string.IsNullOrEmpty(r.Station))
                     .GroupBy(r => r.Station)
                     .ToDictionary(g => g.Key, g => (g.Count(), g.Sum(x => x.Fatalities)));

        return c.Keys.Union(p.Keys)
            .Select(station =>
            {
                c.TryGetValue(station, out var cv);
                p.TryGetValue(station, out var pv);
                return new StationStats
                {
                    Station = station,
                    CrashesCurr = cv.Item1,
                    FatalCurr = cv.Item2,
                    CrashesPrev = pv.Item1,
                    FatalPrev = pv.Item2
                };
            })
            .Where(s => s.CrashesCurr >= 2 || s.FatalCurr >= 1)
            .OrderByDescending(s => s.CrashesCurr)
            .ToList();
    }


    // ── District load — how many crashes each district accounted for in the
    // current period. Grouped straight off Row.District, which LoadAsync
    // already resolves per-station via IStationDistrictLookup, so this needs
    // no new lookups of its own. Only meaningful for an unscoped (province-
    // wide) view -- a single-district scope would just be one bar -- so the
    // view only renders this when DashboardMode is "analytics".
    protected static List<DistrictLoadStats> BuildDistrictLoad(List<Row> curr)
    {
        return curr
            .Where(r => !string.IsNullOrEmpty(r.District) && r.District != "Unknown")
            .GroupBy(r => r.District)
            .Select(g => new DistrictLoadStats
            {
                District = g.Key,
                CrashesCurr = g.Count(),
                FatalCurr = g.Sum(x => x.Fatalities)
            })
            .OrderByDescending(d => d.CrashesCurr)
            .ToList();
    }

    // ── Severity mix — how the current period's casualties split across
    // fatal/serious/slight, independent of crash volume. Row already carries
    // per-crash Fatalities/Serious/Slight counts (same fields MonthlyMemo
    // uses), so this is a straight sum, not a new query.
    protected static SeverityMixStats BuildSeverityMix(List<Row> curr)
    {
        return new SeverityMixStats
        {
            Fatal = curr.Sum(r => r.Fatalities),
            Serious = curr.Sum(r => r.Serious),
            Slight = curr.Sum(r => r.Slight)
        };
    }

    public async Task<InsightsViewModel> BuildInsightsAsync(
        DateOnly from, DateOnly to, string? scopeDistrict = null, string? scopeStation = null)
    {
        var priorFrom = from.AddYears(-1);
        var priorTo = to.AddYears(-1);

        var currentRows = await LoadAsync(from, to);
        var priorRows = await LoadAsync(priorFrom, priorTo);

        if (!string.IsNullOrEmpty(scopeStation))
        {
            currentRows = currentRows.Where(r => string.Equals(r.Station, scopeStation, StringComparison.OrdinalIgnoreCase)).ToList();
            priorRows = priorRows.Where(r => string.Equals(r.Station, scopeStation, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        else if (!string.IsNullOrEmpty(scopeDistrict))
        {
            currentRows = currentRows.Where(r => string.Equals(r.District, scopeDistrict, StringComparison.OrdinalIgnoreCase)).ToList();
            priorRows = priorRows.Where(r => string.Equals(r.District, scopeDistrict, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return new InsightsViewModel
        {
            PeriodLabel = FormatPeriodLabel(from, to),
            PriorPeriodLabel = FormatPeriodLabel(priorFrom, priorTo),
            ScopeLabel = scopeStation ?? scopeDistrict,
            CrashTypes = BuildCrashTypes(currentRows, priorRows),
            Routes = BuildRoutes(currentRows, priorRows).Take(10).ToList(),
            TimeSlots = BuildTimeSlots(currentRows, priorRows),
            Stations = BuildStations(currentRows, priorRows).Take(10).ToList(),
            Districts = BuildDistrictLoad(currentRows),
            Severity = BuildSeverityMix(currentRows)
        };
    }



}