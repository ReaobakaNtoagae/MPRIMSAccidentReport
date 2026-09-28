using CrashReport.Data;
using CrashReport.Models;
using CrashReport.Models.Dtos;
using CrashReport.Security;
using CrashReport.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CrashReport.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _context;
    private readonly MonthlyMemoDataService _memoData;
    private readonly ICrashCaptureService _capture;

    // ICrashFormValidationService and IWebHostEnvironment used to be injected here
    // directly for Submit's validation and attachment-saving code -- both moved to
    // ICrashCaptureService along with the rest of Submit, so neither is needed in
    // this controller anymore.
    public HomeController(AppDbContext context, MonthlyMemoDataService memoData, ICrashCaptureService capture)
    {
        _context = context;
        _memoData = memoData;
        _capture = capture;
    }

    public async Task<IActionResult> Index()
    {
        var now = DateTime.Today;
        var monthStart = new DateOnly(now.Year, now.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var allTime = await _memoData.LoadAsync(new DateOnly(2020, 1, 1), monthEnd);

        bool Has(string privilege) => User.HasClaim(Privileges.ClaimType, privilege);

        var canView = Has(Privileges.Crashes.View);
        var canCreateFull = Has(Privileges.Crashes.Create);
        var canQuickAdd = Has(Privileges.Crashes.CreateSummary);
        var canEdit = Has(Privileges.Crashes.Edit);
        var canDelete = Has(Privileges.Crashes.Delete);
        var canImport = Has(Privileges.Import.Excel);
        var canStandby = Has(Privileges.Reports.Standby);
        var canMonthly = Has(Privileges.Reports.Monthly);
        var canFiveYear = Has(Privileges.Reports.FiveYear);
        var canQuarterly = Has(Privileges.Reports.Quarterly);
        var canAnyReport = canStandby || canMonthly || canFiveYear || canQuarterly;
        var canAdminister = Has(Privileges.Admin.Users) || Has(Privileges.Admin.Roles) || Has(Privileges.Admin.Lookups);

        ViewBag.CanView = canView;
        ViewBag.CanCreateFull = canCreateFull;
        ViewBag.CanQuickAdd = canQuickAdd;
        ViewBag.CanEdit = canEdit;
        ViewBag.CanDelete = canDelete;
        ViewBag.CanImport = canImport;
        ViewBag.CanStandby = canStandby;
        ViewBag.CanMonthly = canMonthly;
        ViewBag.CanFiveYear = canFiveYear;
        ViewBag.CanQuarterly = canQuarterly;
        ViewBag.CanAnyReport = canAnyReport;
        ViewBag.CanAdminister = canAdminister;

        // Role label — display only, drives which dashboard layout/copy renders.
        // Precedence: System Administrator > Provincial Staff > Regional Staff >
        // Cost Centre Administrator > SAPS Officer.
        var roleLabel =
            User.IsInRole("System Administrator") ? "System Administrator" :
            User.IsInRole("Provincial Staff") ? "Provincial Staff" :
            User.IsInRole("Regional Staff") ? "Regional Staff" :
            User.IsInRole("Cost Centre Administrator") ? "Cost Centre Administrator" :
            "SAPS Officer";
        ViewBag.RoleLabel = roleLabel;

        // Scope claims (District/Station), added by AppUserClaimsPrincipalFactory.
        var userDistrict = User.FindFirst("District")?.Value;
        var userStation = User.FindFirst("Station")?.Value;
        ViewBag.UserDistrict = userDistrict;
        ViewBag.UserStation = userStation;

        ViewBag.DashboardMode =
            (roleLabel == "System Administrator" || roleLabel == "Provincial Staff") ? "analytics" :
            roleLabel == "Regional Staff" ? "review" :
            "capture"; // Cost Centre Administrator, SAPS Officer

        // ── Real scoping, not just a banner label. Station-scoped roles
        // (SAPS Officer, Cost Centre Administrator) see only their own
        // station; Regional Staff sees their own district; analytics
        // mode (System Administrator, Provincial Staff) stays unscoped
        // -- that's correct for those roles, not an oversight. Filters
        // the already-loaded Row list directly, since Row.Station and
        // Row.District are already resolved by LoadAsync -- no need for
        // a second query or a separate lookup service injected here. ──
        var scopedAllTime = allTime;
        if (roleLabel == "SAPS Officer" || roleLabel == "Cost Centre Administrator")
        {
            if (!string.IsNullOrEmpty(userStation))
                scopedAllTime = allTime
                    .Where(r => string.Equals(r.Station, userStation, StringComparison.OrdinalIgnoreCase))
                    .ToList();
        }
        else if (roleLabel == "Regional Staff")
        {
            if (!string.IsNullOrEmpty(userDistrict))
                scopedAllTime = allTime
                    .Where(r => string.Equals(r.District, userDistrict, StringComparison.OrdinalIgnoreCase))
                    .ToList();
        }

        var scopedThisMonth = scopedAllTime.Where(r => r.Date >= monthStart && r.Date <= monthEnd).ToList();

        // Also fixes a second real bug: the previous version counted only
        // _context.Crashes / _context.CrashPeople directly, which are
        // manually-captured records ONLY -- Quick Add and imported
        // CrashSummaries were silently excluded from every KPI. LoadAsync
        // already merges both sources correctly, so switching to it here
        // fixes the undercount at the same time as the scoping.
        ViewBag.TotalCrashes = scopedAllTime.Count;
        ViewBag.FatalCount = scopedAllTime.Sum(r => r.Fatalities);
        ViewBag.SeriousCount = scopedAllTime.Sum(r => r.Serious);
        ViewBag.SlightCount = scopedAllTime.Sum(r => r.Slight);

        // Previously referenced by the view but never set -- the
        // computed "thisMonth" value was discarded without ever
        // reaching ViewBag, so this line always rendered blank.
        ViewBag.ThisMonthCrashes = scopedThisMonth.Count;
        ViewBag.ThisMonthFatal = scopedThisMonth.Sum(r => r.Fatalities);
        ViewBag.CurrentMonth = now.ToString("MMMM");

        // For capture/review modes, hand the scoped recent-activity rows
        // straight to the view -- no separate AJAX call needed, and no
        // risk of the client re-fetching an unscoped list. Analytics mode
        // keeps its existing separate /Crashes/Grid call (large page
        // size, unscoped, used for the district/severity charts too).
        if (roleLabel != "System Administrator" && roleLabel != "Provincial Staff")
        {
            var recent = scopedAllTime
                .OrderByDescending(r => r.Date)
                .ThenByDescending(r => r.Time)
                .Take(8)
                .Select(r => new
                {
                    r.CrashId,
                    r.SummaryId,
                    r.CrNo,
                    r.District,
                    r.Station,
                    r.Fatalities,
                    r.Serious,
                    r.Source
                });
            ViewBag.ScopedRecentActivityJson = JsonSerializer.Serialize(recent);
        }

        return View();
    }

    public IActionResult Create() => View();
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var crash = await _context.Crashes.FindAsync(id);
        if (crash == null) return NotFound();
        return View("~/Views/Crashes/Edit.cshtml", crash);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Privileges.Crashes.Edit)]
    public async Task<IActionResult> Edit(int id,
        [Bind("CrashId,CasNo,CrNo,IncidentReportNo,CapturingNumber,CrashDate,CrashTime," +
              "NoOfAppendices,NoOfVehiclesInvolved,ProvinceCode,SpeedLimitKmh," +
              "RoadNumber,KmMarker,BriefDescription")] Crash crash)
    {
        if (id != crash.CrashId) return NotFound();
        if (ModelState.IsValid)
        {
            try
            {
                if (!await _capture.UpdateCoreFieldsAsync(id, crash)) return NotFound();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Crashes.Any(c => c.CrashId == id)) return NotFound();
                throw;
            }
            TempData["SuccessMessage"] = $"Crash report #{id} updated successfully.";
            return RedirectToAction(nameof(Index));
        }
        return View("~/Views/Crashes/Edit.cshtml", crash);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Privileges.Crashes.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _capture.DeleteCrashAsync(id))
        {
            TempData["ErrorMessage"] = $"Crash report #{id} not found.";
            return RedirectToAction(nameof(Index));
        }

        TempData["SuccessMessage"] = $"Crash report #{id} deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Privileges.Crashes.Create)]
    public async Task<IActionResult> Submit([FromForm] string formJson)
    {
        // The actual parse/validate/save logic lives in ICrashCaptureService now
        // (Services/ICrashCaptureService.cs) -- this action just translates its
        // result into the TempData + redirect shape the Create/CreateWithErrors/
        // Index views already expect. See Controllers/Api/HomeApiController.cs for
        // the JSON equivalent, which returns the same CrashSubmitResult as a body
        // instead of a redirect.
        var result = await _capture.SubmitAsync(formJson);

        switch (result.Outcome)
        {
            case CrashSubmitOutcome.EmptyForm:
                TempData["ErrorMessage"] = "No form data received.";
                return RedirectToAction(nameof(Create));

            case CrashSubmitOutcome.ValidationFailed:
                TempData["ValidationErrors"] = result.ValidationErrors;
                TempData["FormJson"] = result.FormJsonForRedisplay;
                return RedirectToAction(nameof(CreateWithErrors));

            case CrashSubmitOutcome.SaveFailed:
                TempData["ErrorMessage"] = result.ErrorMessage;
                return RedirectToAction(nameof(Create));

            default: // Success
                TempData["SuccessMessage"] = result.SuccessMessage;
                return RedirectToAction(nameof(Index));
        }
    }


    public IActionResult CreateWithErrors()
    {
        if (TempData["ValidationErrors"] is List<string> errors)
        {
            ViewBag.ValidationErrors = errors;
        }
        if (TempData["FormJson"] is string formJson)
        {
            ViewBag.FormJson = formJson;
        }
        return View("Create");
    }

    // BuildCrash/SaveRelatedEntities used to live here -- dead code, never called
    // from anywhere in this file (Submit uses its own inline JsonDocument parsing
    // exclusively, now moved to ICrashCaptureService). Confirmed unused before
    // removal, not just assumed.

    public async Task<IActionResult> Insights()
    {
        var roleLabel =
            User.IsInRole("System Administrator") ? "System Administrator" :
            User.IsInRole("Provincial Staff") ? "Provincial Staff" :
            User.IsInRole("Regional Staff") ? "Regional Staff" :
            User.IsInRole("Cost Centre Administrator") ? "Cost Centre Administrator" :
            "SAPS Officer";

        // Insights only exists for roles operating at a scale where a
        // pattern is worth showing -- a single station's handful of
        // records wouldn't have one. SAPS Officer and Cost Centre
        // Administrator never had a nav link to this page, but the
        // action itself is also blocked directly, not just hidden.
        if (roleLabel != "System Administrator" && roleLabel != "Provincial Staff" && roleLabel != "Regional Staff")
            return Forbid();

        var userDistrict = User.FindFirst("District")?.Value;

        var to = DateOnly.FromDateTime(DateTime.Today);
        var from = new DateOnly(to.Year, 1, 1);

        var scopeDistrict = roleLabel == "Regional Staff" ? userDistrict : null;

        var vm = await _memoData.BuildInsightsAsync(from, to, scopeDistrict);

        ViewBag.RoleLabel = roleLabel;
        ViewBag.UserDistrict = userDistrict;
        ViewBag.DashboardMode =
            (roleLabel == "System Administrator" || roleLabel == "Provincial Staff") ? "analytics" : "review";

        return View(vm);
    }
}
