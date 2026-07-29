using CrashReport.Data;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Services;

public interface IStationDistrictLookup
{
    // Returns the whole station→district map in one query, rather than a
    // per-station GetDistrict(name) call — LoadAsync needs to resolve this
    // for every row, and one bulk query beats N round-trips per request.
    Task<IReadOnlyDictionary<string, string>> GetAllAsync();
}

public class StationDistrictLookup : IStationDistrictLookup
{
    private readonly AppDbContext _context;

    public StationDistrictLookup(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync()
    {
        var stations = await _context.SapsStations
            .Include(s => s.DistrictLookup)
            .Where(s => s.IsActive)
            .ToListAsync();

        // Prefer the real FK (DistrictLookup.DistrictName) over the legacy
        // free-text District column — falls back to the text column only if
        // a station hasn't had its district_id backfilled yet.
        return stations
            .GroupBy(s => Normalize(s.StationName))
            .ToDictionary(
                g => g.Key,
                g => g.First().DistrictLookup?.DistrictName
                     ?? g.First().District
                     ?? "Unknown"
            );
    }

    // Case/whitespace-insensitive key so "Nelspruit", "NELSPRUIT", and
    // " Nelspruit " all resolve to the same station.
    public static string Normalize(string? stationName) =>
        (stationName ?? "").Trim().ToUpperInvariant();
}