using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CrashReport.Models;

[Table("crash_summary_vehicles")]
public class CrashSummaryVehicle
{
    [Key]
    [Column("vehicle_id")]
    public int VehicleId { get; set; }

    [Column("summary_id")]
    public int SummaryId { get; set; }

    [Column("vehicle_number")]
    public byte VehicleNumber { get; set; }

    [Column("vehicle_type_code")]
    [MaxLength(20)]
    public string VehicleTypeCode { get; set; } = string.Empty;

    [Column("vehicle_type_name")]
    [MaxLength(60)]
    public string VehicleTypeName { get; set; } = string.Empty;

    [Column("registration")]
    [MaxLength(20)]
    public string? Registration { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public CrashSummary? CrashSummary { get; set; }
    public List<CrashSummaryInjury> Injuries { get; set; } = new();
}