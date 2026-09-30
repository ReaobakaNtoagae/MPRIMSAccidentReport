using CrashReport.Models;

namespace CrashReport.Services;


public interface IContributoryFactorService
{
    
    Task<ContributoryFactor> CreateAsync(ContributoryFactor factor);
}
