using CrashReport.Data;
using CrashReport.Models;
using CrashReport.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Serialization;
using CrashReport.Security;
using CrashReport.Options;
using CrashReport.Services.Import;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

builder.Services.AddScoped<ForcePasswordChangeFilter>();

var mvcBuilder = builder.Services.AddControllersWithViews(options =>
{

    var policy = new AuthorizationPolicyBuilder()
                     .RequireAuthenticatedUser()
                     .Build();
    options.Filters.Add(new AuthorizeFilter(policy));

    // Authorization filters (the AuthorizeFilter above) always run before
    // action filters in the MVC pipeline, regardless of registration order —
    // so this only ever sees requests ASP.NET Core has already confirmed
    // are authenticated.
    options.Filters.Add<ForcePasswordChangeFilter>();

    // Adds the privilege-level [Authorize(Policy = ...)] the app is currently
    // missing on several controllers (Persons/Vehicles/Witnesses/
    // ContributoryFactors, most report-generation endpoints, CrashesController's
    // own Create action) without hand-editing each file — see
    // Models/ActionPrivilegeMap.cs for the full reasoning and every mapping.
    // Also means any future action with neither an [Authorize] attribute nor a
    // map entry makes the app refuse to start, rather than silently running
    // open to any logged-in user.
    options.Conventions.Add(new MapDrivenAuthorizationConvention());
})
.AddNewtonsoftJson(options =>
    options.SerializerSettings.ContractResolver =
        new DefaultContractResolver());

// Views are normally compiled into the app assembly at build time, so a
// .cshtml edit on disk has no effect until a full rebuild + restart. That's
// invisible during ordinary development and easy to mistake for "the change
// didn't work". Runtime compilation re-parses views on each request instead,
// so a browser refresh is enough — enabled only in Development so production
// still ships fully precompiled views.
if (builder.Environment.IsDevelopment())
{
    mvcBuilder.AddRazorRuntimeCompilation();
}


builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register the persisted staged-import pipeline. Each stage has one job: accept
// the file, parse/validate it, record human review decisions, then commit it.
builder.Services.Configure<ImportWorkbookOptions>(
    builder.Configuration.GetSection(ImportWorkbookOptions.SectionName));
builder.Services.AddScoped<IImportBatchRepository, EfImportBatchRepository>();
builder.Services.AddScoped<IImportWorkbookIntakeService, ImportWorkbookIntakeService>();
builder.Services.AddScoped<IWorkbookTemplateDetector, WorkbookTemplateDetector>();
builder.Services.AddScoped<IWorkbookCrashRowParser, WorkbookCrashRowParser>();
builder.Services.AddScoped<IWorkbookSummaryParser, WorkbookSummaryParser>();
builder.Services.AddScoped<IStagingCrashQualityValidator, StagingCrashQualityValidator>();
builder.Services.AddScoped<IImportBatchProcessingService, ImportBatchProcessingService>();
builder.Services.AddScoped<IImportReviewService, ImportReviewService>();
builder.Services.AddScoped<IImportCommitService, ImportCommitService>();
builder.Services.AddScoped<StandbyReportDataService>();
builder.Services.AddScoped<StandbyReportWordService>();
builder.Services.AddScoped<MonthlyMemoDataService>();
builder.Services.AddScoped<MonthlyMemoDocService>();
builder.Services.AddScoped<QuarterlyReportDataService>();
builder.Services.AddScoped<FiveYearReportDataService>();
builder.Services.AddScoped<FiveYearReportDocService>();
builder.Services.AddScoped<IStationDistrictLookup, StationDistrictLookup>();
builder.Services.AddScoped<ICrashFormValidationService, CrashFormValidationService>();
builder.Services.AddScoped<ICrashSummaryValidationService, CrashSummaryValidationService>();
builder.Services.AddScoped<ICrashCaptureService, CrashCaptureService>();
builder.Services.AddScoped<ICrashGridService, CrashGridService>();
builder.Services.AddScoped<ILookupAdminService, LookupAdminService>();

// Services backing the new API controllers under Controllers/Api/ — each mirrors
// business logic that previously lived only inline in an MVC controller action.
builder.Services.AddScoped<IContributoryFactorService, ContributoryFactorService>();
builder.Services.AddScoped<IUserAdminService, UserAdminService>();
builder.Services.AddScoped<IRoleAdminService, RoleAdminService>();
builder.Services.AddScoped<IUserClaimsSyncService, UserClaimsSyncService>();
builder.Services.AddScoped<IReportsHubMappingService, ReportsHubMappingService>();

builder.Services.AddMemoryCache();
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 12;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;

    options.Password.RequiredUniqueChars = 4;
    

    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.AllowedForNewUsers = true;

    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization(options =>
{
    foreach (var (value, _, _) in Privileges.All)
    {
        options.AddPolicy(value, policy =>
            policy.RequireClaim(Privileges.ClaimType, value));
    }
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

await SeedData.InitialiseAsync(app);

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
