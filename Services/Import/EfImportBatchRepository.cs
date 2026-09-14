using CrashReport.Data;
using CrashReport.Models.Import.Models;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Services.Import
{
    public class EfImportBatchRepository : IImportBatchRepository
    {
        private readonly AppDbContext _context;
        public EfImportBatchRepository(AppDbContext context) => _context  = context;

       public Task<ImportBatch?> FindByFingerprintAsync(string sha256, string selectedRegion, int reportingMonth, int reportingYear, CancellationToken cancellationToken) =>
            _context.ImportBatches.AsNoTracking().FirstOrDefaultAsync(x =>
            x.FileSha256 == sha256 &&
            x.SelectedRegion == selectedRegion &&
            x.ReportingMonth == reportingMonth &&
            x.ReportingYear == reportingYear, cancellationToken);
       
       public async Task AddAsync(ImportBatch batch, CancellationToken cancellationToken)
        {
            _context.ImportBatches.Add(batch);
            await _context.SaveChangesAsync(cancellationToken);
        }

       
    }
}
