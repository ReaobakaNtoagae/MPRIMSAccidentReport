namespace CrashReport.Services;

public enum LookupDeactivateOutcome { Deactivated, NotFound, UnknownTable }

/// <summary>
/// Extracts the "which lookup table does this soft-delete apply to" rule out of
/// LookupController.Deactivate's inline switch statement (remediation plan item 4)
/// so it has one home instead of being re-discoverable only by reading a
/// controller action, and so the same rule can be reused by a future admin UI or
/// API surface without duplicating the switch.
/// </summary>
public interface ILookupAdminService
{
    Task<LookupDeactivateOutcome> DeactivateAsync(string table, int id);
}
