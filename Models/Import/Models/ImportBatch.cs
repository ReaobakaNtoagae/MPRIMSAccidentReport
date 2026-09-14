using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CrashReport.Models.Import.Models
{
    [Table("import_batches")]
    public class ImportBatch
    {

        [Key]
        [Column("import_batch_id")]
        public int ImportBatchId { get; set; }

        [Column("original_file_name"), MaxLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;

        [Column("stored_file_reference"), MaxLength(500)]
        public string StoredFileReference { get; set; } = string.Empty;


        [Column("file_sha256"), MaxLength(64)]
        public string FileSha256 { get; set; } = string.Empty;

        [Column("selected_region"), MaxLength(50)]
        public string SelectedRegion { get; set; } = string.Empty;

        [Column("detected_template"), MaxLength(50)]
        public string? DetectedTemplate { get; set; }

        [Column("template_detection_confidence")]
        public decimal? TemplateDetectionConfidence { get; set; }

        [Column("reporting_month")]
        public byte ReportingMonth { get; set; }

        [Column("reporting_year")]
        public short ReportingYear { get; set; }

        [Column("status"), MaxLength(30)]
        public string Status { get; set; } = ImportBatchStatuses.Uploaded;

        [Column("notes"), MaxLength(1000)]
        public string? Notes { get; set; }

        [Column("uploaded_by_user_id"), MaxLength(450)]
        public string UploadedByUserId { get; set; } = string.Empty;

        [Column("uploaded_at")]
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [Column("reviewed_by_user_id"), MaxLength(450)]
        public string? ReviewedByUserId { get; set; }

        [Column("reviewed_at")]
        public DateTime? ReviewedAt { get; set; }

        [Column("imported_by_user_id"), MaxLength(450)]
        public string? ImportedByUserId { get; set; }

        [Column("imported_at")]
        public DateTime? ImportedAt { get; set; }

        [Column("failure_reason"), MaxLength(1000)]
        public string? FailureReason { get; set; }

        [Column("row_version")]
        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public ICollection<StagingCrashSummary> CrashRows { get; set; } = new List<StagingCrashSummary>();
        public ICollection<ImportDataQualityIssue> Issues { get; set; } = new List<ImportDataQualityIssue>();
        public ICollection<StagingImportDemographics> Demographics { get; set; } = new List<StagingImportDemographics>();

    }
}
