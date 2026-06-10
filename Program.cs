using CrashReport.Data;
using CrashReport.Services;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Serialization;


var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

builder.Services.AddControllersWithViews()
    .AddNewtonsoftJson(options =>
        options.SerializerSettings.ContractResolver =
           new DefaultContractResolver());


builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.AddScoped<ExcelImportService>();
builder.Services.AddScoped<WordexportService>();
builder.Services.AddScoped<StandbyReportDataService>();
builder.Services.AddScoped<StandbyReportWordService>();
builder.Services.AddScoped<MonthlyMemoDataService>();
builder.Services.AddScoped<MonthlyMemoDocService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
