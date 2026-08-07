using CrashReport.Data;
using CrashReport.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Services;

public interface ICrashFormValidationService
{
    Task<List<string>> ValidateAsync(CrashReportFormViewModel vm, int? existingCrashId);
}


public class CrashFormValidationService : ICrashFormValidationService
{
    private readonly AppDbContext _context;

    private static readonly HashSet<short> ValidSpeedLimits = new() { 30, 40, 50, 60, 80, 100, 120 };

    private static readonly HashSet<string> PedestrianCrashTypes =
        new(StringComparer.OrdinalIgnoreCase) { "PEDESTRIAN", "PED" };

    public CrashFormValidationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<string>> ValidateAsync(CrashReportFormViewModel vm, int? existingCrashId)
    {
        var errors = new List<string>();
        var ci = vm.CrashInfo;
        var today = DateOnly.FromDateTime(DateTime.Today);

        // ── Step 1: Crash Info ─────────────────────────────────

        // UPDATED: CrNo used to be optional as long as CasNo was present --
        // a leftover from before CR No. became the single, manually-entered
        // real-world identifier (the number already on the physical form,
        // also used for revenue/payment lookups). It's now required
        // outright; CasNo remains optional alongside it.
        if (string.IsNullOrWhiteSpace(ci.CrNo))
            errors.Add("CR No. is required.");

        if (!DateOnly.TryParse(ci.CrashDate, out var crashDate))
        {
            errors.Add("Crash date is required and must be a valid date.");
        }
        else
        {
            if (crashDate > today)
                errors.Add("Crash date cannot be in the future.");

            if (crashDate < today.AddYears(-5))
                errors.Add("Crash date cannot be more than 5 years in the past.");
        }

        if (!string.IsNullOrWhiteSpace(ci.CrNo))
        {
            var duplicate = await _context.Crashes
                .AnyAsync(c => c.CrNo == ci.CrNo.Trim() &&
                               (existingCrashId == null || c.CrashId != existingCrashId));
            if (duplicate)
                errors.Add($"CR No. '{ci.CrNo.Trim()}' already exists in the database.");

            // NOTE: this intentionally does NOT also check CrashSummaries.
            // A crash existing in both Crashes and CrashSummaries under the
            // same CrNo is expected, routine behaviour given the AS-IS
            // workflow (a Cost Centre can legitimately Quick Add a summary
            // before an officer digitizes the full form for the same
            // crash) -- that case is handled separately in Submit() as an
            // allow-and-warn note, not a blocking validation error here.
        }

        if (ci.SpeedLimitKmh.HasValue && !ValidSpeedLimits.Contains(ci.SpeedLimitKmh.Value))
            errors.Add($"Speed limit {ci.SpeedLimitKmh} km/h is not a valid South African " +
                       $"speed limit. Must be one of: {string.Join(", ", ValidSpeedLimits.Order())}.");

        // ── Step 2: Location ───────────────────────────────────

        var loc = vm.Location;
        bool hasLocation = !string.IsNullOrWhiteSpace(loc?.StreetRoadName) ||
                           !string.IsNullOrWhiteSpace(loc?.Suburb) ||
                           !string.IsNullOrWhiteSpace(loc?.CityTown);
        if (!hasLocation)
            errors.Add("At least one location field is required (Street/Road Name, Suburb, or City/Town).");

        bool hasLat = loc?.GpsXCoordinate.HasValue == true;
        bool hasLon = loc?.GpsYCoordinate.HasValue == true;
        if (hasLat != hasLon)
        {
            errors.Add("Both GPS latitude and longitude are required if either is entered.");
        }
        else if (hasLat && hasLon)
        {
            var lat = loc!.GpsXCoordinate!.Value;
            var lon = loc!.GpsYCoordinate!.Value;
            if (lat < -35.0m || lat > -22.0m)
                errors.Add($"GPS latitude {lat} is outside South Africa (valid range: -35.0 to -22.0).");
            if (lon < 16.0m || lon > 33.0m)
                errors.Add($"GPS longitude {lon} is outside South Africa (valid range: 16.0 to 33.0).");
        }


        // ── Step 3: Conditions ─────────────────────────────────

        if (string.IsNullOrWhiteSpace(vm.Conditions?.CrashType))
            errors.Add("Crash type is required.");

        if (string.IsNullOrWhiteSpace(vm.Conditions?.LightCondition))
            errors.Add("Light condition is required.");

        if (vm.Conditions?.WeatherConditions == null ||
            vm.Conditions.WeatherConditions.Count == 0)
            errors.Add("At least one weather condition must be selected.");

        // ── Step 4: Vehicles ────────────────────────────────────

        if (vm.Vehicles == null || vm.Vehicles.Count == 0)
            errors.Add("At least one vehicle must be entered.");
        else
        {
        
            if (vm.Vehicles.Count != ci.NoOfVehiclesInvolved)
                errors.Add($"Number of vehicles entered ({vm.Vehicles.Count}) does not match " +
                           $"'No. of Vehicles Involved' ({ci.NoOfVehiclesInvolved}) on Step 1.");

            var refs = vm.Vehicles.Select(v => v.VehicleReference?.Trim().ToUpper()).ToList();
            if (refs.Distinct().Count() != refs.Count)
                errors.Add("Each vehicle must have a unique Vehicle Reference (A, B, C…).");

            foreach (var ve in vm.Vehicles)
            {
                var vRef = ve.VehicleReference?.Trim().ToUpper() ?? "(unknown)";

                if (string.IsNullOrWhiteSpace(ve.VehicleReference))
                    errors.Add($"Vehicle reference is required for each vehicle.");

                if (!string.IsNullOrWhiteSpace(ve.DriverSurname))
                {
                    if (string.IsNullOrWhiteSpace(ve.DriverIdNumber))
                        errors.Add($"Vehicle {vRef}: driver ID number is required when a driver surname is entered.");
                    else if (!IsValidSaId(ve.DriverIdNumber))
                        errors.Add($"Vehicle {vRef}: driver ID number '{ve.DriverIdNumber}' is not a valid South African ID number.");

                    if (string.IsNullOrWhiteSpace(ve.LicenceCode))
                        errors.Add($"Vehicle {vRef}: licence code is required when a driver is named.");
                }

                if (!string.IsNullOrWhiteSpace(ve.AlcoholTestResult) &&
                    !string.Equals(ve.AlcoholSuspected, "Yes", StringComparison.OrdinalIgnoreCase))
                    errors.Add($"Vehicle {vRef}: alcohol test result entered but 'Alcohol Suspected' is not set to Yes.");

                
               
            }
        }

        // ── Step 5: Persons ─────────────────────────────────────

        var vehicleRefs = (vm.Vehicles ?? new())
            .Select(v => v.VehicleReference?.Trim().ToUpper())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToHashSet()!;

        var driverVehicleRefs = (vm.Vehicles ?? new())
            .Where(v => !string.IsNullOrWhiteSpace(v.DriverSurname) &&
                        !string.IsNullOrWhiteSpace(v.VehicleReference))
            .Select(v => v.VehicleReference!.Trim().ToUpper())
            .ToHashSet();

        foreach (var pe in vm.Persons ?? new())
        {
            if (string.IsNullOrWhiteSpace(pe.Surname)) continue;


            if (string.IsNullOrWhiteSpace(pe.Role))
                errors.Add($"Person '{pe.Surname}': role is required (Driver/Passenger/Pedestrian/Bicyclist).");

            if (string.IsNullOrWhiteSpace(pe.SeverityOfInjury))
                errors.Add($"Person '{pe.Surname}': severity of injury is required.");

            var role = pe.Role?.Trim();

            if ((string.Equals(role, "Driver", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(role, "Passenger", StringComparison.OrdinalIgnoreCase)) &&
                string.IsNullOrWhiteSpace(pe.VehicleReference))
            {
                errors.Add($"Person '{pe.Surname}': vehicle reference is required for {role} role.");
            }

            if (!string.IsNullOrWhiteSpace(pe.VehicleReference))
            {
                var pRef = pe.VehicleReference.Trim().ToUpper();
                if (!vehicleRefs.Contains(pRef))
                    errors.Add($"Person '{pe.Surname}': vehicle reference '{pe.VehicleReference}' does not match any vehicle entered in Step 4.");
            }

            if (string.Equals(role, "Driver", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(pe.VehicleReference))
            {
                var pRef = pe.VehicleReference.Trim().ToUpper();
                if (driverVehicleRefs.Contains(pRef))
                    errors.Add($"Person '{pe.Surname}': vehicle {pe.VehicleReference} already has a driver assigned in Step 4.");
            }

            if (!string.IsNullOrWhiteSpace(pe.IdNumber) && !IsValidSaId(pe.IdNumber))
                errors.Add($"Person '{pe.Surname}': ID number '{pe.IdNumber}' is not a valid South African ID number.");

            if (string.Equals(pe.SeverityOfInjury, "Fatal", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(pe.Gender))
                errors.Add($"Person '{pe.Surname}': gender is required for fatal victims (needed for demographic reports).");
        }

        // ── Step 6: Contributory Factors ────────────────────────

        var majorFactorCount = (vm.Factors ?? new()).Count(f => f.IsMajorFactor);
        if (majorFactorCount > 1)
            errors.Add($"Only one contributory factor can be marked as the major factor ({majorFactorCount} are currently marked).");

        

        // ── Cross-step rules ─────────────────────────────────────

        var crashType = vm.Conditions?.CrashType;
        if (!string.IsNullOrWhiteSpace(crashType) &&
            PedestrianCrashTypes.Contains(crashType))
        {
            var allPersonRoles = (vm.Persons ?? new())
                .Select(pe => pe.Role?.Trim())
                .ToList();

            bool hasPedestrian = allPersonRoles.Any(r =>
                string.Equals(r, "Pedestrian", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r, "Bicyclist", StringComparison.OrdinalIgnoreCase));

            if (!hasPedestrian)
                errors.Add($"Crash type '{crashType}' requires at least one Pedestrian or Bicyclist victim in Step 5.");
        }

        return errors;
    }

    private static bool IsValidSaId(string id)
    {
        id = id?.Trim() ?? "";
        if (id.Length != 13 || !id.All(char.IsDigit)) return false;

        if (!int.TryParse(id.Substring(0, 2), out var yy)) return false;
        if (!int.TryParse(id.Substring(2, 2), out var month) || month < 1 || month > 12) return false;
        if (!int.TryParse(id.Substring(4, 2), out var day) || day < 1 || day > 31) return false;

        var digits = id.Select(c => c - '0').ToArray();
        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            if (i % 2 == 0)
            {
                sum += digits[i];
            }
            else
            {
                var d = digits[i] * 2;
                sum += d > 9 ? d - 9 : d;
            }
        }
        var checkDigit = (10 - (sum % 10)) % 10;
        return checkDigit == digits[12];
    }
}