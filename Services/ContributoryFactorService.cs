using CrashReport.Data;
using CrashReport.Models;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Services;

public class ContributoryFactorService : IContributoryFactorService
{
    private readonly AppDbContext _context;

    public ContributoryFactorService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ContributoryFactor> CreateAsync(ContributoryFactor factor)
    {
        if (factor.IsMajorFactor)
        {
            var existing = await _context.ContributoryFactors
                .Where(f => f.CrashId == factor.CrashId && f.IsMajorFactor)
                .ToListAsync();
            existing.ForEach(f => f.IsMajorFactor = false);
        }

        _context.ContributoryFactors.Add(factor);
        await _context.SaveChangesAsync();
        return factor;
    }
}
