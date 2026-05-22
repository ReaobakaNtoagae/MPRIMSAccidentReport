using CrashReport.Data;
using CrashReport.Services;
using CrashReport.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Controllers;

public class ReportController : Controller
{
    private readonly MonthlyReportService _reportService;
    private readonly AppDbContext _context;
    private readonly WordexportService _wordExport;
    private readonly StandbyReportWordService _standbyWord;  // FIX 9: inject, don't new()

    public ReportController(
        MonthlyReportService reportService,
        AppDbContext context,
        WordexportService wordExport,
        StandbyReportWordService standbyWord)
    {
        _reportService = reportService;
        _context = context;
        _wordExport = wordExport;
        _standbyWord = standbyWord;
    }

    // GET: /Report
    public async Task<IActionResult> Index()
    {
        var available = await _context.Crashes
            .GroupBy(c => new { c.CrashDate.Year, c.CrashDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .OrderByDescending(g => g.Year).ThenByDescending(g => g.Month)
            .ToListAsync();

        ViewBag.Available = available;
        ViewBag.CurrentYear = DateTime.Today.Year;
        ViewBag.CurrentMonth = DateTime.Today.Month;
        return View();
    }

    // GET: /Report/Download?year=2025&month=2&district=EHLANZENI
    [HttpGet]
    public async Task<IActionResult> Download(int year, int month,
        string district = "EHLANZENI")
    {
        if (year < 2000 || year > 2100 || month < 1 || month > 12)
            return BadRequest("Invalid year or month.");

        var bytes = await _reportService.GenerateAsync(year, month, district);
        var fileName = $"Accident_Report_{new DateTime(year, month, 1):MMMM_yyyy}.ToUpper().docx";
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Accident_Report_{new DateTime(year, month, 1):MMMM_yyyy}.xlsx");
    }

    // GET: /Report/Preview?year=2025&month=2 — JSON for the data grid
    [HttpGet]
    public async Task<IActionResult> Preview(int year, int month)
    {
        var crashes = await _context.Crashes
            .Include(c => c.CrashLocations)
            .Include(c => c.CrashConditions)
            .Include(c => c.CrashPeople)
            .Where(c => c.CrashDate.Year == year && c.CrashDate.Month == month)
            .OrderBy(c => c.CrashDate).ThenBy(c => c.CrashTime)
            .Select(c => new
            {
                c.CrashId,
                SapsStation = c.CrNo != null && c.CrNo.Contains("-")
                    ? c.CrNo.Substring(0, c.CrNo.IndexOf("-"))
                    : c.CrNo,
                ArNo = c.CrNo != null && c.CrNo.Contains("-")
                    ? c.CrNo.Substring(c.CrNo.IndexOf("-") + 1)
                    : c.CrNo,
                c.CasNo,
                Date = c.CrashDate.Day.ToString("D2") + "/" + c.CrashDate.Month.ToString("D2"),
                c.RoadNumber,
                Location = c.CrashLocations.Select(l => l.StreetRoadName ?? l.CityTown).FirstOrDefault(),
                CrashType = c.CrashConditions.Select(cc => cc.CrashType).FirstOrDefault(),
                FatalCount = c.CrashPeople.Count(p => p.SeverityOfInjury == "Fatal"),
                SeriousCount = c.CrashPeople.Count(p => p.SeverityOfInjury == "Serious"),
                SlightCount = c.CrashPeople.Count(p => p.SeverityOfInjury == "Slight"),
            })
            .ToListAsync();

        return Json(crashes);
    }

    // POST: /StandbyReport/Generate
    // FIX 10: renamed route to avoid conflict with StandbyReportController.Preview
    [HttpPost("/StandbyReport/Generate")]
    public async Task<IActionResult> GenerateStandbyReport(StandbyReportRequest request)
    {
        if (request.DateFrom > request.DateTo)
            return BadRequest("Start date must be before end date.");

        if (request.DateTo > DateOnly.FromDateTime(DateTime.Today))
            return BadRequest("Cannot generate report for future dates.");

        var vm = await BuildStandbyReportViewModel(request);
        var bytes = _standbyWord.Generate(vm);  // FIX 9: use injected service

        return File(bytes,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            $"Weekly_Standby_Report_{vm.DateFrom:yyyy-MM-dd}_to_{vm.DateTo:yyyy-MM-dd}.docx");
    }

    // ─────────────────────────────────────────────────────────────────────────
    private async Task<StandbyReportViewModel> BuildStandbyReportViewModel(
        StandbyReportRequest request)
    {
        var vm = new StandbyReportViewModel
        {
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            DayRange = GetDayRangeString(request.DateFrom, request.DateTo),
        };

        vm.CurrentProvince = await GetDistrictStatsAsync(request.DateFrom, request.DateTo, null);
        vm.CurrentEhlanzeni = await GetDistrictStatsAsync(request.DateFrom, request.DateTo, "EHLANZENI");
        vm.CurrentBohlabelo = await GetDistrictStatsAsync(request.DateFrom, request.DateTo, "BOHLABELO");
        vm.CurrentGertSibande = await GetDistrictStatsAsync(request.DateFrom, request.DateTo, "GERT SIBANDE");
        vm.CurrentNkangala = await GetDistrictStatsAsync(request.DateFrom, request.DateTo, "NKANGALA");

        if (request.PriorYearFrom.HasValue && request.PriorYearTo.HasValue)
        {
            vm.PriorProvince = await GetDistrictStatsAsync(request.PriorYearFrom.Value, request.PriorYearTo.Value, null);
            vm.PriorEhlanzeni = await GetDistrictStatsAsync(request.PriorYearFrom.Value, request.PriorYearTo.Value, "EHLANZENI");
            vm.PriorBohlabelo = await GetDistrictStatsAsync(request.PriorYearFrom.Value, request.PriorYearTo.Value, "BOHLABELO");
            vm.PriorGertSibande = await GetDistrictStatsAsync(request.PriorYearFrom.Value, request.PriorYearTo.Value, "GERT SIBANDE");
            vm.PriorNkangala = await GetDistrictStatsAsync(request.PriorYearFrom.Value, request.PriorYearTo.Value, "NKANGALA");
        }

        vm.ProblematicRoutes = await GetProblematicRoutesAsync(request.DateFrom, request.DateTo);
        vm.SubPeriod = await BuildSubPeriodStatsAsync(request);

        if (vm.SubPeriod != null)
            vm.Victims = await GetVictimDemographicsAsync(vm.SubPeriod.From, vm.SubPeriod.To);

        return vm;
    }

    // ── District stats ────────────────────────────────────────────────────────
    private async Task<DistrictStats> GetDistrictStatsAsync(
        DateOnly from, DateOnly to, string? district)
    {
        // FIX 2: District is not a DB column — load all crashes in range,
        //        then filter in memory using the CrNo station prefix.
        var allCrashes = await _context.Crashes
            .Include(c => c.CrashPeople)
                .ThenInclude(p => p.Person)          // FIX 6/7: need Person for Gender/Age
            .Include(c => c.CrashLocations)
            .Where(c => c.CrashDate >= from && c.CrashDate <= to)
            .ToListAsync();

        var crashes = district == null
            ? allCrashes
            : allCrashes.Where(c => GetDistrict(c.CrNo) == district).ToList();

        var fatalDetails = crashes
            .Where(c => c.CrashPeople.Any(p => p.SeverityOfInjury == "Fatal"))
            .OrderBy(c => c.CrashDate).ThenBy(c => c.CrashTime)
            .Select(c => new FatalCrashDetail
            {
                CrNo = c.CrNo ?? "N/A",
                Date = c.CrashDate.ToString("dd/MM/yyyy"),
                Time = c.CrashTime?.ToString(@"hh\:mm") ?? "Unknown",
                Route = c.RoadNumber,
                Location = c.CrashLocations.FirstOrDefault()?.StreetRoadName   // FIX 8: null-conditional
                           ?? c.CrashLocations.FirstOrDefault()?.CityTown,
                Count = c.CrashPeople.Count(p => p.SeverityOfInjury == "Fatal"),
            })
            .ToList();

        return new DistrictStats
        {
            Name = district ?? "PROVINCE",
            Crashes = crashes.Count,
            Fatalities = crashes.Sum(c => c.CrashPeople.Count(p => p.SeverityOfInjury == "Fatal")),
            Serious = crashes.Sum(c => c.CrashPeople.Count(p => p.SeverityOfInjury == "Serious")),
            Slight = crashes.Sum(c => c.CrashPeople.Count(p => p.SeverityOfInjury == "Slight")),

            // FIX 1: TimeOnly.Hour not .Hours
            FatalTime1 = crashes
                .Where(c => c.CrashTime.HasValue &&
                            c.CrashTime.Value.Hour >= 6 && c.CrashTime.Value.Hour < 14)
                .Sum(c => c.CrashPeople.Count(p => p.SeverityOfInjury == "Fatal")),
            FatalTime2 = crashes
                .Where(c => c.CrashTime.HasValue &&
                            c.CrashTime.Value.Hour >= 14 && c.CrashTime.Value.Hour < 22)
                .Sum(c => c.CrashPeople.Count(p => p.SeverityOfInjury == "Fatal")),
            FatalTime3 = crashes
                .Where(c => c.CrashTime.HasValue &&
                            (c.CrashTime.Value.Hour >= 22 || c.CrashTime.Value.Hour < 6))
                .Sum(c => c.CrashPeople.Count(p => p.SeverityOfInjury == "Fatal")),

            // FIX 3/4: use Role, not RoadUserType or Pedestrian bool
            FatalPedestrians = crashes
                .SelectMany(c => c.CrashPeople)
                .Count(p => p.SeverityOfInjury == "Fatal" &&
                            string.Equals(p.Role, "PEDESTRIAN", StringComparison.OrdinalIgnoreCase)),

            FatalDetails = fatalDetails,
        };
    }

    // ── Problematic routes ────────────────────────────────────────────────────
    private async Task<List<ProblematicRoute>> GetProblematicRoutesAsync(
        DateOnly from, DateOnly to)
    {
        // FIX 8: load into memory first to avoid null-ref on FirstOrDefault() in EF projection
        var crashes = await _context.Crashes
            .Include(c => c.CrashPeople)
            .Include(c => c.CrashLocations)
            .Where(c => c.CrashDate >= from && c.CrashDate <= to &&
                        c.RoadNumber != null)
            .ToListAsync();

        return crashes
            .GroupBy(c => new { District = GetDistrict(c.CrNo), c.RoadNumber })
            .Select(g =>
            {
                int fatalities = g.Sum(c => c.CrashPeople.Count(p => p.SeverityOfInjury == "Fatal"));
                int count = g.Count();
                if (fatalities < 1 && count < 3) return null;

                var locations = g
                    .SelectMany(c => c.CrashLocations)
                    .Select(l => l.StreetRoadName ?? l.CityTown)   // FIX 8
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Distinct()
                    .Take(3);

                return new ProblematicRoute
                {
                    District = g.Key.District,
                    Route = g.Key.RoadNumber!,
                    Crashes = count,
                    Fatalities = fatalities,
                    Locations = string.Join(", ", locations),
                };
            })
            .Where(r => r != null)
            .OrderByDescending(r => r!.Fatalities)
            .ThenByDescending(r => r!.Crashes)
            .ToList()!;
    }

    // ── Sub-period ────────────────────────────────────────────────────────────
    private async Task<SubPeriodStats?> BuildSubPeriodStatsAsync(StandbyReportRequest request)
    {
        int daysDiff = request.DateTo.DayNumber - request.DateFrom.DayNumber;
        if (daysDiff < 3) return null;

        // Default: last 3 days of the week
        DateOnly subFrom = request.DateTo.AddDays(-2);
        DateOnly subTo = request.DateTo;

        // Valentine's override: if period spans Feb 14–16 use those dates
        var feb14 = new DateOnly(request.DateFrom.Year, 2, 14);
        var feb16 = new DateOnly(request.DateFrom.Year, 2, 16);
        if (request.DateFrom <= feb14 && request.DateTo >= feb16)
        {
            subFrom = feb14;
            subTo = feb16;
        }

        return new SubPeriodStats
        {
            Label = $"{subFrom:dd MMMM yyyy} – {subTo:dd MMMM yyyy}",
            From = subFrom,
            To = subTo,
            Province = await GetDistrictStatsAsync(subFrom, subTo, null),
            Ehlanzeni = await GetDistrictStatsAsync(subFrom, subTo, "EHLANZENI"),
            Bohlabelo = await GetDistrictStatsAsync(subFrom, subTo, "BOHLABELO"),
            GertSibande = await GetDistrictStatsAsync(subFrom, subTo, "GERT SIBANDE"),
            Nkangala = await GetDistrictStatsAsync(subFrom, subTo, "NKANGALA"),
        };
    }

    // ── Victim demographics ───────────────────────────────────────────────────
    private async Task<VictimDemographics> GetVictimDemographicsAsync(DateOnly from, DateOnly to)
    {
        var people = await _context.CrashPeople
            .Include(p => p.Person)         // FIX 6/7: Gender and Age live on Person
            .Include(p => p.Crash)
            .Where(p => p.Crash.CrashDate >= from &&
                        p.Crash.CrashDate <= to &&
                        p.SeverityOfInjury == "Fatal")
            .ToListAsync();

        // FIX 6: Gender is on the Person navigation property
        bool IsMale(CrashReport.Models.CrashPerson p) =>
            string.Equals(p.Person?.Gender, "M", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Person?.Gender, "MALE", StringComparison.OrdinalIgnoreCase);

        bool IsFemale(CrashReport.Models.CrashPerson p) =>
            string.Equals(p.Person?.Gender, "F", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Person?.Gender, "FEMALE", StringComparison.OrdinalIgnoreCase);

        // FIX 3: Role, not RoadUserType
        bool IsRole(CrashReport.Models.CrashPerson p, string role) =>
            string.Equals(p.Role, role, StringComparison.OrdinalIgnoreCase);

        return new VictimDemographics
        {
            TotalFatalities = people.Count,

            // FIX 7: Age is on Person nav property
            Age0to7 = people.Count(p => p.Person?.Age is >= 0 and <= 7),
            Age8to12 = people.Count(p => p.Person?.Age is >= 8 and <= 12),
            Age13to18 = people.Count(p => p.Person?.Age is >= 13 and <= 18),
            Age19to35 = people.Count(p => p.Person?.Age is >= 19 and <= 35),
            Age36Plus = people.Count(p => p.Person?.Age >= 36),

            MaleTotal = people.Count(IsMale),
            FemaleTotal = people.Count(IsFemale),

            // FIX 3/4/5: use Role throughout
            MaleDriver = people.Count(p => IsMale(p) && IsRole(p, "DRIVER")),
            FemaleDriver = people.Count(p => IsFemale(p) && IsRole(p, "DRIVER")),
            MalePassenger = people.Count(p => IsMale(p) && IsRole(p, "PASSENGER")),
            FemalePassenger = people.Count(p => IsFemale(p) && IsRole(p, "PASSENGER")),
            MalePedestrian = people.Count(p => IsMale(p) && IsRole(p, "PEDESTRIAN")),
            FemalePedestrian = people.Count(p => IsFemale(p) && IsRole(p, "PEDESTRIAN")),
            MaleCyclist = people.Count(p => IsMale(p) && IsRole(p, "CYCLIST")),
            FemaleCyclist = people.Count(p => IsFemale(p) && IsRole(p, "CYCLIST")),
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // FIX 2: derive district from the CrNo station prefix (mirrors StandbyReportDataService)
    private static readonly Dictionary<string, HashSet<string>> DistrictStations =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["EHLANZENI"] = new(StringComparer.OrdinalIgnoreCase) { "TONGA", "WHITE RIVER", "NELSPRUIT", "MASOYI", "MATSULU", "NGODWANA", "MHALA", "CALCUTTA", "MASHISHING", "BARBERTON", "KABOKWENI", "KANYAMAZANE", "KANAYAMAZANE", "HAZYVIEW", "SABIE", "GRASKOP", "ACORNHOEK", "KOMATIPOORT", "MALALANE", "SCHOEMANSDAL", "BUSHBUCKRIDGE", "KAMHLUSHWA" },
            ["BOHLABELO"] = new(StringComparer.OrdinalIgnoreCase) { "ACORNHOEK", "BUSHBUCKRIDGE", "MHALA", "GRASKOP", "SABIE", "KLASERIE" },
            ["GERT SIBANDE"] = new(StringComparer.OrdinalIgnoreCase) { "ERMELO", "SECUNDA", "STANDERTON", "BETHAL", "BALFOUR", "VOLKSRUST", "PIET RETIEF", "WAKKERSTROOM", "MORGENZON", "AMSTERDAM" },
            ["NKANGALA"] = new(StringComparer.OrdinalIgnoreCase) { "WITBANK", "MIDDELBURG", "DELMAS", "OGIES", "KRIEL", "BELFAST", "CAROLINA", "LEANDRA", "KWAMHLANGA", "BRONKHORSTSPRUIT" },
        };

    private static string GetDistrict(string? crNo)
    {
        if (string.IsNullOrWhiteSpace(crNo)) return "UNKNOWN";
        var station = crNo.Contains('-') ? crNo.Split('-')[0].Trim() : crNo.Trim();
        foreach (var kvp in DistrictStations)
            if (kvp.Value.Contains(station))
                return kvp.Key;
        return "UNKNOWN";
    }

    private static string GetDayRangeString(DateOnly from, DateOnly to)
    {
        string f = from.DayOfWeek.ToString().ToUpper();
        string t = to.DayOfWeek.ToString().ToUpper();
        return f == t ? f : $"{f} TO {t}";
    }

    // GET: /StandbyReport/Export
    [HttpGet("/StandbyReport/Export")]
    public async Task<IActionResult> ExportStandbyReport(
        DateOnly dateFrom,
        DateOnly dateTo,
        DateOnly? priorYearFrom = null,
        DateOnly? priorYearTo = null)
    {
        try
        {
            // Validate dates
            if (dateFrom > dateTo)
                return BadRequest("Start date must be before end date.");

            if (dateTo > DateOnly.FromDateTime(DateTime.Today))
                return BadRequest("Cannot generate report for future dates.");

            // Build the request object
            var request = new StandbyReportRequest
            {
                DateFrom = dateFrom,
                DateTo = dateTo,
                PriorYearFrom = priorYearFrom,
                PriorYearTo = priorYearTo
            };

            // Build the ViewModel
            var vm = await BuildStandbyReportViewModel(request);

            // Generate Word document
            var service = new StandbyReportWordService();
            byte[] docBytes = service.Generate(vm);

            string fileName = $"Weekly_Standby_Report_{vm.DateFrom:yyyy-MM-dd}_to_{vm.DateTo:yyyy-MM-dd}.docx";

            return File(docBytes,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                fileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error generating report: {ex.Message}");
        }
    }

    // GET: /StandbyReport
    [HttpGet("/StandbyReport")]
    public IActionResult StandbyReport()
    {
        // Initialize with default values (current week)
        var today = DateOnly.FromDateTime(DateTime.Today);
        var startOfWeek = today.AddDays(-(int)today.DayOfWeek + 1); // Monday
        var endOfWeek = startOfWeek.AddDays(6); // Sunday

        var request = new StandbyReportRequest
        {
            DateFrom = startOfWeek,
            DateTo = endOfWeek,
            PriorYearFrom = startOfWeek.AddYears(-1),
            PriorYearTo = endOfWeek.AddYears(-1)
        };

        return View("StandbyReport", request);
    }


}