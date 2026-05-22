using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CrashReport.Models;

[Table("lkp_saps_stations")]
public class SapsStation
{
    [Key]
    [Column("station_id")]
    public int StationId { get; set; }

    [Required, MaxLength(100)]
    [Column("station_name")]
    public string StationName { get; set; } = string.Empty;

    [MaxLength(5)]
    [Column("province_code")]
    public string? ProvinceCode { get; set; }

    [MaxLength(100)]
    [Column("district")]
    public string? District { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

[Table("lkp_locations")]
public class LookupLocation
{
    [Key]
    [Column("location_id")]
    public int LocationId { get; set; }

    [Required, MaxLength(200)]
    [Column("location_name")]
    public string LocationName { get; set; } = string.Empty;

    [Column("station_id")]
    public int? StationId { get; set; }

    [MaxLength(5)]
    [Column("province_code")]
    public string? ProvinceCode { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [ForeignKey(nameof(StationId))]
    public SapsStation? Station { get; set; }
}

[Table("lkp_routes")]
public class LookupRoute
{
    [Key]
    [Column("route_id")]
    public int RouteId { get; set; }

    [Required, MaxLength(20)]
    [Column("route_code")]
    public string RouteCode { get; set; } = string.Empty;

    [MaxLength(200)]
    [Column("description")]
    public string? Description { get; set; }

    [MaxLength(5)]
    [Column("province_code")]
    public string? ProvinceCode { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

[Table("lkp_crash_types")]
public class LookupCrashType
{
    [Key]
    [Column("crash_type_id")]
    public int CrashTypeId { get; set; }

    [Required, MaxLength(50)]
    [Column("crash_type_code")]
    public string CrashTypeCode { get; set; } = string.Empty;

    [MaxLength(200)]
    [Column("description")]
    public string? Description { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

[Table("lkp_vehicle_types")]
public class LookupVehicleType
{
    [Key]
    [Column("vehicle_type_id")]
    public int VehicleTypeId { get; set; }

    [Required, MaxLength(20)]
    [Column("vehicle_type_code")]
    public string VehicleTypeCode { get; set; } = string.Empty;

    [MaxLength(100)]
    [Column("description")]
    public string? Description { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}