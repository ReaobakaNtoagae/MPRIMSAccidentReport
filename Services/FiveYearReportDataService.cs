using CrashReport.Data;
using CrashReport.ViewModels;
using CrashReport.Models;
using CrashReport.Services;
using Microsoft.EntityFrameworkCore;
using static CrashReport.ViewModels.FiveYearReportRequest;

namespace CrashReport.Services;


public class FiveYearReportDataService : MonthlyMemoDataService
{
    public FiveYearReportDataService(AppDbContext context, IStationDistrictLookup stationDistrict)
        : base(context, stationDistrict) { }

    private static readonly Dictionary<string, string> RegionDisplayNames = new()
    {
        ["EhlanzeniSouth"] = "EHLANZENI",
        ["EhlanzeniNorth"] = "BOHLABELA",
        ["GertSibande"] = "GERT SIBANDE",
        ["Nkangala"] = "NKANGALA",
    };



    private static readonly string[] CrashTypeList =
    {
        "PEDESTRIAN", "HEAD ON", "LOST CONTROL", "SIDESWIPE",
        "OVERTURNED", "FIXED OBJECT", "HEAD REAR", "REAR END"
    };

    private static readonly (string Label, string Category)[] VehicleCategoryList =
    {
        ("SEDANS", "Passenger"), ("LDV", "Goods"), ("TAXIS", "Taxi"),
        ("TRUCKS", "Truck"), ("MOTORCYCLES", "Motorcycle"),
        ("BICYCLE", "Bicycle"), ("ARTICULATED", "Articulated"), ("BUSSES", "Bus")
    };

    private static readonly (string Label, int Start, int End)[] TimeSlotList =
    {
        ("06H00 -14H00", 6, 14), ("14H00 -22H00", 14, 22), ("22H00-06H00", 22, 6)
    };

    private static readonly (string Label, DayOfWeek Day)[] DayList =
    {
        ("MONDAY", DayOfWeek.Monday), ("TUESDAY", DayOfWeek.Tuesday),
        ("WEDNESDAY", DayOfWeek.Wednesday), ("THURSDAY", DayOfWeek.Thursday),
        ("FRIDAY", DayOfWeek.Friday), ("SATURDAY", DayOfWeek.Saturday), ("SUNDAY", DayOfWeek.Sunday)
    };

