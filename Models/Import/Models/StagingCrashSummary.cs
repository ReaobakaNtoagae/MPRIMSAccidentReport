using CrashReport.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace CrashReport.Models.Import.Models
{
    [Table("staging_crash_summaries")]
    public class StagingCrashSummary
    {

        [Key]
        // Primary key for the temporary row. This is intentionally separate from the
        // production CrashSummary ID because staging rows may still be rejected.
        [Column("staging_summary_id")] public long StagingSummaryId { get; set; }

        [Column("import_batch_id")] public int ImportBatchId { get; set; }
        [Column("worksheet_name"), MaxLength(128)] public string WorksheetName { get; set; } = string.Empty;

        [Column("source_row_number")] public int SourceRowNumber { get; set; }

        [Column("raw_row_json")] public string RawRowJson { get; set; } = "{}";

        [Column("original_station"), MaxLength(250)] public string? OriginalStation { get; set; }
        [Column("station"), MaxLength(50)] public string? Station { get; set; }
        [Column("original_ar_number"), MaxLength(100)] public string? OriginalArNumber { get; set; }
        [Column("ar_number"), MaxLength(50)] public string? ArNumber { get; set; }
        [Column("original_cas_number"), MaxLength(150)] public string? OriginalCasNumber { get; set; }
        [Column("cas_number"), MaxLength(50)] public string? CasNumber { get; set; }
        [Column("original_date"), MaxLength(100)] public string? OriginalDate { get; set; }
        [Column("crash_date")] public DateOnly? CrashDate { get; set; }
        [Column("original_day"), MaxLength(30)] public string? OriginalDay { get; set; }
        [Column("calculated_day"), MaxLength(15)] public string? CalculatedDay { get; set; }
        [Column("original_time"), MaxLength(100)] public string? OriginalTime { get; set; }
        [Column("crash_time")] public TimeOnly? CrashTime { get; set; }
        [Column("original_route"), MaxLength(100)] public string? OriginalRoute { get; set; }
        [Column("route"), MaxLength(20)] public string? Route { get; set; }
        [Column("original_location"), MaxLength(500)] public string? OriginalLocation { get; set; }
        [Column("location"), MaxLength(150)] public string? Location { get; set; }
        [Column("original_crash_type"), MaxLength(150)] public string? OriginalCrashType { get; set; }
        [Column("crash_type"), MaxLength(30)] public string? CrashType { get; set; }
        [Column("original_vehicles"), MaxLength(500)] public string? OriginalVehicles { get; set; }
        [Column("vehicles_string"), MaxLength(100)] public string? VehiclesString { get; set; }
        [Column("vehicle_count")] public byte? VehicleCount { get; set; }

        [Column("fatal_drivers")] public byte? FatalDrivers { get; set; }
        [Column("fatal_passengers")] public byte? FatalPassengers { get; set; }
        [Column("fatal_pedestrians")] public byte? FatalPedestrians { get; set; }
        [Column("fatal_cyclists")] public byte? FatalCyclists { get; set; }
        [Column("fatal_male")] public byte? FatalMale { get; set; }
        [Column("fatal_female")] public byte? FatalFemale { get; set; }
        [Column("serious_drivers")] public byte? SeriousDrivers { get; set; }
        [Column("serious_passengers")] public byte? SeriousPassengers { get; set; }
        [Column("serious_pedestrians")] public byte? SeriousPedestrians { get; set; }
        [Column("serious_cyclists")] public byte? SeriousCyclists { get; set; }
        [Column("slight_drivers")] public byte? SlightDrivers { get; set; }
        [Column("slight_passengers")] public byte? SlightPassengers { get; set; }
        [Column("slight_pedestrians")] public byte? SlightPedestrians { get; set; }
        [Column("slight_cyclists")] public byte? SlightCyclists { get; set; }
        [Column("validation_status"), MaxLength(20)] public string ValidationStatus { get; set; } = ImportValidationStatuses.Pending;
        [Column("duplicate_status"), MaxLength(30)] public string DuplicateStatus { get; set; } = ImportDuplicateStatuses.NotChecked;
        [Column("review_status"), MaxLength(30)] public string ReviewStatus { get; set; } = ImportReviewStatuses.Pending;

        [Column("import_status"), MaxLength(20)] public string ImportStatus { get; set; } = ImportRecordStatuses.NotImported;

        [Column("review_notes"), MaxLength(1000)] public string? ReviewNotes { get; set; }
        [Column("reviewed_by_user_id"), MaxLength(450)] public string? ReviewedByUserId { get; set; }
        [Column("reviewed_at")] public DateTime? ReviewedAt { get; set; }
        [Column("production_summary_id")] public int? ProductionSummaryId { get; set; }
        [Column("imported_at")] public DateTime? ImportedAt { get; set; }

        [Column("row_version")]
        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public ImportBatch ImportBatch { get; set; } = null!;
        public CrashSummary? ProductionSummary { get; set; }
        public ICollection<ImportDataQualityIssue> Issues { get; set; } = new List<ImportDataQualityIssue>();


    }
}
