using CrashReport.Models;

namespace CrashReport.Services;

/// <summary>
/// Extracted from ContributoryFactorsController.Create (POST): the cross-entity
/// consistency rule that only one ContributoryFactor per crash may be flagged
/// IsMajorFactor. Kept here so ContributoryFactorsApiController doesn't have to
/// duplicate it, and so the rule lives in one place if it ever changes.
/// </summary>
public interface IContributoryFactorService
{
    /// <summary>
    /// If <paramref name="factor"/>.IsMajorFactor is true, unmarks every other
    /// IsMajorFactor factor already recorded against the same crash, then inserts
    /// <paramref name="factor"/> and saves. Returns the inserted factor
    /// (FactorId populated).
    /// </summary>
    Task<ContributoryFactor> CreateAsync(ContributoryFactor factor);
}
