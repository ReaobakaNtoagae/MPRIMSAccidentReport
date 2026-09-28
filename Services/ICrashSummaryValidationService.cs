using CrashReport.Models;
using CrashReport.Models.Dtos;

namespace CrashReport.Services;

/// <summary>
/// The ~150 lines of validation and count-rollup logic that used to be duplicated,
/// almost line-for-line, between CreateSummaryController.CreateSummary (Quick
/// Capture create) and CrashesController.EditSummary (Quick Capture edit) — both
/// operate on the same CrashSummary + CrashSummaryVehicle + CrashSummaryInjury
/// shape, so a bug fix or rule change previously had to be made twice, in two
/// different files, and it was easy to miss one (see remediation plan item 4).
///
/// Every method returns null on success and an error message on failure, matching
/// the `if (error != null) return Json(new { success = false, message = error })`
/// pattern both callers already used — this is a mechanical extraction, not a
/// rewrite, so the validation rules themselves are unchanged.
/// </summary>
public interface ICrashSummaryValidationService
{
    /// <summary>Vehicle type required, vehicle numbers unique. Returns the distinct vehicle numbers on success.</summary>
    string? ValidateVehicles(List<VehicleEntryInput> vehicles, out List<byte> vehicleNumbers);

    /// <summary>
    /// Per-injury rules: valid severity, role required unless Fatal, age/gender/race
    /// ranges, and Driver/Passenger-must-reference-a-submitted-vehicle vs
    /// Pedestrian/Cyclist-must-not. Also bounds the combined person/vehicle count
    /// to 255 (both counts are stored in byte columns downstream).
    /// </summary>
    string? ValidateInjuries(List<InjuryEntryInput> injuries, List<VehicleEntryInput> vehicles, List<byte> vehicleNumbers);

    /// <summary>
    /// Cross-checks the role-based-editor's typed Serious/Slight totals against the
    /// actual injury rows. <paramref name="enforceLegacyNonRoleTotalsCheck"/> is true
    /// only for CreateSummary — EditSummary already reconciles the legacy
    /// "TotalsOnlyEditor" case separately, earlier in its own flow, so it does not
    /// re-run this branch. Preserved as a flag rather than unified so this extraction
    /// doesn't silently change either caller's behavior.
    /// </summary>
    string? ValidateInjuryTotalsAgreement(
        List<InjuryEntryInput> injuries, CrashSummary model, bool roleBasedEditor, bool enforceLegacyNonRoleTotalsCheck);

    /// <summary>
    /// Recomputes all 12 fatal/serious/slight × driver/passenger/pedestrian/cyclist
    /// counts, VehicleCount, FatalitiesTotal, and the fatal-only age/gender/race
    /// demographic rollup, and writes them onto <paramref name="summary"/>. Always
    /// zeroes the demographic fields first — a no-op on a freshly-constructed
    /// CrashSummary (Create), required on an existing one (Edit) since those fields
    /// are incremented, not assigned.
    /// </summary>
    void ApplyInjuryRollup(CrashSummary summary, List<InjuryEntryInput> injuries, int vehicleCount);

    /// <summary>
    /// Inserts the CrashSummaryVehicle rows for a summary and returns the
    /// VehicleNumber → generated VehicleId map needed to link injuries to the
    /// vehicle each one occupied. Does not open or commit a transaction — the
    /// caller (CreateSummary/EditSummary) owns that, since this participates in
    /// the same DbContext instance/transaction via constructor injection.
    /// </summary>
    Task<Dictionary<byte, int>> SaveVehiclesAsync(int summaryId, List<VehicleEntryInput> vehicles);

    /// <summary>Inserts the CrashSummaryInjury rows, linking each to its vehicle via the map SaveVehiclesAsync returned.</summary>
    Task SaveInjuriesAsync(int summaryId, List<InjuryEntryInput> injuries, Dictionary<byte, int> vehicleNumberToId);

    /// <summary>
    /// Deletes existing injuries then vehicles for a summary, in that order —
    /// required, not a style choice: crash_summary_injuries → crash_summaries and
    /// crash_summary_vehicles → crash_summaries are NO ACTION (not CASCADE) FKs,
    /// and injuries also FK to crash_summary_vehicles, so injuries must go first.
    /// Used by both EditSummary (delete-then-reinsert) and DeleteSummary.
    /// </summary>
    Task DeleteVehiclesAndInjuriesAsync(int summaryId);
}
