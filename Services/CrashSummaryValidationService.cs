using CrashReport.Data;
using CrashReport.Models;
using CrashReport.Models.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Services;

public class CrashSummaryValidationService : ICrashSummaryValidationService
{
    private readonly AppDbContext _context;

    public CrashSummaryValidationService(AppDbContext context)
    {
        _context = context;
    }

    private static readonly string[] ValidSeverities = { "Fatal", "Serious", "Slight" };
    private static readonly string[] ValidRoles = { "Driver", "Passenger", "Pedestrian", "Cyclist" };
    private static readonly string[] ValidAgeGroups = { "0-7", "8-12", "13-18", "19-35", "36+" };
    private static readonly string[] ValidRaces = { "B", "C", "I", "W", "O" };

    public string? ValidateVehicles(List<VehicleEntryInput> vehicles, out List<byte> vehicleNumbers)
    {
        vehicleNumbers = vehicles.Select(v => v.VehicleNumber).ToList();

        foreach (var v in vehicles)
        {
            if (string.IsNullOrWhiteSpace(v.VehicleTypeCode))
                return $"Vehicle {v.VehicleNumber}: vehicle type is required.";
        }

        if (vehicleNumbers.Distinct().Count() != vehicleNumbers.Count)
            return "Each vehicle must have a unique vehicle number.";

        return null;
    }

    public string? ValidateInjuries(List<InjuryEntryInput> injuries, List<VehicleEntryInput> vehicles, List<byte> vehicleNumbers)
    {
        
        if (injuries.Count > 255 || vehicles.Count > 255)
            return "A quick record supports up to 255 people and vehicles.";

        foreach (var inj in injuries)
        {
            if (inj.AgeGroupCode != null && !ValidAgeGroups.Contains(inj.AgeGroupCode))
                return "Select a valid age group.";

            if (!ValidSeverities.Contains(inj.Severity))
                return $"Each casualty needs a valid severity (Fatal, Serious, or Slight) — got '{inj.Severity}'.";

            
            if (inj.Severity != "Fatal" && !ValidRoles.Contains(inj.Role))
                return $"Each casualty needs a valid role (Driver, Passenger, Pedestrian, or Cyclist) — got '{inj.Role}'.";
            if (inj.Role is not null && !ValidRoles.Contains(inj.Role))
                return "Select a valid road user when fatality details are supplied.";

            if (inj.Age.HasValue && (inj.Age.Value < 0 || inj.Age.Value > 120))
                return $"Age {inj.Age} is out of range (0–120).";

            if (!string.IsNullOrEmpty(inj.Gender) && inj.Gender != "M" && inj.Gender != "F")
                return "Gender must be M or F if provided.";

            if (!string.IsNullOrEmpty(inj.Race) && !ValidRaces.Contains(inj.Race))
                return "Race must be B, C, I, W, or O if provided.";

           
            if (inj.Role == "Driver" || inj.Role == "Passenger")
            {
                
                if (inj.VehicleNumber.HasValue && !vehicleNumbers.Contains(inj.VehicleNumber.Value))
                    return $"A {inj.Role.ToLower()} casualty must reference one of the vehicles entered above.";
            }
            else if (inj.VehicleNumber != null)
            {
                return $"A {inj.Role?.ToLower()} casualty cannot be linked to a vehicle.";
            }
        }

        return null;
    }

    public string? ValidateInjuryTotalsAgreement(
        List<InjuryEntryInput> injuries, CrashSummary model, bool roleBasedEditor, bool enforceLegacyNonRoleTotalsCheck)
    {
        var seriousRows = injuries.Count(i => i.Severity == "Serious");
        var slightRows = injuries.Count(i => i.Severity == "Slight");

        var mismatch = roleBasedEditor
            ? model.SeriousInjuriesTotal != seriousRows || model.SlightInjuriesTotal != slightRows
            : enforceLegacyNonRoleTotalsCheck &&
              ((seriousRows > 0 && model.SeriousInjuriesTotal.HasValue && model.SeriousInjuriesTotal != seriousRows) ||
               (slightRows > 0 && model.SlightInjuriesTotal.HasValue && model.SlightInjuriesTotal != slightRows));

        return mismatch ? "Injury totals must match the supplied casualty details." : null;
    }

