namespace CrashReport.Models;


public class Row
{

    public int? CrashId { get; set; }

    
    public int? SummaryId { get; set; }

    public string CrNo { get; set; } = "";
    public string CasNo { get; set; } = "";
    public string ArNo { get; set; } = "";         
    public string Station { get; set; } = "";
    public string District { get; set; } = "";     
    public string ProvinceCode { get; set; } = ""; 
    public string Location { get; set; } = "";     
    public string Source { get; set; } = "";     

    public DateOnly Date { get; set; }
    public TimeOnly? Time { get; set; }
    public string Route { get; set; } = "";
    public string CrashType { get; set; } = "";

    public List<string> VehicleCats { get; set; } = new();
    public byte VehicleCount { get; set; }

    public int Fatalities { get; set; }
    public int Serious { get; set; }
    public int Slight { get; set; }

    public int FatalDrivers { get; set; }
    public int FatalPassengers { get; set; }
    public int FatalPedestrians { get; set; }
    public int FatalCyclists { get; set; }
    public int SeriousDrivers { get; set; }
    public int SeriousPassengers { get; set; }
    public int SeriousPedestrians { get; set; }
    public int SeriousCyclists { get; set; }
    public int SlightDrivers { get; set; }
    public int SlightPassengers { get; set; }
    public int SlightPedestrians { get; set; }
    public int SlightCyclists { get; set; }

    // Convenience — single severity bucket for the grid, since the raw data is
    // split by role (driver/passenger/pedestrian/cyclist) rather than one value.
    public string OverallSeverity =>
        Fatalities > 0 ? "Fatal" :
        Serious > 0 ? "Serious" : "Minor";
}