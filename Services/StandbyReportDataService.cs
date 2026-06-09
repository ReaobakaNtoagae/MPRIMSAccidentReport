using CrashReport.Data;
using CrashReport.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Services;

public class StandbyReportDataService
{
    private readonly AppDbContext _context;


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


        var current = await LoadPeriodAsync(from, to);
        vm.CurrentProvince = SumAll(current);
        vm.CurrentEhlanzeni = FilterByDistrict(current, "EHLANZENI");
        vm.CurrentBohlabelo = FilterByDistrict(current, "BOHLABELO");
        vm.CurrentGertSibande = FilterByDistrict(current, "GERT SIBANDE");
        vm.CurrentNkangala = FilterByDistrict(current, "NKANGALA");

     
        if (priorFrom.HasValue && priorTo.HasValue)
        {
            var prior = await LoadPeriodAsync(priorFrom.Value, priorTo.Value);
            vm.PriorProvince = SumAll(prior);
            vm.PriorEhlanzeni = FilterByDistrict(prior, "EHLANZENI");
            vm.PriorBohlabelo = FilterByDistrict(prior, "BOHLABELO");
            vm.PriorGertSibande = FilterByDistrict(prior, "GERT SIBANDE");
            vm.PriorNkangala = FilterByDistrict(prior, "NKANGALA");
        }

   
        vm.ProblematicRoutes = await BuildProblematicRoutesAsync(from, to);

        vm.SubPeriod = await BuildSubPeriodAsync(from, to);

     
        if (vm.SubPeriod != null)
        {
            vm.Victims = await BuildDemographicsAsync(vm.SubPeriod.From, vm.SubPeriod.To);
        }
        else
        {
            
            vm.Victims = await BuildDemographicsAsync(from, to);
        }

        return vm;
    }

  
    private async Task<SubPeriodStats?> BuildSubPeriodAsync(DateOnly from, DateOnly to)
    {
        int daysDiff = to.DayNumber - from.DayNumber;
        if (daysDiff < 3) return null;

       
        DateOnly subFrom = to.AddDays(-2);
        DateOnly subTo = to;

        
        var feb14 = new DateOnly(from.Year, 2, 14);
        var feb16 = new DateOnly(from.Year, 2, 16);
        if (from <= feb14 && to >= feb16)
        {
            subFrom = feb14;
            subTo = feb16;
        }

        var periodData = await LoadPeriodAsync(subFrom, subTo);

        return new SubPeriodStats
        {
            Label = $"{subFrom:dd MMMM yyyy} – {subTo:dd MMMM yyyy}",
            From = subFrom,
            To = subTo,
            Province = SumAll(periodData),
            Ehlanzeni = FilterByDistrict(periodData, "EHLANZENI"),
            Bohlabelo = FilterByDistrict(periodData, "BOHLABELO"),
            GertSibande = FilterByDistrict(periodData, "GERT SIBANDE"),
            Nkangala = FilterByDistrict(periodData, "NKANGALA")
        };
    }

    
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
                Location = loc?.StreetRoadName ?? loc?.CityTown ?? loc?.Suburb,
                Fatalities = people.Count(p => p.SeverityOfInjury == "Fatal"),
                Serious = people.Count(p => p.SeverityOfInjury == "Serious"),
                Slight = people.Count(p => p.SeverityOfInjury == "Slight"),
                FatalPedestrian = people.Count(p =>
                    p.SeverityOfInjury == "Fatal" && p.Role == "Pedestrian")
            };
        }).ToList();
    }

  
    private static DistrictStats SumAll(List<CrashRow> rows) =>
        Aggregate(rows, "ALL");

  
    private static DistrictStats FilterByDistrict(List<CrashRow> rows, string district)
    {
        var filtered = rows.Where(r =>
            string.Equals(r.District, district, StringComparison.OrdinalIgnoreCase)).ToList();
        return Aggregate(filtered, district);
    }

    private static DistrictStats Aggregate(List<CrashRow> rows, string name)
    {
       
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
            // Sum fatalities by time slot (not count of crashes)
            FatalTime1 = rows.Where(r => IsInTimeSlot(r.CrashTime, 6, 14)).Sum(r => r.Fatalities),
            FatalTime2 = rows.Where(r => IsInTimeSlot(r.CrashTime, 14, 22)).Sum(r => r.Fatalities),
            FatalTime3 = rows.Where(r => IsInTimeSlot(r.CrashTime, 22, 6)).Sum(r => r.Fatalities),
            FatalPedestrians = rows.Sum(r => r.FatalPedestrian),
            FatalDetails = fatalDetails
        };
    }

   
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
                    .SelectMany(c => c.CrashLocations.Select(l => l.StreetRoadName ?? l.CityTown ?? l.Suburb))
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

   
    private async Task<VictimDemographics> BuildDemographicsAsync(DateOnly from, DateOnly to)
    {
        var people = await _context.CrashPeople
            .Include(cp => cp.Crash)
            .Include(cp => cp.Person)
            .Where(cp => cp.Crash.CrashDate >= from &&
                         cp.Crash.CrashDate <= to &&
                         cp.SeverityOfInjury == "Fatal")
            .ToListAsync();

        static bool IsMale(CrashReport.Models.CrashPerson p) =>
            p.Person != null && string.Equals(p.Person.Gender, "Male", StringComparison.OrdinalIgnoreCase);

        static bool IsFemale(CrashReport.Models.CrashPerson p) =>
            p.Person != null && string.Equals(p.Person.Gender, "Female", StringComparison.OrdinalIgnoreCase);

        static bool IsRole(CrashReport.Models.CrashPerson p, string role) =>
            string.Equals(p.Role, role, StringComparison.OrdinalIgnoreCase);

        return new VictimDemographics
        {
            TotalFatalities = people.Count,

            // Age breakdown
            Age0to7 = people.Count(p => p.Person?.Age is >= 0 and <= 7),
            Age8to12 = people.Count(p => p.Person?.Age is >= 8 and <= 12),
            Age13to18 = people.Count(p => p.Person?.Age is >= 13 and <= 18),
            Age19to35 = people.Count(p => p.Person?.Age is >= 19 and <= 35),
            Age36Plus = people.Count(p => p.Person?.Age >= 36),

            // Gender totals
            MaleTotal = people.Count(IsMale),
            FemaleTotal = people.Count(IsFemale),

            // Driver
            MaleDriver = people.Count(p => IsMale(p) && IsRole(p, "Driver")),
            FemaleDriver = people.Count(p => IsFemale(p) && IsRole(p, "Driver")),

            // Passenger
            MalePassenger = people.Count(p => IsMale(p) && IsRole(p, "Passenger")),
            FemalePassenger = people.Count(p => IsFemale(p) && IsRole(p, "Passenger")),

            // Pedestrian
            MalePedestrian = people.Count(p => IsMale(p) && IsRole(p, "Pedestrian")),
            FemalePedestrian = people.Count(p => IsFemale(p) && IsRole(p, "Pedestrian")),

            // Cyclist
            MaleCyclist = people.Count(p => IsMale(p) && IsRole(p, "Bicyclist")),
            FemaleCyclist = people.Count(p => IsFemale(p) && IsRole(p, "Bicyclist"))
        };
    }


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
        return h >= startH || h < endH; 
    }

    private static string GetDayRange(DateOnly from, DateOnly to)
    {
        static string DayName(DayOfWeek d) => d.ToString().ToUpper();
        return $"{DayName(from.DayOfWeek)} TO {DayName(to.DayOfWeek)}";
    }


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