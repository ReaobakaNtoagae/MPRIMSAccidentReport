namespace CrashReport.ViewModels;

// ── Step 1: Crash Info ────────────────────────────────────────
public class CrashInfoViewModel
{
    public string? CasNo { get; set; }
    public string? CrNo { get; set; }
    public string? IncidentReportNo { get; set; }
    public string? CapturingNumber { get; set; }
    public string? CrashDate { get; set; }
    public string? CrashTime { get; set; }
    public string? ProvinceCode { get; set; }
    public short? SpeedLimitKmh { get; set; }
    public string? RoadNumber { get; set; }
    public string? KmMarker { get; set; }
    public byte NoOfVehiclesInvolved { get; set; } = 1;
    public byte NoOfAppendices { get; set; } = 0;
    public string? BriefDescription { get; set; }
}

// ── Step 2: Location ──────────────────────────────────────────
public class LocationViewModel
{
    public string? StreetRoadName { get; set; }
    public string? AreaType { get; set; }
    public bool? BuiltUpArea { get; set; }
    public decimal? GpsXCoordinate { get; set; }
    public decimal? GpsYCoordinate { get; set; }
    public string? IntersectionStreet { get; set; }
    public string? Suburb { get; set; }
    public string? CityTown { get; set; }
    public string? RoadFunctionalClassification { get; set; }
    public string? JunctionType { get; set; }
    public string? RoadLayout { get; set; }
    public string? RoadSurfaceType { get; set; }
    public string? RoadSurfaceCondition { get; set; }
}

// ── Step 3: Conditions ────────────────────────────────────────
public class ConditionsViewModel
{
    public string? LightCondition { get; set; }
    public List<string> WeatherConditions { get; set; } = new();
    public string? TrafficControlType { get; set; }
    public string? CrashType { get; set; }
    public bool? HitAndRun { get; set; }
    public string? RoadSegmentGrade { get; set; }
    public string? ObstructionType { get; set; }
    public string? RoadSignsCondition { get; set; }
    public string? RoadMarkingVisibility { get; set; }
}

// ── Step 4: Vehicle ───────────────────────────────────────────
public class VehicleEntryViewModel
{
    public string VehicleReference { get; set; } = "A";
    // Vehicle details
    public string? LicenceDiscNumber { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? Colour { get; set; }
    public string? VehicleCategory { get; set; }
    public string? SpecialFunction { get; set; }
    public string? VinNumber { get; set; }
    // Driver details
    public string? DriverIdType { get; set; }
    public string? DriverIdNumber { get; set; }
    public string? DriverSurname { get; set; }
    public string? DriverFullNames { get; set; }
    public string? DriverCellPhone { get; set; }
    public string? LicenceCode { get; set; }
    // Conduct
    public string? VehicleManoeuvre { get; set; }
    public string? SeatbeltUsed { get; set; }
    public string? AlcoholSuspected { get; set; }
    public string? AlcoholTestResult { get; set; }
    public string? DrugSuspected { get; set; }
    public string? PositionBeforeCrash { get; set; }
    public string? SeverityOfInjury { get; set; }
}

// ── Step 5: Persons ───────────────────────────────────────────
public class PersonEntryViewModel
{
    public string? IdNumber { get; set; }
    public string? Surname { get; set; }
    public string? FullNames { get; set; }
    public string? Gender { get; set; }
    public string? Role { get; set; }
    public string? VehicleReference { get; set; }
    public string? SeatingPosition { get; set; }
    public string? SeverityOfInjury { get; set; }
    public string? SeatbeltHelmet { get; set; }
    public string? Hospital { get; set; }
}

// ── Step 6: Contributory Factors ─────────────────────────────
public class FactorEntryViewModel
{
    public string FactorCategory { get; set; } = string.Empty;
    public string FactorDescription { get; set; } = string.Empty;
    public bool IsMajorFactor { get; set; }
}

// ── Master ViewModel (whole form) ────────────────────────────
public class CrashReportFormViewModel
{
    public CrashInfoViewModel CrashInfo { get; set; } = new();
    public LocationViewModel Location { get; set; } = new();
    public ConditionsViewModel Conditions { get; set; } = new();
    public List<VehicleEntryViewModel> Vehicles { get; set; } = new();
    public List<PersonEntryViewModel> Persons { get; set; } = new();
    public List<FactorEntryViewModel> Factors { get; set; } = new();
}