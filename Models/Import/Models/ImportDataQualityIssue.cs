using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CrashReport.Models.Import.Models
{
    [Table("import_data_quality_issues")]
    public class ImportDataQualityIssue
    {
        [Key, Column("issue_id")] public long IssueId { get; set; }
        [Column("import_batch_id")] public int ImportBatchId { get; set; }
        [Column("staging_summary_id")] public long? StagingSummaryId { get; set; }
        [Column("field_name"), MaxLength(100)] public string? FieldName { get; set; }
        [Column("issue_code"), MaxLength(100)] public string IssueCode { get; set; } = string.Empty;
        [Column("severity"), MaxLength(20)] public string Severity { get; set; } = ImportIssueSeverities.Warning;
        [Column("is_blocking")] public bool IsBlocking { get; set; }
        [Column("description"), MaxLength(1000)] public string Description { get; set; } = string.Empty;
        [Column("original_value"), MaxLength(1000)] public string? OriginalValue { get; set; }
        [Column("suggested_value"), MaxLength(1000)] public string? SuggestedValue { get; set; }
        [Column("resolution_status"), MaxLength(30)] public string ResolutionStatus { get; set; } = ImportIssueResolutionStatuses.Open;
        [Column("resolution_notes"), MaxLength(1000)] public string? ResolutionNotes { get; set; }
        [Column("resolved_by_user_id"), MaxLength(450)] public string? ResolvedByUserId { get; set; }
        [Column("resolved_at")] public DateTime? ResolvedAt { get; set; }
        // Consultation details stay on the finding so the question, source value and
        // eventual answer remain part of one audit trail.
        [Column("requires_data_owner")] public bool RequiresDataOwner { get; set; }
        [Column("referred_at")] public DateTime? ReferredAt { get; set; }
        [Column("referred_by_user_id"), MaxLength(450)] public string? ReferredByUserId { get; set; }
        [Column("referred_to"), MaxLength(200)] public string? ReferredTo { get; set; }
        [Column("response_due_at")] public DateTime? ResponseDueAt { get; set; }
        [Column("referral_question"), MaxLength(2000)] public string? ReferralQuestion { get; set; }
        [Column("data_owner_response"), MaxLength(2000)] public string? DataOwnerResponse { get; set; }
        [Column("responded_at")] public DateTime? RespondedAt { get; set; }
        [Column("response_recorded_by_user_id"), MaxLength(450)] public string? ResponseRecordedByUserId { get; set; }
        [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ImportBatch ImportBatch { get; set; } = null!;
        public StagingCrashSummary? StagingSummary { get; set; }

    }
}
