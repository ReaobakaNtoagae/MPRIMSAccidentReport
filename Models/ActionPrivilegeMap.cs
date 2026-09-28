namespace CrashReport.Security;

/// <summary>
/// The privilege required for every controller action that doesn't already carry
/// its own explicit [Authorize]/[AllowAnonymous] attribute. Read together with
/// MapDrivenAuthorizationConvention, which is the thing that actually enforces this.
///
/// An action that already has an explicit attribute is left alone — it doesn't need
/// an entry here, and this map doesn't override it. This file exists specifically
/// for the actions that, today, have neither: they fall through to "must be logged
/// in" only, via the global AuthorizeFilter in Program.cs, with no privilege check
/// at all. That's the gap flagged in the remediation plan (item 3/5) — e.g. any
/// logged-in user, regardless of role, could reach Persons/Vehicles/Witnesses/
/// ContributoryFactors, most of the /api/lookup/* write endpoints, and — the most
/// serious one found while actually building this map — CrashesController.Create
/// (POST), which inserts a new crash record with no privilege check whatsoever.
///
/// Built by reading every controller in this solution (2026-09-27), not assumed —
/// several of these assignments correct an earlier, less careful draft. Notably:
/// Persons/Vehicles/Witnesses/ContributoryFactors are NOT blanket-mapped to
/// Crashes.View — their Create/Edit/Delete actions need Crashes.Create/Edit/Delete
/// specifically, same as CrashesController itself. And LookupController is NOT
/// blanket-mapped to Admin.Lookups — most of its actions are read-only reference
/// data (districts, stations, cost centres, crash/vehicle types) consumed by
/// ordinary data-entry forms across the app (the CR1 form, Quick Capture, Crashes
/// search) and have to stay open to any logged-in user. Only its actual write
/// endpoints (AddStation, AddLocation, AddRoute, AddCrashType, AddVehicleType,
/// Deactivate) need Admin.Lookups. Getting this wrong in either direction either
/// reopens the security gap or breaks every non-admin user's forms.
/// </summary>
public static class ActionPrivilegeMap
{
    // "{ControllerName}.{ActionName}" -> required privilege. ActionName is the
    // *effective* action name after any [ActionName(...)] override — e.g.
    // CrashesController's GET Delete(int? id) and POST DeleteConfirmed(int id)
    // (which carries [ActionName("Delete")]) are both keyed "Crashes.Delete",
    // which is correct: both should require the same privilege.
    public static readonly Dictionary<string, string> ActionOverrides = new()
    {
        // Crashes — Index/Search/GetAll/FilterOptions/Details/Grid fall through to
        // the ControllerDefault (View) below; these need more than that.
        ["Crashes.Create"]        = Privileges.Crashes.Create,  // GET form + POST save — POST had NO check at all
        ["Crashes.Edit"]          = Privileges.Crashes.Edit,    // GET only; POST Edit already has its own [Authorize]
        ["Crashes.Delete"]        = Privileges.Crashes.Delete,  // GET confirm page; POST DeleteConfirmed already has its own [Authorize]
        ["Crashes.EditSummary"]   = Privileges.Crashes.Edit,    // GET only; POST EditSummary already has its own [Authorize]

        // Home (the CR1 form)
        ["Home.Create"]           = Privileges.Crashes.Create,
        ["Home.CreateWithErrors"] = Privileges.Crashes.Create,  // redisplays Create with validation errors after a failed Submit
        ["Home.Edit"]             = Privileges.Crashes.Edit,    // GET only; POST Edit already has its own [Authorize]

        // Persons / Vehicles — read actions fall through to the ControllerDefault
        // (View) below; Create/Edit/Delete need the matching Crashes privilege,
        // same as CrashesController's own actions.
        ["Persons.Create"]        = Privileges.Crashes.Create,
        ["Persons.Edit"]          = Privileges.Crashes.Edit,
        ["Persons.Delete"]        = Privileges.Crashes.Delete,
        ["Vehicles.Create"]       = Privileges.Crashes.Create,
        ["Vehicles.Edit"]         = Privileges.Crashes.Edit,
        ["Vehicles.Delete"]       = Privileges.Crashes.Delete,

        // Witnesses / ContributoryFactors — no read-only actions exist on these
        // two controllers at all, only attach-to-crash mutations.
        ["Witnesses.Create"]           = Privileges.Crashes.Edit,
        ["Witnesses.Delete"]           = Privileges.Crashes.Edit,
        ["ContributoryFactors.Create"] = Privileges.Crashes.Edit,
        ["ContributoryFactors.Delete"] = Privileges.Crashes.Edit,

        // CreateSummary (Quick Capture) — Index wasn't attributed; the
        // CreateSummary GET/POST actions already carry their own [Authorize].
        ["CreateSummary.Index"]   = Privileges.Crashes.CreateSummary,

        // The four report controllers each only had Index attributed — Preview/
        // Download/Export/ExportHtml (the actions that actually return report
        // data or generate the .docx) had no privilege check at all.
        ["StandbyReport.Preview"]    = Privileges.Reports.Standby,
        ["StandbyReport.Export"]     = Privileges.Reports.Standby,
        ["StandbyReport.ExportHtml"] = Privileges.Reports.Standby,
        ["FiveYearReport.Preview"]   = Privileges.Reports.FiveYear,
        ["FiveYearReport.Download"]  = Privileges.Reports.FiveYear,
        ["MemoReport.Preview"]       = Privileges.Reports.Monthly,
        ["MemoReport.Download"]      = Privileges.Reports.Monthly,
        ["QuarterlyReport.Preview"]  = Privileges.Reports.Quarterly,
        ["QuarterlyReport.Download"] = Privileges.Reports.Quarterly,

        // /api/lookup/* — only the mutating endpoints. See the class remarks above
        // for why the read endpoints are deliberately NOT here.
        ["Lookup.AddStation"]     = Privileges.Admin.Lookups,
        ["Lookup.AddLocation"]    = Privileges.Admin.Lookups,
        ["Lookup.AddRoute"]       = Privileges.Admin.Lookups,
        ["Lookup.AddCrashType"]   = Privileges.Admin.Lookups,
        ["Lookup.AddVehicleType"] = Privileges.Admin.Lookups,
        ["Lookup.Deactivate"]     = Privileges.Admin.Lookups,
    };

