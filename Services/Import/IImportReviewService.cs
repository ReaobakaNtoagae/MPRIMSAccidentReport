namespace CrashReport.Services.Import;

/// <summary>
/// Records human decisions made after automatic cleaning. Automation detects and suggests;
/// this service controls which reviewed rows may move toward production.
/// </summary>
public interface IImportReviewService
{
    Task UpdateRowAsync(UpdateStagingRowCommand command, string userId,
        CancellationToken cancellationToken = default);
    Task ResolveIssueAsync(long issueId, string decision, string? notes, string userId,
        CancellationToken cancellationToken = default);
    Task ReferIssueAsync(ReferImportIssueCommand command, string userId,
        CancellationToken cancellationToken = default);
    Task RecordDataOwnerResponseAsync(RecordDataOwnerResponseCommand command, string userId,
        CancellationToken cancellationToken = default);
    Task ApproveRowAsync(long stagingSummaryId, string? notes, string userId,
        CancellationToken cancellationToken = default);
    Task RejectRowAsync(long stagingSummaryId, string? notes, string userId,
        CancellationToken cancellationToken = default);
    Task ApproveBatchAsync(int importBatchId, string userId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Contains only the editable, cleaned values. The Original* columns on the staging
/// entity are deliberately absent, so a reviewer can never overwrite the audit copy.
/// </summary>
public sealed record UpdateStagingRowCommand(
    long StagingSummaryId,
    string? Station,
    string? ArNumber,
    string? CasNumber,
    string? CrashDate,
    string? CrashTime,
    string? Route,
    string? Location,
    string? CrashType,
    string? Notes);

// These commands contain the consultation input without coupling the service to MVC.
public sealed record ReferImportIssueCommand(long IssueId, string ReferredTo,
    string Question, DateTime? ResponseDueAt);
public sealed record RecordDataOwnerResponseCommand(long IssueId, string Response);

public static class ImportIssueDecisions
{
    public const string ApplySuggestion = "ApplySuggestion";
    public const string Accept = "Accept";
    public const string Dismiss = "Dismiss";
}