    public async Task<FiveYearReportViewModel> BuildAsync(FiveYearReportRequest req)
    {
        var years = Enumerable.Range(req.EndYear - 4, 5).ToArray();
        var monthName = new DateTime(2000, req.Month, 1).ToString("MMMM").ToUpper();

        var vm = new FiveYearReportViewModel
        {
            MonthName = monthName,
            StartYear = years[0],
            EndYear = req.EndYear,
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

        // Load all 5 years of crash rows for this month up front — every
        // section below just filters/aggregates this same in-memory set,
        // rather than re-querying the database per section.
        var rowsByYear = new Dictionary<int, List<Row>>();
        foreach (var y in years)
        {
            var from = new DateOnly(y, req.Month, 1);
            var to = from.AddMonths(1).AddDays(-1);
            rowsByYear[y] = await LoadAsync(from, to);
        }

        // ── Section 1: Regional status summaries ──
        vm.RegionSummaries.Add(BuildRegionSummary("PROVINCIAL", years, rowsByYear, null));
        foreach (var (key, _, stations) in Districts)
            vm.RegionSummaries.Add(BuildRegionSummary(RegionDisplayNames[key], years, rowsByYear, stations));

        // ── Section 2: Problematic routes — Provincial + each region ──
        vm.ProvincialRoutes = BuildRegionRouteData("PROVINCIAL", years, rowsByYear, null);
        foreach (var (key, _, stations) in Districts)
            vm.RegionRoutes.Add(BuildRegionRouteData(RegionDisplayNames[key], years, rowsByYear, stations));

        // ── Section 3: Crash types & vehicle categories ──
        vm.CrashTypes = BuildCrashTypeRanking(years, rowsByYear);
        vm.VehicleCategories = BuildVehicleCategoryRanking(years, rowsByYear);

        // ── Section 4: Time of day ──
        (vm.TimeSlotsCrashes, vm.TimeSlotsFatalities) = BuildTimeSlotRanking(years, rowsByYear);

        // ── Section 5: Day of week ──
        (vm.DaysOfWeekCrashes, vm.DaysOfWeekFatalities) = BuildDayOfWeekRanking(years, rowsByYear);

        // ── Section 6: Shock weekend ──
        (vm.WeekendsCrashes, vm.WeekendsFatalities) = BuildWeekendRanking(req.Month, years, rowsByYear);

        // ── Section 7: Demographics (caveat applies) ──
        await PopulateDemographicsAsync(vm, req.Month, years);

        return vm;
    }

    // ── Section 1 helper ──────────────────────────────────────────
    private static RegionSummary BuildRegionSummary(
        string displayName, int[] years, Dictionary<int, List<Row>> rowsByYear, HashSet<string>? stations)
    {
        var crashes = new int[years.Length];
        var fatal = new int[years.Length];
        var serious = new int[years.Length];
        var slight = new int[years.Length];

        for (int i = 0; i < years.Length; i++)
        {
            var rows = rowsByYear[years[i]];
            if (stations != null) rows = rows.Where(r => stations.Contains(r.Station)).ToList();

            crashes[i] = rows.Count;
            fatal[i] = rows.Sum(r => r.Fatalities);
            serious[i] = rows.Sum(r => r.Serious);
            slight[i] = rows.Sum(r => r.Slight);
        }

        return new RegionSummary
        {
            RegionName = displayName,
            Stats = new List<YearlyStatRow>
            {
                new() { Label = "CRASHES", Years = crashes },
                new() { Label = "FATALITIES", Years = fatal },
                new() { Label = "SERIOUS INJURIES", Years = serious },
                new() { Label = "SLIGHT INJURIES", Years = slight },
            }
        };
    }

    // ── Section 2 helper ──────────────────────────────────────────
    private static RegionRouteData BuildRegionRouteData(
        string displayName, int[] years, Dictionary<int, List<Row>> rowsByYear, HashSet<string>? stations)
    {
        var yearRows = new List<Row>[years.Length];
        var routeSet = new HashSet<string>();

        for (int i = 0; i < years.Length; i++)
        {
            var rows = rowsByYear[years[i]];
            if (stations != null) rows = rows.Where(r => stations.Contains(r.Station)).ToList();
            yearRows[i] = rows;
            foreach (var r in rows.Where(r => !string.IsNullOrEmpty(r.Route)))
                routeSet.Add(r.Route);
        }

        var crashRows = routeSet.Select(route => new RankedRow
        {
            Label = route,
            Years = yearRows.Select(rows => rows.Count(r => r.Route == route)).ToArray()
        })
        .Where(r => r.Total > 0)
        .OrderByDescending(r => r.Total)
        .ToList();
        ApplyPercent(crashRows);

        var fatalRows = routeSet.Select(route => new RankedRow
        {
            Label = route,
            Years = yearRows.Select(rows => rows.Where(r => r.Route == route).Sum(r => r.Fatalities)).ToArray()
        })
        .Where(r => r.Total > 0)
        .OrderByDescending(r => r.Total)
        .ToList();
        ApplyPercent(fatalRows);

        return new RegionRouteData
        {
            RegionName = displayName,
            CrashRoutes = new RankedTable { Title = $"{displayName} PROBLEMATIC ROUTES: CRASHES", Rows = crashRows },
            FatalityRoutes = new RankedTable { Title = $"{displayName} PROBLEMATIC ROUTES: FATALITIES", Rows = fatalRows }
        };
    }

    // ── Section 3 helpers ────────────────────────────────────────
    private static RankedTable BuildCrashTypeRanking(int[] years, Dictionary<int, List<Row>> rowsByYear)
    {
        var rows = CrashTypeList.Select(type => new RankedRow
        {
            Label = type,
            Years = years.Select(y => rowsByYear[y]
                .Count(r => string.Equals(r.CrashType, type, StringComparison.OrdinalIgnoreCase)))
                .ToArray()
        })
        .Where(r => r.Total > 0)
        .OrderByDescending(r => r.Total)
        .ToList();
        ApplyPercent(rows);

        return new RankedTable { Title = "PROVINCIAL CRASH TYPES", Rows = rows };
    }

    private static RankedTable BuildVehicleCategoryRanking(int[] years, Dictionary<int, List<Row>> rowsByYear)
    {
        var rows = VehicleCategoryList.Select(cat => new RankedRow
        {
            Label = cat.Label,
            Years = years.Select(y => rowsByYear[y]
                .Count(r => r.VehicleCats.Any(v => string.Equals(v, cat.Category, StringComparison.OrdinalIgnoreCase))))
                .ToArray()
        })
        .Where(r => r.Total > 0)
        .OrderByDescending(r => r.Total)
        .ToList();
        ApplyPercent(rows);

        return new RankedTable { Title = "PROVINCIAL VEHICLE CATEGORIES", Rows = rows };
    }

    // ── Section 4 helper ────────────────────────────────────────
    private static (RankedTable Crashes, RankedTable Fatalities) BuildTimeSlotRanking(
        int[] years, Dictionary<int, List<Row>> rowsByYear)
    {
        var crashRows = TimeSlotList.Select(slot => new RankedRow
        {
            Label = slot.Label,
            Years = years.Select(y => rowsByYear[y].Count(r => InSlot(r.Time, slot.Start, slot.End))).ToArray()
        }).ToList();
        ApplyPercent(crashRows);

        var fatalRows = TimeSlotList.Select(slot => new RankedRow
        {
            Label = slot.Label,
            Years = years.Select(y => rowsByYear[y].Where(r => InSlot(r.Time, slot.Start, slot.End)).Sum(r => r.Fatalities)).ToArray()
        }).ToList();
        ApplyPercent(fatalRows);

        return (
            new RankedTable { Title = "PROVINCE PREVALENT TIMES: CRASHES", Rows = crashRows },
            new RankedTable { Title = "PROVINCE PREVALENT TIMES: FATALITIES", Rows = fatalRows }
        );
    }

    // ── Section 5 helper ────────────────────────────────────────
    private static (RankedTable Crashes, RankedTable Fatalities) BuildDayOfWeekRanking(
        int[] years, Dictionary<int, List<Row>> rowsByYear)
    {
        var crashRows = DayList.Select(d => new RankedRow
        {
            Label = d.Label,
            Years = years.Select(y => rowsByYear[y].Count(r => r.Date.DayOfWeek == d.Day)).ToArray()
        }).ToList();
        ApplyPercent(crashRows);

        var fatalRows = DayList.Select(d => new RankedRow
        {
            Label = d.Label,
            Years = years.Select(y => rowsByYear[y].Where(r => r.Date.DayOfWeek == d.Day).Sum(r => r.Fatalities)).ToArray()
        }).ToList();
        ApplyPercent(fatalRows);

        return (
            new RankedTable { Title = "PROVINCE DAYS OF THE WEEK: CRASHES", Rows = crashRows },
            new RankedTable { Title = "PROVINCE DAYS OF THE WEEK: FATALITIES", Rows = fatalRows }
        );
    }

    // ── Section 6 helper — "shock weekend" ──────────────────────
    private static (RankedTable Crashes, RankedTable Fatalities) BuildWeekendRanking(
        int month, int[] years, Dictionary<int, List<Row>> rowsByYear)
    {
        var labelYear = years.Max();
        var labelWeekends = GetWeekendsInMonth(labelYear, month);
        var ordinals = new[] { "1ST", "2ND", "3RD", "4TH", "5TH", "6TH" };

        var crashRows = new List<RankedRow>();
        var fatalRows = new List<RankedRow>();

        for (int wi = 0; wi < labelWeekends.Count; wi++)
        {
            var crashYrs = new int[years.Length];
            var fatalYrs = new int[years.Length];

            for (int yi = 0; yi < years.Length; yi++)
            {
                var yearWeekends = GetWeekendsInMonth(years[yi], month);
                if (wi >= yearWeekends.Count) continue;

                var (start, end) = yearWeekends[wi];
                var rows = rowsByYear[years[yi]].Where(r => r.Date >= start && r.Date <= end).ToList();
                crashYrs[yi] = rows.Count;
                fatalYrs[yi] = rows.Sum(r => r.Fatalities);
            }

            var label = FormatWeekendLabel(labelWeekends[wi].Start, labelWeekends[wi].End);
            crashRows.Add(new RankedRow { Label = label, Years = crashYrs });
            fatalRows.Add(new RankedRow { Label = label, Years = fatalYrs });
        }

        ApplyPercent(crashRows);
        ApplyPercent(fatalRows);

        var monthName = new DateTime(2000, month, 1).ToString("MMMM").ToUpper();

        return (
            new RankedTable { Title = $"SHOCK WEEKEND FOR {monthName}: CRASHES", Rows = crashRows },
            new RankedTable { Title = $"SHOCK WEEKEND FOR {monthName}: FATALITIES", Rows = fatalRows }
        );
    }

    private static List<(DateOnly Start, DateOnly End)> GetWeekendsInMonth(int year, int month)
    {
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var weekends = new List<(DateOnly, DateOnly)>();

        for (var d = monthStart; d <= monthEnd; d = d.AddDays(1))
        {
            if (d.DayOfWeek == DayOfWeek.Friday)
                weekends.Add((d, d.AddDays(2))); // Friday → Sunday, may spill into next month
        }

        return weekends;
    }

    private static string FormatWeekendLabel(DateOnly start, DateOnly end)
    {
        var months = new[] { "", "JAN", "FEB", "MAR", "APR", "MAY", "JUN",
                             "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };
        return $"{start.Day:D2}-{end.Day:D2} {months[end.Month]}";
    }

    // ── Section 7 helper — demographics (data-quality caveat) ──
    private async Task PopulateDemographicsAsync(FiveYearReportViewModel vm, int month, int[] years)
    {
        var ageLabels = new[] { "0-7", "08-12", "13-18", "19-35", "36+" };
        var roleLabels = new[] { "DRIVER", "PASSENGER", "PEDESTRIANS", "CYCLIST" };

        var ageByLabel = ageLabels.ToDictionary(l => l, _ => new Dictionary<int, int>());
        var maleByLabel = roleLabels.ToDictionary(l => l, _ => new Dictionary<int, int>());
        var femaleByLabel = roleLabels.ToDictionary(l => l, _ => new Dictionary<int, int>());

        var yearsWithData = new List<int>();

        foreach (var y in years)
        {
            var from = new DateOnly(y, month, 1);
            var to = from.AddMonths(1).AddDays(-1);

            var record = await _context.CrashDemographics
                .FirstOrDefaultAsync(d => d.PeriodFrom == from && d.PeriodTo == to && d.ProvinceCode == "MP");

            if (record == null) continue; // no submission for this year/month — leave as a gap, not a zero

            yearsWithData.Add(y);

            ageByLabel["0-7"][y] = record.Age0to7;
            ageByLabel["08-12"][y] = record.Age8to12;
            ageByLabel["13-18"][y] = record.Age13to18;
            ageByLabel["19-35"][y] = record.Age19to35;
            ageByLabel["36+"][y] = record.Age36Plus;

            maleByLabel["DRIVER"][y] = record.DriverMale;
            maleByLabel["PASSENGER"][y] = record.PassengerMale;
            maleByLabel["PEDESTRIANS"][y] = record.PedestrianMale;
            maleByLabel["CYCLIST"][y] = record.CyclistMale;

            femaleByLabel["DRIVER"][y] = record.DriverFemale;
            femaleByLabel["PASSENGER"][y] = record.PassengerFemale;
            femaleByLabel["PEDESTRIANS"][y] = record.PedestrianFemale;
            femaleByLabel["CYCLIST"][y] = record.CyclistFemale;
        }

        vm.DemographicYearsAvailable = yearsWithData;
        vm.DemographicsHasGaps = yearsWithData.Count < years.Length;

        vm.AgeGroups = ageByLabel.Select(kv => new DemographicYearRow { Label = kv.Key, ByYear = kv.Value }).ToList();
        vm.MaleByRole = maleByLabel.Select(kv => new DemographicYearRow { Label = kv.Key, ByYear = kv.Value }).ToList();
        vm.FemaleByRole = femaleByLabel.Select(kv => new DemographicYearRow { Label = kv.Key, ByYear = kv.Value }).ToList();
    }

    // ── Shared ────────────────────────────────────────────────────
    private static void ApplyPercent(List<RankedRow> rows)
    {
        var grandTotal = rows.Sum(r => r.Total);
        foreach (var r in rows)
            r.Percent = grandTotal > 0 ? Math.Round(100.0 * r.Total / grandTotal) : 0;
    }
}