    // Controller -> privilege applied to every one of its actions that isn't
    // already covered by an explicit attribute or an entry in ActionOverrides.
    public static readonly Dictionary<string, string> ControllerDefaults = new()
    {
        ["Crashes"]  = Privileges.Crashes.View,   // Index, Search, GetAll, FilterOptions, Details, Grid
        ["Persons"]  = Privileges.Crashes.View,   // Index, GetAll, Details
        ["Vehicles"] = Privileges.Crashes.View,   // Index, GetAll, Details
        ["Lookups"]  = Privileges.Admin.Lookups,  // the admin "Lookup Management" page (distinct from the Lookup API controller)
    };

    // Deliberately open to any logged-in user, no extra privilege — reviewed and
    // intentional, not a silent fallback. Anything NOT covered by an explicit
    // attribute, ActionOverrides, ControllerDefaults, or this list makes the app
    // refuse to start (see MapDrivenAuthorizationConvention) rather than run with
    // an unreviewed gap.
    public static readonly HashSet<string> AuthenticatedOnly = new(StringComparer.Ordinal)
    {
        "Home.Index", "Home.Insights",
        "Account.ChangePassword",
        "Reports.Index",   // ReportsController — a placeholder view with no data; possibly dead, not confirmed
        "Lookup.Districts", "Lookup.CostCentres", "Lookup.SearchStations",
        "Lookup.SearchLocations", "Lookup.SearchRoutes", "Lookup.SearchCrashTypes",
        "Lookup.SearchVehicleTypes", "Lookup.FormOptions", "Lookup.FixedValues",
    };
}
