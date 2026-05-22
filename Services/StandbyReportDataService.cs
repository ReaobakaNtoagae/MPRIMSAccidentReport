using CrashReport.Data;
using CrashReport.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Services;

/// <summary>
/// Queries the database and assembles all the statistics needed
/// for the Weekly Standby Report document.
/// </summary>
public class StandbyReportDataService
{
    private readonly AppDbContext _context;

    // ── District → SAPS station name prefixes ─────────────────
    // Used to filter crashes by district based on the CrNo prefix.
    // Extend this list as more stations are added to the database.
    private static readonly Dictionary<string, HashSet<string>> DistrictStations =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["EHLANZENI"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "TONGA","WHITE RIVER","NELSPRUIT","MASOYI","MATSULU","NGODWANA",
                "MHALA","CALCUTTA","MASHISHING","BARBERTON","KABOKWENI",
                "KANYAMAZANE","KANAYAMAZANE","HAZYVIEW","SABIE","GRASKOP",
                "ACORNHOEK","KOMATIPOORT","MALALANE","SCHOEMANSDAL",
                "BUSHBUCKRIDGE","KAMHLUSHWA"
            },
            ["BOHLABELO"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "ACORNHOEK","BUSHBUCKRIDGE","MHALA","GRASKOP","SABIE","KLASERIE"
            },
            ["GERT SIBANDE"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "ERMELO","SECUNDA","STANDERTON","BETHAL","BALFOUR","VOLKSRUST",
                "PIET RETIEF","WAKKERSTROOM","MORGENZON","AMSTERDAM"
            },
            ["NKANGALA"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "WITBANK","MIDDELBURG","DELMAS","OGIES","KRIEL","BELFAST",
                "CAROLINA","LEANDRA","KWAMHLANGA","BRONKHORSTSPRUIT"
            }
        };

    public StandbyReportDataService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<StandbyReportViewModel> BuildAsync(
        DateOnly from, DateOnly to,
        DateOnly? priorFrom = null, DateOnly? priorTo = null)
    {
        var vm = new StandbyReportViewModel
        {
            DateFrom = from,
            DateTo = to,
            DayRange = GetDayRange(from, to)
        };

        // ── Current period ─────────────────────────────────────
        var current = await LoadPeriodAsync(from, to);
        vm.CurrentProvince = SumAll(current);
        vm.CurrentEhlanzeni = FilterByDistrict(current, "EHLANZENI");
        vm.CurrentBohlabelo = FilterByDistrict(current, "BOHLABELO");
        vm.CurrentGertSibande = FilterByDistrict(current, "GERT SIBANDE");
        vm.CurrentNkangala = FilterByDistrict(current, "NKANGALA");

        // ── Prior year (same period) ───────────────────────────
        if (priorFrom.HasValue && priorTo.HasValue)
        {
            var prior = await LoadPeriodAsync(priorFrom.Value, priorTo.Value);
            vm.PriorProvince = SumAll(prior);
            vm.PriorEhlanzeni = FilterByDistrict(prior, "EHLANZENI");
            vm.PriorBohlabelo = FilterByDistrict(prior, "BOHLABELO");
            vm.PriorGertSibande = FilterByDistrict(prior, "GERT SIBANDE");
            vm.PriorNkangala = FilterByDistrict(prior, "NKANGALA");
        }

        // ── Problematic routes ─────────────────────────────────
        vm.ProblematicRoutes = await BuildProblematicRoutesAsync(from, to);

        // ── Victim demographics ────────────────────────────────
        vm.Victims = await BuildDemographicsAsync(from, to);

        return vm;
    }

    // ── Load all crash data for a date range ──────────────────
    private async Task<List<CrashRow>> LoadPeriodAsync(DateOnly from, DateOnly to)
    {
        var crashes = await _context.Crashes
            .Include(c => c.CrashConditions)
            .Include(c => c.CrashLocations)
            .Include(c => c.CrashPeople)
            .Where(c => c.CrashDate >= from && c.CrashDate <= to)
            .ToListAsync();

        return crashes.Select(c =>
        {
            var station = ExtractStation(c.CrNo);
            var district = GetDistrict(station);
            var people = c.CrashPeople.ToList();

            var loc = c.CrashLocations.FirstOrDefault();
            return new CrashRow
            {
                CrashId = c.CrashId,
                CrNo = c.CrNo ?? station,
                Station = station,
                District = district,
                CrashDate = c.CrashDate,
                CrashTime = c.CrashTime,
                Route = c.RoadNumber ?? "",
                Location = loc?.StreetRoadName ?? loc?.CityTown,
                Fatalities = people.Count(p => p.SeverityOfInjury == "Fatal"),
                Serious = people.Count(p => p.SeverityOfInjury == "Serious"),
                Slight = people.Count(p => p.SeverityOfInjury == "Slight"),
                FatalPedestrian = people.Count(p =>
                    p.SeverityOfInjury == "Fatal" && p.Role == "Pedestrian")
            };
        }).ToList();
    }

    // ── Aggregate all crashes into province-level stats ───────
    private static DistrictStats SumAll(List<CrashRow> rows) =>
        Aggregate(rows, "ALL");

    // ── Filter by district and aggregate ─────────────────────
    private static DistrictStats FilterByDistrict(List<CrashRow> rows, string district)
    {
        var filtered = rows.Where(r =>
            string.Equals(r.District, district, StringComparison.OrdinalIgnoreCase)).ToList();
        return Aggregate(filtered, district);
    }

    private static DistrictStats Aggregate(List<CrashRow> rows, string name)
    {
        // Build detail list for each crash that has fatalities
        var fatalDetails = rows
            .Where(r => r.Fatalities > 0)
            .OrderBy(r => r.CrashDate)
            .ThenBy(r => r.CrashTime)
            .Select(r => new FatalCrashDetail
            {
                CrNo = r.CrNo,
                Date = r.CrashDate.ToString("dd/MM/yyyy"),
                Time = r.CrashTime.HasValue
                               ? r.CrashTime.Value.ToString("HH:mm")
                               : "Unknown",
                Route = r.Route,
                Location = r.Location,
                Count = r.Fatalities
            })
            .ToList();

        return new DistrictStats
        {
            Name = name,
            Crashes = rows.Count,
            Fatalities = rows.Sum(r => r.Fatalities),
            Serious = rows.Sum(r => r.Serious),
            Slight = rows.Sum(r => r.Slight),
            FatalTime1 = rows.Count(r => r.Fatalities > 0 && IsInTimeSlot(r.CrashTime, 6, 14)),
            FatalTime2 = rows.Count(r => r.Fatalities > 0 && IsInTimeSlot(r.CrashTime, 14, 22)),
            FatalTime3 = rows.Count(r => r.Fatalities > 0 && IsInTimeSlot(r.CrashTime, 22, 6)),
            FatalPedestrians = rows.Sum(r => r.FatalPedestrian),
            FatalDetails = fatalDetails
        };
    }

    // ── Build problematic routes ──────────────────────────────
    private async Task<List<ProblematicRoute>> BuildProblematicRoutesAsync(
        DateOnly from, DateOnly to)
    {
        var crashes = await _context.Crashes
            .Include(c => c.CrashLocations)
            .Include(c => c.CrashPeople)
            .Where(c => c.CrashDate >= from && c.CrashDate <= to &&
                        c.RoadNumber != null)
            .ToListAsync();

        var routes = crashes
            .GroupBy(c => new
            {
                Route = c.RoadNumber!,
                District = GetDistrict(ExtractStation(c.CrNo))
            })
            .Where(g => g.Count() >= 2 ||
                        g.Sum(c => c.CrashPeople.Count(p => p.SeverityOfInjury == "Fatal")) >= 1)
            .Select(g =>
            {
                var locs = g
                    .SelectMany(c => c.CrashLocations.Select(l => l.StreetRoadName ?? l.CityTown))
                    .Where(l => !string.IsNullOrEmpty(l))
                    .Distinct()
                    .Take(3)
                    .ToList();

                return new ProblematicRoute
                {
                    District = g.Key.District,
                    Route = g.Key.Route,
                    Crashes = g.Count(),
                    Fatalities = g.Sum(c =>
                        c.CrashPeople.Count(p => p.SeverityOfInjury == "Fatal")),
                    Locations = string.Join(", ", locs)
                };
            })
            .OrderByDescending(r => r.Fatalities)
            .ThenByDescending(r => r.Crashes)
            .ToList();

        return routes;
    }

    // ── Victim demographics ────────────────────────────────────
    private async Task<VictimDemographics> BuildDemographicsAsync(
        DateOnly from, DateOnly to)
    {
        var people = await _context.CrashPeople
            .Include(cp => cp.Crash)
            .Include(cp => cp.Person)
            .Where(cp => cp.Crash.CrashDate >= from &&
                         cp.Crash.CrashDate <= to &&
                         cp.SeverityOfInjury == "Fatal")
            .ToListAsync();

        static int CountByRoleGender(
            IEnumerable<CrashReport.Models.CrashPerson> list, string role, string gender) =>
            list.Count(p =>
                string.Equals(p.Role, role, StringComparison.OrdinalIgnoreCase) &&
                (p.Person == null
                    ? false
                    : string.Equals(p.Person.Gender, gender,
                        StringComparison.OrdinalIgnoreCase)));

        return new VictimDemographics
        {
            TotalFatalities = people.Count,
            MaleDriver = CountByRoleGender(people, "Driver", "Male"),
            FemaleDriver = CountByRoleGender(people, "Driver", "Female"),
            MalePassenger = CountByRoleGender(people, "Passenger", "Male"),
            FemalePassenger = CountByRoleGender(people, "Passenger", "Female"),
            MalePedestrian = CountByRoleGender(people, "Pedestrian", "Male"),
            FemalePedestrian = CountByRoleGender(people, "Pedestrian", "Female"),
            MaleCyclist = CountByRoleGender(people, "Bicyclist", "Male"),
            FemaleCyclist = CountByRoleGender(people, "Bicyclist", "Female"),
            MaleTotal = people.Count(p =>
                p.Person != null &&
                string.Equals(p.Person.Gender, "Male", StringComparison.OrdinalIgnoreCase)),
            FemaleTotal = people.Count(p =>
                p.Person != null &&
                string.Equals(p.Person.Gender, "Female", StringComparison.OrdinalIgnoreCase))
        };
    }

    // ── Helpers ───────────────────────────────────────────────
    private static string ExtractStation(string? crNo)
    {
        if (string.IsNullOrEmpty(crNo)) return string.Empty;
        return crNo.Contains('-') ? crNo.Split('-')[0].Trim() : crNo.Trim();
    }

    private static string GetDistrict(string station)
    {
        foreach (var kvp in DistrictStations)
            if (kvp.Value.Contains(station))
                return kvp.Key;
        return "UNKNOWN";
    }

    private static bool IsInTimeSlot(TimeOnly? time, int startH, int endH)
    {
        if (!time.HasValue) return false;
        var h = time.Value.Hour;
        if (startH < endH) return h >= startH && h < endH;
        return h >= startH || h < endH; // wraps midnight (22–06)
    }

    private static string GetDayRange(DateOnly from, DateOnly to)
    {
        static string DayName(DayOfWeek d) => d.ToString().ToUpper();
        return $"{DayName(from.DayOfWeek)} TO {DayName(to.DayOfWeek)}";
    }

    // ── Internal DTO ─────────────────────────────────────────
    private class CrashRow
    {
        public int CrashId { get; set; }
        public string CrNo { get; set; } = string.Empty;
        public string Station { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public DateOnly CrashDate { get; set; }
        public TimeOnly? CrashTime { get; set; }
        public string Route { get; set; } = string.Empty;
        public string? Location { get; set; }
        public int Fatalities { get; set; }
        public int Serious { get; set; }
        public int Slight { get; set; }
        public int FatalPedestrian { get; set; }
    }
}