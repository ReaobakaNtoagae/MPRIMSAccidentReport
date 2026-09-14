namespace CrashReport.Services.Import;

/// <summary>
/// Performs the final, explicit move from reviewed staging data into production tables.
/// </summary>
public interface IImportCommitService
{
    Task<ImportCommitResult> CommitAsync(int importBatchId, string userId,
        CancellationToken cancellationToken = default);
}

public sealed record ImportCommitResult(
    int ImportBatchId,
    int ImportedRows,
    int RejectedRows,
    int VehicleRows,
    int InjuryRows,
    int DemographicRows);
