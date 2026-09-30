namespace CrashReport.Services;

public enum LookupDeactivateOutcome { Deactivated, NotFound, UnknownTable }

public interface ILookupAdminService
{
    Task<LookupDeactivateOutcome> DeactivateAsync(string table, int id);
}
