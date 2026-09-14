using CrashReport.Models.Import.Models;

namespace CrashReport.Services.Import
{
    public interface IImportBatchRepository
    {
        // A fingerprint lookup legitimately returns null when this workbook is new.
        Task<ImportBatch?> FindByFingerprintAsync(string sha256, string selectedRegion, int reportingMonth, int reportingYear, CancellationToken cancellationToken);
        Task AddAsync(ImportBatch batch, CancellationToken cancellationToken);
    }
}
