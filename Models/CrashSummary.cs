using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CrashReport.Models;


[Table("crash_summaries")]
[Index(nameof(CrNo), nameof(SourceFile), IsUnique = true)]
public class CrashSummary
{
    [Key]
    [Column("summary_id")]
    public int SummaryId { get; set; }

    [Column("cr_no")]
    [MaxLength(50)]
    public string CrNo { get; set; } = string.Empty;

    [Column("station")]
    [MaxLength(50)]
    public string Station { get; set; } = string.Empty;

    [Column("cas_no")]
    [MaxLength(50)]
    public string? CasNo { get; set; }

    [Column("crash_date")]
    public DateOnly CrashDate { get; set; }

    [Column("crash_time")]
    public TimeOnly? CrashTime { get; set; }

    [Column("route")]
    [MaxLength(20)]
    public string? Route { get; set; }

    [Column("location")]
    [MaxLength(150)]
    public string? Location { get; set; }

    [Column("crash_type")]
    [MaxLength(30)]
    public string? CrashType { get; set; }

    [Column("vehicles_string")]
    [MaxLength(100)]
    public string? VehiclesString { get; set; }

    [Column("vehicle_count")]
    public byte VehicleCount { get; set; }

    [Column("fatal_drivers")] public byte FatalDrivers { get; set; }
    [Column("fatal_passengers")] public byte FatalPassengers { get; set; }
    [Column("fatal_pedestrians")] public byte FatalPedestrians { get; set; }
    [Column("fatal_cyclists")] public byte FatalCyclists { get; set; }
    [Column("fatal_male")] public byte FatalMale { get; set; }
    [Column("fatal_female")] public byte FatalFemale { get; set; }

    [Column("serious_drivers")] public byte SeriousDrivers { get; set; }
    [Column("serious_passengers")] public byte SeriousPassengers { get; set; }
    [Column("serious_pedestrians")] public byte SeriousPedestrians { get; set; }
    [Column("serious_cyclists")] public byte SeriousCyclists { get; set; }

    [Column("slight_drivers")] public byte SlightDrivers { get; set; }
    [Column("slight_passengers")] public byte SlightPassengers { get; set; }
    [Column("slight_pedestrians")] public byte SlightPedestrians { get; set; }
    [Column("slight_cyclists")] public byte SlightCyclists { get; set; }

    [Column("source_file")]
    [MaxLength(255)]
    public string? SourceFile { get; set; }

    [Column("imported_at")]
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    // ── Convenience totals (not mapped — computed) ──
    [NotMapped] public int Fatalities => FatalDrivers + FatalPassengers + FatalPedestrians + FatalCyclists;
    [NotMapped] public int Serious => SeriousDrivers + SeriousPassengers + SeriousPedestrians + SeriousCyclists;
    [NotMapped] public int Slight => SlightDrivers + SlightPassengers + SlightPedestrians + SlightCyclists;
}

