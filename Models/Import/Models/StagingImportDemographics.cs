using CrashReport.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CrashReport.Models.Import.Models
{

    [Table("staging_import_demographics")]
    public class StagingImportDemographics
    {
        [Key, Column("staging_demographics_id")] public int StagingDemographicsId { get; set; }
        [Column("import_batch_id")] public int ImportBatchId { get; set; }
        [Column("worksheet_name"), MaxLength(128)] public string WorksheetName { get; set; } = string.Empty;
        [Column("raw_section_json")] public string RawSectionJson { get; set; } = "{}";
        [Column("period_from")] public DateOnly PeriodFrom { get; set; }
        [Column("period_to")] public DateOnly PeriodTo { get; set; }
        [Column("province_code"), MaxLength(5)] public string? ProvinceCode { get; set; }
        [Column("region"), MaxLength(50)] public string? Region { get; set; }
        [Column("age_0_7")] public int? Age0to7 { get; set; }
        [Column("age_8_12")] public int? Age8to12 { get; set; }
        [Column("age_13_18")] public int? Age13to18 { get; set; }
        [Column("age_19_35")] public int? Age19to35 { get; set; }
        [Column("age_36_plus")] public int? Age36Plus { get; set; }
        [Column("driver_male")] public int? DriverMale { get; set; }
        [Column("driver_female")] public int? DriverFemale { get; set; }
        [Column("passenger_male")] public int? PassengerMale { get; set; }
        [Column("passenger_female")] public int? PassengerFemale { get; set; }
        [Column("pedestrian_male")] public int? PedestrianMale { get; set; }
        [Column("pedestrian_female")] public int? PedestrianFemale { get; set; }
        [Column("cyclist_male")] public int? CyclistMale { get; set; }
        [Column("cyclist_female")] public int? CyclistFemale { get; set; }
        [Column("race_black")] public int? RaceBlack { get; set; }
        [Column("race_coloured")] public int? RaceColoured { get; set; }
        [Column("race_white")] public int? RaceWhite { get; set; }
        [Column("race_indian")] public int? RaceIndian { get; set; }
        [Column("race_other")] public int? RaceOther { get; set; }
        [Column("validation_status"), MaxLength(20)] public string ValidationStatus { get; set; } = ImportValidationStatuses.Pending;
        [Column("production_demographics_id")] public int? ProductionDemographicsId { get; set; }
        public ImportBatch ImportBatch { get; set; } = null!;
        public CrashDemographicRecord? ProductionDemographics { get; set; }

    }
}
