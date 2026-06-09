using CrashReport.Models;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ── Core DbSets ──────────────────────────────────────────
    public DbSet<Person> Persons { get; set; }
    public DbSet<DriversLicence> DriversLicences { get; set; }
    public DbSet<Vehicle> Vehicles { get; set; }
    public DbSet<Crash> Crashes { get; set; }
    public DbSet<CrashLocation> CrashLocations { get; set; }
    public DbSet<CrashCondition> CrashConditions { get; set; }
    public DbSet<CrashWeather> CrashWeathers { get; set; }
    public DbSet<CrashVehicle> CrashVehicles { get; set; }
    public DbSet<VehicleDamage> VehicleDamages { get; set; }
    public DbSet<CrashPerson> CrashPeople { get; set; }
    public DbSet<PedestrianBicyclistDetail> PedestrianBicyclistDetails { get; set; }
    public DbSet<ContributoryFactor> ContributoryFactors { get; set; }
    public DbSet<DangerousGood> DangerousGoods { get; set; }
    public DbSet<Witness> Witnesses { get; set; }
    public DbSet<OfficialUse> OfficialUses { get; set; }
    public DbSet<CrashSketch> CrashSketches { get; set; }
    public DbSet<CrashDemographicRecord> CrashDemographics { get; set; }

    // ── Lookup DbSets ────────────────────────────────────────
    public DbSet<SapsStation> SapsStations { get; set; }
    public DbSet<LookupLocation> LookupLocations { get; set; }
    public DbSet<LookupRoute> LookupRoutes { get; set; }
    public DbSet<LookupCrashType> LookupCrashTypes { get; set; }
    public DbSet<LookupVehicleType> LookupVehicleTypes { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        
        modelBuilder.Entity<Person>(e => {
            e.ToTable("persons");
            e.HasKey(p => p.PersonId);
            e.Property(p => p.PersonId).HasColumnName("person_id");
            e.Property(p => p.IdType).HasMaxLength(20).HasColumnName("id_type");
            e.Property(p => p.IdNumber).HasMaxLength(50).HasColumnName("id_number");
            e.Property(p => p.Age).HasColumnName("age");
            e.Property(p => p.Surname).HasMaxLength(100).HasColumnName("surname");
            e.Property(p => p.FullNames).HasMaxLength(200).HasColumnName("full_names");
            e.Property(p => p.CountryOfOrigin).HasMaxLength(100).HasColumnName("country_of_origin");
            e.Property(p => p.Nationality).HasMaxLength(100).HasColumnName("nationality");
            e.Property(p => p.PopulationGroup).HasMaxLength(20).HasColumnName("population_group");
            e.Property(p => p.Gender).HasMaxLength(10).HasColumnName("gender");
            e.Property(p => p.HomeAddress).HasMaxLength(300).HasColumnName("home_address");
            e.Property(p => p.CellPhone).HasMaxLength(20).HasColumnName("cell_phone");
            e.Property(p => p.OtherPhone).HasMaxLength(20).HasColumnName("other_phone");
            e.Property(p => p.WorkContactAddress).HasMaxLength(300).HasColumnName("work_contact_address");
            e.Property(p => p.CreatedAt).HasDefaultValueSql("(getdate())").HasColumnName("created_at");
            e.HasIndex(p => p.IdNumber).HasDatabaseName("idx_persons_id_number");
            e.HasIndex(p => p.Surname).HasDatabaseName("idx_persons_surname");
        });

        
        modelBuilder.Entity<DriversLicence>(e => {
            e.ToTable("drivers_licences");
            e.HasKey(d => d.LicenceId);
            e.Property(d => d.LicenceId).HasColumnName("licence_id");
            e.Property(d => d.PersonId).HasColumnName("person_id");
            e.Property(d => d.LicenceType).HasMaxLength(5).HasColumnName("licence_type");
            e.Property(d => d.LicenceNumber).HasMaxLength(50).HasColumnName("licence_number");
            e.Property(d => d.LicenceCode).HasMaxLength(10).HasColumnName("licence_code");
            e.Property(d => d.DateOfIssue).HasColumnName("date_of_issue");
            e.Property(d => d.PrdpCode).HasMaxLength(20).HasColumnName("prdp_code");
            e.HasOne(d => d.Person).WithMany(p => p.DriversLicences)
                .HasForeignKey(d => d.PersonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_dl_person");
        });

       
        modelBuilder.Entity<Vehicle>(e => {
            e.ToTable("vehicles");
            e.HasKey(v => v.VehicleId);
            e.Property(v => v.VehicleId).HasColumnName("vehicle_id");
            e.Property(v => v.CountryOfRegistration).HasMaxLength(10).HasDefaultValue("RSA").HasColumnName("country_of_registration");
            e.Property(v => v.LicenceDiscNumber).HasMaxLength(50).HasColumnName("licence_disc_number");
            e.Property(v => v.Colour).HasMaxLength(50).HasColumnName("colour");
            e.Property(v => v.Make).HasMaxLength(100).HasColumnName("make");
            e.Property(v => v.Model).HasMaxLength(100).HasColumnName("model");
            e.Property(v => v.VinNumber).HasMaxLength(50).HasColumnName("vin_number");
            e.Property(v => v.TrailerLicenceNumber).HasMaxLength(50).HasColumnName("trailer_licence_number");
            e.Property(v => v.VehicleCategory).HasMaxLength(20).HasColumnName("vehicle_category");
            e.Property(v => v.VehicleTypeCode).HasMaxLength(10).HasColumnName("vehicle_type_code");
            e.Property(v => v.SpecialFunction).HasMaxLength(20).HasColumnName("special_function");
            e.Property(v => v.PrivateOrBusiness).HasMaxLength(10).HasColumnName("private_or_business");
            e.Property(v => v.LicenceTypeFitting).HasMaxLength(20).HasColumnName("licence_type_fitting");
            e.Property(v => v.CreatedAt).HasDefaultValueSql("(getdate())").HasColumnName("created_at");
            e.HasIndex(v => v.VinNumber).HasDatabaseName("idx_vehicles_vin");
            e.HasIndex(v => v.LicenceDiscNumber).HasDatabaseName("idx_vehicles_disc");
        });

        
        modelBuilder.Entity<Crash>(e => {
            e.ToTable("crashes");
            e.HasKey(c => c.CrashId);
            e.Property(c => c.CrashId).HasColumnName("crash_id");
            e.Property(c => c.CasNo).HasMaxLength(30).HasColumnName("cas_no");
            e.Property(c => c.CrNo).HasMaxLength(30).HasColumnName("cr_no");
            e.Property(c => c.IncidentReportNo).HasMaxLength(30).HasColumnName("incident_report_no");
            e.Property(c => c.CapturingNumber).HasMaxLength(30).HasColumnName("capturing_number");
            e.Property(c => c.CrashDate).HasColumnName("crash_date");
            e.Property(c => c.CrashTime).HasColumnName("crash_time");
            e.Property(c => c.NoOfAppendices).HasDefaultValue((byte)0).HasColumnName("no_of_appendices");
            e.Property(c => c.NoOfVehiclesInvolved).HasDefaultValue((byte)1).HasColumnName("no_of_vehicles_involved");
            e.Property(c => c.ProvinceCode).HasMaxLength(5).HasColumnName("province_code");
            e.Property(c => c.SpeedLimitKmh).HasColumnName("speed_limit_kmh");
            e.Property(c => c.RoadNumber).HasMaxLength(30).HasColumnName("road_number");
            e.Property(c => c.KmMarker).HasMaxLength(30).HasColumnName("km_marker");
            e.Property(c => c.BriefDescription).HasColumnName("brief_description");
            e.Property(c => c.CreatedAt).HasDefaultValueSql("(getdate())").HasColumnName("created_at");
            e.HasIndex(c => c.CrashDate).HasDatabaseName("idx_crashes_date");
            e.HasIndex(c => c.ProvinceCode).HasDatabaseName("idx_crashes_province");
            e.HasIndex(c => c.CasNo).HasDatabaseName("idx_crashes_cas_no");
        });

        
        modelBuilder.Entity<CrashLocation>(e => {
            e.ToTable("crash_locations");
            e.HasKey(l => l.LocationId);
            e.Property(l => l.LocationId).HasColumnName("location_id");
            e.Property(l => l.CrashId).HasColumnName("crash_id");
            e.Property(l => l.BuiltUpArea).HasColumnName("built_up_area");
            e.Property(l => l.AreaType).HasMaxLength(20).HasColumnName("area_type");
            e.Property(l => l.StreetRoadName).HasMaxLength(200).HasColumnName("street_road_name").IsRequired(false);
            e.Property(l => l.GpsXCoordinate).HasColumnType("decimal(10,7)").HasColumnName("gps_x_coordinate");
            e.Property(l => l.GpsYCoordinate).HasColumnType("decimal(10,7)").HasColumnName("gps_y_coordinate");
            e.Property(l => l.IntersectionStreet).HasMaxLength(200).HasColumnName("intersection_street");
            e.Property(l => l.IntersectionRoadNo).HasMaxLength(50).HasColumnName("intersection_road_no");
            e.Property(l => l.BetweenFrom).HasMaxLength(200).HasColumnName("between_from");
            e.Property(l => l.BetweenTo).HasMaxLength(200).HasColumnName("between_to");
            e.Property(l => l.Suburb).HasMaxLength(100).HasColumnName("suburb");
            e.Property(l => l.CityTown).HasMaxLength(100).HasColumnName("city_town");
            e.Property(l => l.DistanceKm).HasColumnType("decimal(6,2)").HasColumnName("distance_km");
            e.Property(l => l.CompassDirection).HasMaxLength(5).HasColumnName("compass_direction");
            e.Property(l => l.FromPoint).HasMaxLength(300).HasColumnName("from_point");
            e.Property(l => l.KmMarkerInfo).HasMaxLength(100).HasColumnName("km_marker_info");
            e.Property(l => l.NextCityTown).HasMaxLength(100).HasColumnName("next_city_town");
            e.Property(l => l.RoadFunctionalClassification).HasMaxLength(50).HasColumnName("road_functional_classification");
            e.Property(l => l.JunctionType).HasMaxLength(50).HasColumnName("junction_type");
            e.Property(l => l.RoadLayout).HasMaxLength(20).HasColumnName("road_layout");
            e.Property(l => l.RoadSurfaceType).HasMaxLength(20).HasColumnName("road_surface_type");
            e.Property(l => l.RoadSurfaceQuality).HasMaxLength(20).HasColumnName("road_surface_quality");
            e.Property(l => l.RoadSurfaceCondition).HasMaxLength(20).HasColumnName("road_surface_condition");
            e.HasOne(l => l.Crash).WithMany(c => c.CrashLocations)
                .HasForeignKey(l => l.CrashId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_cl_crash");
        });

        
        modelBuilder.Entity<CrashCondition>(e => {
            e.ToTable("crash_conditions");
            e.HasKey(c => c.ConditionId);
            e.Property(c => c.ConditionId).HasColumnName("condition_id");
            e.Property(c => c.CrashId).HasColumnName("crash_id");
            e.Property(c => c.LightCondition).HasMaxLength(30).HasColumnName("light_condition");
            e.Property(c => c.ObstructionType).HasMaxLength(30).HasColumnName("obstruction_type");
            e.Property(c => c.TrafficControlType).HasMaxLength(50).HasColumnName("traffic_control_type");
            e.Property(c => c.RoadSignsCondition).HasMaxLength(20).HasColumnName("road_signs_condition");
            e.Property(c => c.RoadMarkingVisibility).HasMaxLength(20).HasColumnName("road_marking_visibility");
            e.Property(c => c.OvertakingControl).HasMaxLength(20).HasColumnName("overtaking_control");
            e.Property(c => c.RoadSegmentGrade).HasMaxLength(20).HasColumnName("road_segment_grade");
            e.Property(c => c.CrashType).HasMaxLength(50).HasColumnName("crash_type");
            e.Property(c => c.HitAndRun).HasColumnName("hit_and_run");
            e.Property(c => c.TyreBurstObserved).HasMaxLength(10).HasColumnName("tyre_burst_observed");
            e.Property(c => c.VehicleLightsCondition).HasMaxLength(20).HasColumnName("vehicle_lights_condition");
            e.Property(c => c.OtherObservations).HasColumnName("other_observations");
            e.HasOne(c => c.Crash).WithMany(cr => cr.CrashConditions)
                .HasForeignKey(c => c.CrashId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_cc_crash");
        });

        
        modelBuilder.Entity<CrashWeather>(e => {
            e.ToTable("crash_weather");
            e.HasKey(cw => new { cw.CrashId, cw.WeatherCondition });
            e.Property(cw => cw.CrashId).HasColumnName("crash_id");
            e.Property(cw => cw.WeatherCondition).HasMaxLength(30).HasColumnName("weather_condition");
            e.HasOne(cw => cw.Crash).WithMany(c => c.CrashWeathers)
                .HasForeignKey(cw => cw.CrashId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_cw_crash");
        });

       
        modelBuilder.Entity<CrashVehicle>(e => {
            e.ToTable("crash_vehicles");
            e.HasKey(cv => cv.CrashVehicleId);
            e.Property(cv => cv.CrashVehicleId).HasColumnName("crash_vehicle_id");
            e.Property(cv => cv.CrashId).HasColumnName("crash_id");
            e.Property(cv => cv.VehicleId).HasColumnName("vehicle_id");
            e.Property(cv => cv.DriverPersonId).HasColumnName("driver_person_id");
            e.Property(cv => cv.VehicleType).HasColumnName("vehicle_type").HasMaxLength(50);
            e.Property(cv => cv.VehicleReference).HasMaxLength(2).IsFixedLength().HasColumnName("vehicle_reference");
            e.Property(cv => cv.SeatbeltUsed).HasMaxLength(30).HasColumnName("seatbelt_used");
            e.Property(cv => cv.AlcoholSuspected).HasMaxLength(10).HasColumnName("alcohol_suspected");
            e.Property(cv => cv.AlcoholTestResult).HasMaxLength(20).HasColumnName("alcohol_test_result");
            e.Property(cv => cv.DrugSuspected).HasMaxLength(10).HasColumnName("drug_suspected");
            e.Property(cv => cv.DrugTestResult).HasMaxLength(20).HasColumnName("drug_test_result");
            e.Property(cv => cv.VehicleManoeuvre).HasMaxLength(50).HasColumnName("vehicle_manoeuvre");
            e.Property(cv => cv.PositionBeforeCrash).HasMaxLength(30).HasColumnName("position_before_crash");
            e.Property(cv => cv.PassengersForReward).HasMaxLength(10).HasColumnName("passengers_for_reward");
            e.Property(cv => cv.BreakdownCompany).HasMaxLength(200).HasColumnName("breakdown_company");
            e.HasOne(cv => cv.Crash).WithMany(c => c.CrashVehicles)
                .HasForeignKey(cv => cv.CrashId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_cv_crash");
            e.HasOne(cv => cv.Vehicle).WithMany(v => v.CrashVehicles)
                .HasForeignKey(cv => cv.VehicleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_cv_vehicle");
            e.HasOne(cv => cv.DriverPerson).WithMany(p => p.CrashVehicles)
                .HasForeignKey(cv => cv.DriverPersonId)
                .HasConstraintName("fk_cv_driver");
            e.HasIndex(cv => cv.CrashId).HasDatabaseName("idx_cv_crash");
            e.HasIndex(cv => cv.VehicleId).HasDatabaseName("idx_cv_vehicle");
            e.HasIndex(cv => cv.DriverPersonId).HasDatabaseName("idx_cv_driver");
        });


       
        modelBuilder.Entity<VehicleDamage>(e => {
            e.ToTable("vehicle_damage");
            e.HasKey(vd => vd.DamageId);
            e.Property(vd => vd.DamageId).HasColumnName("damage_id");
            e.Property(vd => vd.CrashVehicleId).HasColumnName("crash_vehicle_id");
            e.Property(vd => vd.DamagePoint).HasMaxLength(30).HasColumnName("damage_point");
            e.HasOne(vd => vd.CrashVehicle).WithMany(cv => cv.VehicleDamages)
                .HasForeignKey(vd => vd.CrashVehicleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_vd_crash_vehicle");
        });

        
        modelBuilder.Entity<CrashPerson>(e => {
            e.ToTable("crash_persons");
            e.HasKey(cp => cp.CrashPersonId);
            e.Property(cp => cp.CrashPersonId).HasColumnName("crash_person_id");
            e.Property(cp => cp.CrashId).HasColumnName("crash_id");
            e.Property(cp => cp.PersonId).HasColumnName("person_id");
            e.Property(cp => cp.CrashVehicleId).HasColumnName("crash_vehicle_id");
            e.Property(cp => cp.Role).HasMaxLength(20).HasColumnName("role");
            e.Property(cp => cp.VehicleReference).HasMaxLength(2).IsFixedLength().HasColumnName("vehicle_reference");
            e.Property(cp => cp.PersonReference).HasMaxLength(5).HasColumnName("person_reference");
            e.Property(cp => cp.PassengerNumber).HasColumnName("passenger_number");
            e.Property(cp => cp.SeatingPosition).HasMaxLength(20).HasColumnName("seating_position");
            e.Property(cp => cp.SeverityOfInjury).HasMaxLength(20).HasColumnName("severity_of_injury");
            e.Property(cp => cp.SeatbeltHelmetUsed).HasMaxLength(30).HasColumnName("seatbelt_helmet_used");
            e.Property(cp => cp.ChildRestraintUsed).HasMaxLength(20).HasColumnName("child_restraint_used");
            e.Property(cp => cp.LiquorDrugSuspected).HasMaxLength(10).HasColumnName("liquor_drug_suspected");
            e.Property(cp => cp.LiquorDrugTestDone).HasMaxLength(10).HasColumnName("liquor_drug_test_done");
            e.Property(cp => cp.AmbulanceServiceRef).HasMaxLength(100).HasColumnName("ambulance_service_ref");
            e.Property(cp => cp.Hospital).HasMaxLength(200).HasColumnName("hospital");
            e.HasOne(cp => cp.Crash).WithMany(c => c.CrashPeople)
                .HasForeignKey(cp => cp.CrashId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_cp_crash");
            e.HasOne(cp => cp.Person).WithMany(p => p.CrashPeople)
                .HasForeignKey(cp => cp.PersonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_cp_person");
            e.HasOne(cp => cp.CrashVehicle).WithMany(cv => cv.CrashPeople)
                .HasForeignKey(cp => cp.CrashVehicleId)
                .HasConstraintName("fk_cp_crash_vehicle");
            e.HasIndex(cp => cp.CrashId).HasDatabaseName("idx_cp_crash");
            e.HasIndex(cp => cp.PersonId).HasDatabaseName("idx_cp_person");
            e.HasIndex(cp => cp.SeverityOfInjury).HasDatabaseName("idx_cp_injury");
        });

       
        modelBuilder.Entity<PedestrianBicyclistDetail>(e => {
            e.ToTable("pedestrian_bicyclist_details");
            e.HasKey(pd => pd.DetailId);
            e.Property(pd => pd.DetailId).HasColumnName("detail_id");
            e.Property(pd => pd.CrashPersonId).HasColumnName("crash_person_id");
            e.Property(pd => pd.PositionOnRoad).HasMaxLength(30).HasColumnName("position_on_road");
            e.Property(pd => pd.LocationReCrossing).HasMaxLength(40).HasColumnName("location_re_crossing");
            e.Property(pd => pd.Manoeuvre).HasMaxLength(30).HasColumnName("manoeuvre");
            e.Property(pd => pd.PedestrianAction).HasMaxLength(20).HasColumnName("pedestrian_action");
            e.Property(pd => pd.ClothingColour).HasMaxLength(20).HasColumnName("clothing_colour");
            e.HasOne(pd => pd.CrashPerson).WithMany(cp => cp.PedestrianBicyclistDetails)
                .HasForeignKey(pd => pd.CrashPersonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_pd_cp");
        });

        
        modelBuilder.Entity<ContributoryFactor>(e => {
            e.ToTable("contributory_factors");
            e.HasKey(cf => cf.FactorId);
            e.Property(cf => cf.FactorId).HasColumnName("factor_id");
            e.Property(cf => cf.CrashId).HasColumnName("crash_id");
            e.Property(cf => cf.FactorCategory).HasMaxLength(30).HasColumnName("factor_category");
            e.Property(cf => cf.FactorDescription).HasMaxLength(200).HasColumnName("factor_description");
            e.Property(cf => cf.IsMajorFactor).HasColumnName("is_major_factor");
            e.HasOne(cf => cf.Crash).WithMany(c => c.ContributoryFactors)
                .HasForeignKey(cf => cf.CrashId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_cf_crash");
            e.HasIndex(cf => cf.CrashId).HasDatabaseName("idx_cf_crash");
            e.HasIndex(cf => cf.FactorCategory).HasDatabaseName("idx_cf_category");
        });

       
        modelBuilder.Entity<DangerousGood>(e => {
            e.ToTable("dangerous_goods");
            e.HasKey(dg => dg.DgId);
            e.Property(dg => dg.DgId).HasColumnName("dg_id");
            e.Property(dg => dg.CrashId).HasColumnName("crash_id");
            e.Property(dg => dg.VehicleReference).HasMaxLength(2).IsFixedLength().HasColumnName("vehicle_reference");
            e.Property(dg => dg.GoodsCarried).HasMaxLength(10).HasColumnName("goods_carried");
            e.Property(dg => dg.SpillageObserved).HasMaxLength(10).HasColumnName("spillage_observed");
            e.Property(dg => dg.VapourGasEmission).HasMaxLength(10).HasColumnName("vapour_gas_emission");
            e.Property(dg => dg.PlacardDisplayed).HasMaxLength(10).HasColumnName("placard_displayed");
            e.Property(dg => dg.UnNumber).HasMaxLength(20).HasColumnName("un_number");
            e.Property(dg => dg.CompanyName).HasMaxLength(200).HasColumnName("company_name");
            e.Property(dg => dg.EmergencyServicesActivated).HasMaxLength(200).HasColumnName("emergency_services_activated");
            e.HasOne(dg => dg.Crash).WithMany(c => c.DangerousGoods)
                .HasForeignKey(dg => dg.CrashId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_dg_crash");
        });

        
        modelBuilder.Entity<Witness>(e => {
            e.ToTable("witnesses");
            e.HasKey(w => w.WitnessId);
            e.Property(w => w.WitnessId).HasColumnName("witness_id");
            e.Property(w => w.CrashId).HasColumnName("crash_id");
            e.Property(w => w.SurnameInitials).HasMaxLength(100).HasColumnName("surname_initials");
            e.Property(w => w.IdType).HasMaxLength(20).HasColumnName("id_type");
            e.Property(w => w.IdNumber).HasMaxLength(50).HasColumnName("id_number");
            e.Property(w => w.WorkContactAddress).HasMaxLength(300).HasColumnName("work_contact_address");
            e.Property(w => w.CellPhone).HasMaxLength(20).HasColumnName("cell_phone");
            e.Property(w => w.OtherPhone).HasMaxLength(20).HasColumnName("other_phone");
            e.HasOne(w => w.Crash).WithMany(c => c.Witnesses)
                .HasForeignKey(w => w.CrashId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_w_crash");
        });

        
        modelBuilder.Entity<OfficialUse>(e => {
            e.ToTable("official_use");
            e.HasKey(o => o.OfficialId);
            e.Property(o => o.OfficialId).HasColumnName("official_id");
            e.Property(o => o.CrashId).HasColumnName("crash_id");
            e.Property(o => o.OccurrenceBookNo).HasMaxLength(50).HasColumnName("occurrence_book_no");
            e.Property(o => o.AccidentRegisterNo).HasMaxLength(50).HasColumnName("accident_register_no");
            e.Property(o => o.SapsCasNo).HasMaxLength(50).HasColumnName("saps_cas_no");
            e.Property(o => o.OfficeWhereOccurred).HasMaxLength(200).HasColumnName("office_where_occurred");
            e.Property(o => o.DepartmentNameOccurred).HasMaxLength(100).HasColumnName("department_name_occurred");
            e.Property(o => o.DateStamp).HasColumnName("date_stamp");
            e.Property(o => o.InspectedByInitials).HasMaxLength(20).HasColumnName("inspected_by_initials");
            e.Property(o => o.InspectedByRank).HasMaxLength(50).HasColumnName("inspected_by_rank");
            e.Property(o => o.InspectedBySurname).HasMaxLength(100).HasColumnName("inspected_by_surname");
            e.Property(o => o.InspectedByServiceNumber).HasMaxLength(50).HasColumnName("inspected_by_service_number");
            e.Property(o => o.InspectedBySignature).HasMaxLength(500).HasColumnName("inspected_by_signature");
            e.Property(o => o.OfficeWhereReported).HasMaxLength(200).HasColumnName("office_where_reported");
            e.Property(o => o.DepartmentNameReported).HasMaxLength(100).HasColumnName("department_name_reported");
            e.Property(o => o.CompletedBy).HasMaxLength(100).HasColumnName("completed_by");
            e.Property(o => o.CompletedInitials).HasMaxLength(20).HasColumnName("completed_initials");
            e.Property(o => o.CompletedRank).HasMaxLength(50).HasColumnName("completed_rank");
            e.Property(o => o.CompletedSurname).HasMaxLength(100).HasColumnName("completed_surname");
            e.Property(o => o.CompletedServiceNumber).HasMaxLength(50).HasColumnName("completed_service_number");
            e.Property(o => o.CompletedDate).HasColumnName("completed_date");
            e.Property(o => o.CompletedTime).HasColumnName("completed_time");
            e.Property(o => o.CompletedSignature).HasMaxLength(500).HasColumnName("completed_signature");
            e.Property(o => o.CapturingNumber).HasMaxLength(30).HasColumnName("capturing_number");
            e.Property(o => o.Comments).HasColumnName("comments");
            e.HasOne(o => o.Crash).WithMany(c => c.OfficialUses)
                .HasForeignKey(o => o.CrashId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_ou_crash");
        });

      
        modelBuilder.Entity<CrashSketch>(e => {
            e.ToTable("crash_sketches");
            e.HasKey(s => s.SketchId);
            e.Property(s => s.SketchId).HasColumnName("sketch_id");
            e.Property(s => s.CrashId).HasColumnName("crash_id");
            e.Property(s => s.SketchType).HasMaxLength(20).HasDefaultValue("accident").HasColumnName("sketch_type");
            e.Property(s => s.FilePath).HasMaxLength(500).HasColumnName("file_path");
            e.Property(s => s.NorthDirection).HasMaxLength(5).HasColumnName("north_direction");
            e.Property(s => s.Notes).HasColumnName("notes");
            e.Property(s => s.CreatedAt).HasDefaultValueSql("(getdate())").HasColumnName("created_at");
            e.HasOne(s => s.Crash).WithMany(c => c.CrashSketches)
                .HasForeignKey(s => s.CrashId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_cs_crash");
        });

        
        modelBuilder.Entity<SapsStation>().ToTable("lkp_saps_stations");
        modelBuilder.Entity<LookupLocation>().ToTable("lkp_locations");
        modelBuilder.Entity<LookupRoute>().ToTable("lkp_routes");
        modelBuilder.Entity<LookupCrashType>().ToTable("lkp_crash_types");
        modelBuilder.Entity<LookupVehicleType>().ToTable("lkp_vehicle_types");
    }
}