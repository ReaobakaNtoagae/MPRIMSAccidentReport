using CrashReport.Data;

namespace CrashReport.Services;

public class LookupAdminService : ILookupAdminService
{
    private readonly AppDbContext _context;
    public LookupAdminService(AppDbContext context) => _context = context;

    public async Task<LookupDeactivateOutcome> DeactivateAsync(string table, int id)
    {
        switch (table.ToLower())
        {
            case "stations":
                var s = await _context.SapsStations.FindAsync(id);
                if (s == null) return LookupDeactivateOutcome.NotFound;
                s.IsActive = false;
                break;
            case "locations":
                var l = await _context.LookupLocations.FindAsync(id);
                if (l == null) return LookupDeactivateOutcome.NotFound;
                l.IsActive = false;
                break;
            case "routes":
                var r = await _context.LookupRoutes.FindAsync(id);
                if (r == null) return LookupDeactivateOutcome.NotFound;
                r.IsActive = false;
                break;
            case "crashtypes":
                var ct = await _context.LookupCrashTypes.FindAsync(id);
                if (ct == null) return LookupDeactivateOutcome.NotFound;
                ct.IsActive = false;
                break;
            case "vehicletypes":
                var vt = await _context.LookupVehicleTypes.FindAsync(id);
                if (vt == null) return LookupDeactivateOutcome.NotFound;
                vt.IsActive = false;
                break;
            default:
                return LookupDeactivateOutcome.UnknownTable;
        }

        await _context.SaveChangesAsync();
        return LookupDeactivateOutcome.Deactivated;
    }
}
