namespace CrashReport.Models;

public class CrashGridFilter
{


    public DateOnly From { get; set; } = new DateOnly(2020, 1, 1);
    public DateOnly To { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public string? District { get; set; }
    public string? Severity { get; set; }  
    public string? Source { get; set; }  
    public string? Search { get; set; }    

    public string SortBy { get; set; } = "Date";
    public bool SortDesc { get; set; } = true;

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}