    public void ApplyInjuryRollup(CrashSummary summary, List<InjuryEntryInput> injuries, int vehicleCount)
    {
        int Count(string severity, string role) =>
            injuries.Count(i => i.Severity == severity && i.Role == role);

        summary.VehicleCount = (byte)vehicleCount;
        summary.FatalitiesTotal = injuries.Count(i => i.Severity == "Fatal");

        summary.FatalDrivers = (byte)Count("Fatal", "Driver");
        summary.FatalPassengers = (byte)Count("Fatal", "Passenger");
        summary.FatalPedestrians = (byte)Count("Fatal", "Pedestrian");
        summary.FatalCyclists = (byte)Count("Fatal", "Cyclist");

        summary.SeriousDrivers = (byte)Count("Serious", "Driver");
        summary.SeriousPassengers = (byte)Count("Serious", "Passenger");
        summary.SeriousPedestrians = (byte)Count("Serious", "Pedestrian");
        summary.SeriousCyclists = (byte)Count("Serious", "Cyclist");

        summary.SlightDrivers = (byte)Count("Slight", "Driver");
        summary.SlightPassengers = (byte)Count("Slight", "Passenger");
        summary.SlightPedestrians = (byte)Count("Slight", "Pedestrian");
        summary.SlightCyclists = (byte)Count("Slight", "Cyclist");

      
        summary.FatalMale = 0; summary.FatalFemale = 0;
        summary.FatalAge0to7 = 0; summary.FatalAge8to12 = 0; summary.FatalAge13to18 = 0;
        summary.FatalAge19to35 = 0; summary.FatalAge36Plus = 0;
        summary.FatalAfrican = 0; summary.FatalColoured = 0; summary.FatalIndian = 0;
        summary.FatalWhite = 0; summary.FatalOtherRace = 0;

        foreach (var inj in injuries.Where(i => i.Severity == "Fatal"))
        {
            if (!inj.Age.HasValue)
            {
                switch (inj.AgeGroupCode)
                {
                    case "0-7": summary.FatalAge0to7++; break;
                    case "8-12": summary.FatalAge8to12++; break;
                    case "13-18": summary.FatalAge13to18++; break;
                    case "19-35": summary.FatalAge19to35++; break;
                    case "36+": summary.FatalAge36Plus++; break;
                }
            }
            else
            {
                var age = inj.Age.Value;
                if (age <= 7) summary.FatalAge0to7++;
                else if (age <= 12) summary.FatalAge8to12++;
                else if (age <= 18) summary.FatalAge13to18++;
                else if (age <= 35) summary.FatalAge19to35++;
                else summary.FatalAge36Plus++;
            }

            if (inj.Gender == "M") summary.FatalMale++;
            else if (inj.Gender == "F") summary.FatalFemale++;

            switch (inj.Race)
            {
                case "B": summary.FatalAfrican++; break;
                case "C": summary.FatalColoured++; break;
                case "I": summary.FatalIndian++; break;
                case "W": summary.FatalWhite++; break;
                case "O": summary.FatalOtherRace++; break;
            }
        }
    }

    public async Task<Dictionary<byte, int>> SaveVehiclesAsync(int summaryId, List<VehicleEntryInput> vehicles)
    {
        var vehicleNumberToId = new Dictionary<byte, int>();
        foreach (var v in vehicles)
        {
            var entity = new CrashSummaryVehicle
            {
                SummaryId = summaryId,
                VehicleNumber = v.VehicleNumber,
                VehicleTypeCode = v.VehicleTypeCode,
                VehicleTypeName = v.VehicleTypeName,
                Make = string.IsNullOrWhiteSpace(v.Make) ? null : v.Make.Trim(),
                Registration = string.IsNullOrWhiteSpace(v.Registration) ? null : v.Registration
            };
            _context.CrashSummaryVehicles.Add(entity);
            await _context.SaveChangesAsync();
            vehicleNumberToId[v.VehicleNumber] = entity.VehicleId;
        }
        return vehicleNumberToId;
    }

    public async Task SaveInjuriesAsync(int summaryId, List<InjuryEntryInput> injuries, Dictionary<byte, int> vehicleNumberToId)
    {
        foreach (var inj in injuries)
        {
            _context.CrashSummaryInjuries.Add(new CrashSummaryInjury
            {
                SummaryId = summaryId,
                VehicleId = inj.VehicleNumber.HasValue ? vehicleNumberToId[inj.VehicleNumber.Value] : null,
                Severity = inj.Severity,
                Role = inj.Role,
                Age = (byte?)inj.Age,
                AgeGroupCode = inj.AgeGroupCode,
                Gender = string.IsNullOrEmpty(inj.Gender) ? null : inj.Gender,
                Race = string.IsNullOrEmpty(inj.Race) ? null : inj.Race
            });
        }
        if (injuries.Count > 0)
            await _context.SaveChangesAsync();
    }

    public async Task DeleteVehiclesAndInjuriesAsync(int summaryId)
    {
        var existingInjuries = _context.CrashSummaryInjuries.Where(i => i.SummaryId == summaryId);
        _context.CrashSummaryInjuries.RemoveRange(existingInjuries);
        await _context.SaveChangesAsync();

        var existingVehicles = _context.CrashSummaryVehicles.Where(v => v.SummaryId == summaryId);
        _context.CrashSummaryVehicles.RemoveRange(existingVehicles);
        await _context.SaveChangesAsync();
    }
}
