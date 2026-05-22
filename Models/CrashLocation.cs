using System;
using System.Collections.Generic;

namespace CrashReport.Models;

public partial class CrashLocation
{
    public int LocationId { get; set; }

    public int CrashId { get; set; }

    public bool? BuiltUpArea { get; set; }

    public string? AreaType { get; set; }

    public string? StreetRoadName { get; set; }

    public decimal? GpsXCoordinate { get; set; }

    public decimal? GpsYCoordinate { get; set; }

    public string? IntersectionStreet { get; set; }

    public string? IntersectionRoadNo { get; set; }

    public string? BetweenFrom { get; set; }

    public string? BetweenTo { get; set; }

    public string? Suburb { get; set; }

    public string? CityTown { get; set; }

    public decimal? DistanceKm { get; set; }

    public string? CompassDirection { get; set; }

    public string? FromPoint { get; set; }

    public string? KmMarkerInfo { get; set; }

    public string? NextCityTown { get; set; }

    public string? RoadFunctionalClassification { get; set; }

    public string? JunctionType { get; set; }

    public string? RoadLayout { get; set; }

    public string? RoadSurfaceType { get; set; }

    public string? RoadSurfaceQuality { get; set; }

    public string? RoadSurfaceCondition { get; set; }

    public virtual Crash Crash { get; set; } = null!;
}
