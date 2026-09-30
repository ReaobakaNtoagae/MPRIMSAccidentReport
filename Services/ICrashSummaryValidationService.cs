using CrashReport.Models;
using CrashReport.Models.Dtos;

namespace CrashReport.Services;


public interface ICrashSummaryValidationService
{
    
    string? ValidateVehicles(List<VehicleEntryInput> vehicles, out List<byte> vehicleNumbers);

    
    string? ValidateInjuries(List<InjuryEntryInput> injuries, List<VehicleEntryInput> vehicles, List<byte> vehicleNumbers);

    
    string? ValidateInjuryTotalsAgreement(
        List<InjuryEntryInput> injuries, CrashSummary model, bool roleBasedEditor, bool enforceLegacyNonRoleTotalsCheck);

   
    void ApplyInjuryRollup(CrashSummary summary, List<InjuryEntryInput> injuries, int vehicleCount);

   
    Task<Dictionary<byte, int>> SaveVehiclesAsync(int summaryId, List<VehicleEntryInput> vehicles);

   
    Task SaveInjuriesAsync(int summaryId, List<InjuryEntryInput> injuries, Dictionary<byte, int> vehicleNumberToId);

    Task DeleteVehiclesAndInjuriesAsync(int summaryId);
}
