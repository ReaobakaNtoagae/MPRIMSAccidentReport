using CrashReport.Models;
using CrashReport.Models.Dtos;

namespace CrashReport.Services;

/// <summary>
/// The full-capture (CR1 form) save path — previously ~600 lines living directly
/// inside HomeController.Submit, the single largest block of business logic in
/// the app, with two other actions (HomeController.Edit POST, HomeController.Delete)
/// duplicating smaller pieces of crash persistence right next to it. Extracted so
/// both the MVC HomeController and the new HomeApiController can call the same
/// save/delete logic instead of maintaining two copies (remediation plan item 4).
///
/// This is a mechanical extraction, not a rewrite — every field mapping, every
/// GetString/GetShort/GetByte/GetDecimal/GetDateOnly/GetBool tolerant-JSON-parsing
/// helper, and the allow-and-warn duplicate-CrNo-as-CrashSummary behavior are
/// preserved exactly as they were in HomeController.Submit.
/// </summary>
public interface ICrashCaptureService
{
    /// <summary>
    /// Parses, validates, and saves a full CR1 form submission (the raw multi-shape
    /// JSON blob the capture wizard posts) inside one transaction. Never throws for
    /// an ordinary validation failure — those come back as
    /// CrashSubmitOutcome.ValidationFailed with the error list; only an unexpected
    /// save-time failure produces SaveFailed (the transaction is rolled back before
    /// this returns).
    /// </summary>
    Task<CrashSubmitResult> SubmitAsync(string formJson);

    /// <summary>
    /// Updates only the top-level Crash fields the Edit form binds (CasNo, CrNo,
    /// IncidentReportNo, CapturingNumber, CrashDate, CrashTime, NoOfAppendices,
    /// NoOfVehiclesInvolved, ProvinceCode, SpeedLimitKmh, RoadNumber, KmMarker,
    /// BriefDescription) — this was always a thin, direct EF update, not something
    /// with real business logic behind it, but both HomeController.Edit and a new
    /// API equivalent need the exact same "does this id exist" handling.
    /// </summary>
    Task<bool> UpdateCoreFieldsAsync(int id, Crash formValues);

    /// <summary>
    /// CrashesController.Create's own POST action -- a second, separate "create a
    /// crash" path from HomeController.Submit, but a much thinner one: it only
    /// ever set the same top-level fields Edit does (same [Bind] list), with no
    /// location/vehicles/people/etc. Duplicate-CrNo checked the same way
    /// SubmitAsync checks it (allow-and-warn against CrashSummary is NOT done
    /// here, matching the original action, which only ever checked against other
    /// Crashes). Returns the new CrashId on success, or a validation error message
    /// naming the duplicate CrNo.
    /// </summary>
    Task<(bool success, string? duplicateError, int crashId)> CreateCoreOnlyAsync(Crash crash);

    /// <summary>
    /// Full cascade delete of a crash and every dependent row (locations,
    /// conditions, weather, vehicles + their damages and drivers/passengers,
    /// pedestrian/bicyclist detail, contributory factors, dangerous goods,
    /// witnesses, official-use records, sketches/attachments) in FK-safe order.
    /// Returns false if the crash doesn't exist. This is the same cascade
    /// HomeController.Delete already did correctly — CrashesController.DeleteConfirmed
    /// is switched to use this too, since it was previously doing a bare
    /// `_context.Crashes.Remove(crash)` with none of this cleanup, which would throw
    /// an FK-violation exception the moment that action was ever actually exercised
    /// against a crash with any child rows (found while extracting this, not
    /// something the original review flagged).
    /// </summary>
    Task<bool> DeleteCrashAsync(int crashId);
}
