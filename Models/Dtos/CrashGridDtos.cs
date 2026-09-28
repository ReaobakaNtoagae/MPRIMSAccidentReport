namespace CrashReport.Models.Dtos;

/// <summary>Exactly the anonymous-object shape CrashesController.Grid used to build inline.</summary>
public class CrashGridRowDto
{
    public int? CrashId { get; set; }
    public int? SummaryId { get; set; }
    public string CrNo { get; set; } = "";
    public string CasNo { get; set; } = "";
    public string ArNo { get; set; } = "";
    public string Station { get; set; } = "";
    public string District { get; set; } = "";
    public string Date { get; set; } = "";
    public string? Time { get; set; }
    public string Route { get; set; } = "";
    public string Location { get; set; } = "";
    public string CrashType { get; set; } = "";
    public byte VehicleCount { get; set; }
    public int Fatalities { get; set; }
    public int Serious { get; set; }
    public int Slight { get; set; }
    public string Severity { get; set; } = "";
    public string Source { get; set; } = "";
}

public class CrashGridResult
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<CrashGridRowDto> Rows { get; set; } = new();
}
