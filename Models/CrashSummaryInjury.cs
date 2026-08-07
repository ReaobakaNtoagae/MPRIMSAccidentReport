using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CrashReport.Models;

[Table("crash_summary_injuries")]
public class CrashSummaryInjury
{
    [Key]
    [Column("injury_id")]
    public int InjuryId { get; set; }

    [Column("summary_id")]
    public int SummaryId { get; set; }

    // Null for pedestrians/cyclists -- there is no vehicle to link to.
    [Column("vehicle_id")]
    public int? VehicleId { get; set; }

    [Column("severity")]
    [MaxLength(10)]
    public string Severity { get; set; } = string.Empty; // "Fatal" | "Serious" | "Slight"

    [Column("role")]
    [MaxLength(20)]
    public string Role { get; set; } = string.Empty; // "Driver" | "Passenger" | "Pedestrian" | "Cyclist"

    // All nullable -- a row with just severity+role still counts as a
    // casualty. A mass-casualty crash still just works even if nobody
    // has time to fill in demographics for every person.
    [Column("age")]
    public byte? Age { get; set; }

    [Column("gender")]
    [MaxLength(1)]
    public string? Gender { get; set; }

    [Column("race")]
    [MaxLength(1)]
    public string? Race { get; set; }

  
    public CrashSummary? CrashSummary { get; set; }
    public CrashSummaryVehicle? Vehicle { get; set; }
}