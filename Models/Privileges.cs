namespace CrashReport.Security;

/// <summary>
/// Named application privileges.
///
/// Each privilege is stored as a claim (type = "privilege", value = Privilege.X)
/// on a role via AspNetRoleClaims. When a user logs in, ASP.NET Core loads
/// all claims from every role the user belongs to, so privilege checks are
/// automatic and require no per-request database hit.
///
/// To protect a controller action:
///   [Authorize(Policy = Privileges.Crashes.Create)]
///
/// To check in Razor:
///   @if (User.HasClaim("privilege", Privileges.Reports.Monthly)) { ... }
/// </summary>
public static class Privileges
{
    public const string ClaimType = "privilege";

    public static class Crashes
    {
        public const string View = "Crashes.View";
        public const string Create = "Crashes.Create";
        public const string Edit = "Crashes.Edit";
        public const string Delete = "Crashes.Delete";
    }

    public static class Import
    {
        public const string Excel = "Import.Excel";
    }

    public static class Reports
    {
        public const string Standby = "Reports.Standby";
        public const string Monthly = "Reports.Monthly";
        public const string FiveYear = "Reports.FiveYear";
        public const string Quarterly = "Reports.Quarterly";
    }

    public static class Admin
    {
        public const string Users = "Admin.Users";
        public const string Roles = "Admin.Roles";
        public const string Lookups = "Admin.Lookups";
    }

    /// <summary>
    /// All privileges in display order, used to render the
    /// privilege assignment grid in the Roles management UI.
    /// </summary>
    public static readonly IReadOnlyList<(string Value, string Label, string Group)> All =
    [
        (Crashes.View,      "View crash records",          "Crash Management"),
        (Crashes.Create,    "Create new crash records",    "Crash Management"),
        (Crashes.Edit,      "Edit existing crash records", "Crash Management"),
        (Crashes.Delete,    "Delete crash records",        "Crash Management"),
        (Import.Excel,      "Import from Excel workbooks", "Data Import"),
        (Reports.Standby,   "Generate standby report",     "Reports"),
        (Reports.Monthly,   "Generate monthly memo",       "Reports"),
        (Reports.FiveYear,  "Generate 5-year report",      "Reports"),
        (Reports.Quarterly, "Generate quarterly report",   "Reports"),
        (Admin.Users,       "Manage users",                "Administration"),
        (Admin.Roles,       "Manage roles and privileges", "Administration"),
        (Admin.Lookups,     "Manage lookup tables",        "Administration"),
    ];

    /// <summary>Default privilege sets assigned during seeding.</summary>
    public static class Defaults
    {
        public static readonly string[] Administrator =
        [
            Crashes.View, Crashes.Create, Crashes.Edit, Crashes.Delete,
            Import.Excel,
            Reports.Standby, Reports.Monthly, Reports.FiveYear, Reports.Quarterly,
            Admin.Users, Admin.Roles, Admin.Lookups
        ];

        public static readonly string[] Supervisor =
        [
            Crashes.View,
            Reports.Standby, Reports.Monthly, Reports.FiveYear, Reports.Quarterly
        ];

        public static readonly string[] DataCapturer =
        [
            Crashes.View, Crashes.Create, Crashes.Edit,
            Import.Excel
        ];
    }
}