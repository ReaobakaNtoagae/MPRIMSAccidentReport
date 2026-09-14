namespace CrashReport.Services.Import;

public sealed record ImportWorkbookIntakeCommand(Stream Content, string OriginalFileName,
    long DeclaredLength, string SelectedRegion, int ReportingMonth, int ReportingYear,
    string UploadedByUserId, string? Notes);

public sealed record ImportWorkbookIntakeResult(int ImportBatchId, bool AlreadyExists,
    string OriginalFileName, string FileSha256, string SelectedRegion,
    int ReportingMonth, int ReportingYear);

public interface IImportWorkbookIntakeService
{
    Task<ImportWorkbookIntakeResult> IntakeAsync(ImportWorkbookIntakeCommand command, CancellationToken cancellationToken = default);

}
public sealed class ImportWorkbookIntakeException : Exception
{
    public ImportWorkbookIntakeException(string userMessage) : base(userMessage) { }
    public ImportWorkbookIntakeException(string userMessage, Exception innerException) 
        :base(userMessage, innerException) { }
}