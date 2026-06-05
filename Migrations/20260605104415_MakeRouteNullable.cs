using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrashReport.Migrations
{
    /// <inheritdoc />
    public partial class MakeRouteNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contributory_factors_crashes_CrashId",
                table: "contributory_factors");

            migrationBuilder.DropForeignKey(
                name: "FK_crash_conditions_crashes_CrashId",
                table: "crash_conditions");

            migrationBuilder.DropForeignKey(
                name: "FK_crash_locations_crashes_CrashId",
                table: "crash_locations");

            migrationBuilder.DropForeignKey(
                name: "FK_crash_persons_crash_vehicles_CrashVehicleId",
                table: "crash_persons");

            migrationBuilder.DropForeignKey(
                name: "FK_crash_persons_crashes_CrashId",
                table: "crash_persons");

            migrationBuilder.DropForeignKey(
                name: "FK_crash_persons_persons_PersonId",
                table: "crash_persons");

            migrationBuilder.DropForeignKey(
                name: "FK_crash_sketches_crashes_CrashId",
                table: "crash_sketches");

            migrationBuilder.DropForeignKey(
                name: "FK_crash_vehicles_crashes_CrashId",
                table: "crash_vehicles");

            migrationBuilder.DropForeignKey(
                name: "FK_crash_vehicles_persons_DriverPersonId",
                table: "crash_vehicles");

            migrationBuilder.DropForeignKey(
                name: "FK_crash_vehicles_vehicles_VehicleId",
                table: "crash_vehicles");

            migrationBuilder.DropForeignKey(
                name: "FK_crash_weather_crashes_CrashId",
                table: "crash_weather");

            migrationBuilder.DropForeignKey(
                name: "FK_dangerous_goods_crashes_CrashId",
                table: "dangerous_goods");

            migrationBuilder.DropForeignKey(
                name: "FK_drivers_licences_persons_PersonId",
                table: "drivers_licences");

            migrationBuilder.DropForeignKey(
                name: "FK_official_use_crashes_CrashId",
                table: "official_use");

            migrationBuilder.DropForeignKey(
                name: "FK_pedestrian_bicyclist_details_crash_persons_CrashPersonId",
                table: "pedestrian_bicyclist_details");

            migrationBuilder.DropForeignKey(
                name: "FK_vehicle_damage_crash_vehicles_CrashVehicleId",
                table: "vehicle_damage");

            migrationBuilder.DropForeignKey(
                name: "FK_witnesses_crashes_CrashId",
                table: "witnesses");

            migrationBuilder.DropIndex(
                name: "IX_pedestrian_bicyclist_details_CrashPersonId",
                table: "pedestrian_bicyclist_details");

            migrationBuilder.DropIndex(
                name: "IX_official_use_CrashId",
                table: "official_use");

            migrationBuilder.DropIndex(
                name: "IX_drivers_licences_PersonId",
                table: "drivers_licences");

            migrationBuilder.DropIndex(
                name: "IX_crash_locations_CrashId",
                table: "crash_locations");

            migrationBuilder.DropIndex(
                name: "IX_crash_conditions_CrashId",
                table: "crash_conditions");

            migrationBuilder.RenameColumn(
                name: "WorkContactAddress",
                table: "witnesses",
                newName: "work_contact_address");

            migrationBuilder.RenameColumn(
                name: "SurnameInitials",
                table: "witnesses",
                newName: "surname_initials");

            migrationBuilder.RenameColumn(
                name: "OtherPhone",
                table: "witnesses",
                newName: "other_phone");

            migrationBuilder.RenameColumn(
                name: "IdType",
                table: "witnesses",
                newName: "id_type");

            migrationBuilder.RenameColumn(
                name: "IdNumber",
                table: "witnesses",
                newName: "id_number");

            migrationBuilder.RenameColumn(
                name: "CrashId",
                table: "witnesses",
                newName: "crash_id");

            migrationBuilder.RenameColumn(
                name: "CellPhone",
                table: "witnesses",
                newName: "cell_phone");

            migrationBuilder.RenameColumn(
                name: "WitnessId",
                table: "witnesses",
                newName: "witness_id");

            migrationBuilder.RenameIndex(
                name: "IX_witnesses_CrashId",
                table: "witnesses",
                newName: "IX_witnesses_crash_id");

            migrationBuilder.RenameColumn(
                name: "Model",
                table: "vehicles",
                newName: "model");

            migrationBuilder.RenameColumn(
                name: "Make",
                table: "vehicles",
                newName: "make");

            migrationBuilder.RenameColumn(
                name: "Colour",
                table: "vehicles",
                newName: "colour");

            migrationBuilder.RenameColumn(
                name: "VinNumber",
                table: "vehicles",
                newName: "vin_number");

            migrationBuilder.RenameColumn(
                name: "VehicleTypeCode",
                table: "vehicles",
                newName: "vehicle_type_code");

            migrationBuilder.RenameColumn(
                name: "VehicleCategory",
                table: "vehicles",
                newName: "vehicle_category");

            migrationBuilder.RenameColumn(
                name: "TrailerLicenceNumber",
                table: "vehicles",
                newName: "trailer_licence_number");

            migrationBuilder.RenameColumn(
                name: "SpecialFunction",
                table: "vehicles",
                newName: "special_function");

            migrationBuilder.RenameColumn(
                name: "PrivateOrBusiness",
                table: "vehicles",
                newName: "private_or_business");

            migrationBuilder.RenameColumn(
                name: "LicenceTypeFitting",
                table: "vehicles",
                newName: "licence_type_fitting");

            migrationBuilder.RenameColumn(
                name: "LicenceDiscNumber",
                table: "vehicles",
                newName: "licence_disc_number");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "vehicles",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "CountryOfRegistration",
                table: "vehicles",
                newName: "country_of_registration");

            migrationBuilder.RenameColumn(
                name: "VehicleId",
                table: "vehicles",
                newName: "vehicle_id");

            migrationBuilder.RenameIndex(
                name: "IX_vehicles_VinNumber",
                table: "vehicles",
                newName: "idx_vehicles_vin");

            migrationBuilder.RenameIndex(
                name: "IX_vehicles_LicenceDiscNumber",
                table: "vehicles",
                newName: "idx_vehicles_disc");

            migrationBuilder.RenameColumn(
                name: "DamagePoint",
                table: "vehicle_damage",
                newName: "damage_point");

            migrationBuilder.RenameColumn(
                name: "CrashVehicleId",
                table: "vehicle_damage",
                newName: "crash_vehicle_id");

            migrationBuilder.RenameColumn(
                name: "DamageId",
                table: "vehicle_damage",
                newName: "damage_id");

            migrationBuilder.RenameIndex(
                name: "IX_vehicle_damage_CrashVehicleId",
                table: "vehicle_damage",
                newName: "IX_vehicle_damage_crash_vehicle_id");

            migrationBuilder.RenameColumn(
                name: "Surname",
                table: "persons",
                newName: "surname");

            migrationBuilder.RenameColumn(
                name: "Nationality",
                table: "persons",
                newName: "nationality");

            migrationBuilder.RenameColumn(
                name: "Gender",
                table: "persons",
                newName: "gender");

            migrationBuilder.RenameColumn(
                name: "Age",
                table: "persons",
                newName: "age");

            migrationBuilder.RenameColumn(
                name: "WorkContactAddress",
                table: "persons",
                newName: "work_contact_address");

            migrationBuilder.RenameColumn(
                name: "PopulationGroup",
                table: "persons",
                newName: "population_group");

            migrationBuilder.RenameColumn(
                name: "OtherPhone",
                table: "persons",
                newName: "other_phone");

            migrationBuilder.RenameColumn(
                name: "IdType",
                table: "persons",
                newName: "id_type");

            migrationBuilder.RenameColumn(
                name: "IdNumber",
                table: "persons",
                newName: "id_number");

            migrationBuilder.RenameColumn(
                name: "HomeAddress",
                table: "persons",
                newName: "home_address");

            migrationBuilder.RenameColumn(
                name: "FullNames",
                table: "persons",
                newName: "full_names");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "persons",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "CountryOfOrigin",
                table: "persons",
                newName: "country_of_origin");

            migrationBuilder.RenameColumn(
                name: "CellPhone",
                table: "persons",
                newName: "cell_phone");

            migrationBuilder.RenameColumn(
                name: "PersonId",
                table: "persons",
                newName: "person_id");

            migrationBuilder.RenameIndex(
                name: "IX_persons_Surname",
                table: "persons",
                newName: "idx_persons_surname");

            migrationBuilder.RenameIndex(
                name: "IX_persons_IdNumber",
                table: "persons",
                newName: "idx_persons_id_number");

            migrationBuilder.RenameColumn(
                name: "Manoeuvre",
                table: "pedestrian_bicyclist_details",
                newName: "manoeuvre");

            migrationBuilder.RenameColumn(
                name: "PositionOnRoad",
                table: "pedestrian_bicyclist_details",
                newName: "position_on_road");

            migrationBuilder.RenameColumn(
                name: "PedestrianAction",
                table: "pedestrian_bicyclist_details",
                newName: "pedestrian_action");

            migrationBuilder.RenameColumn(
                name: "LocationReCrossing",
                table: "pedestrian_bicyclist_details",
                newName: "location_re_crossing");

            migrationBuilder.RenameColumn(
                name: "CrashPersonId",
                table: "pedestrian_bicyclist_details",
                newName: "crash_person_id");

            migrationBuilder.RenameColumn(
                name: "ClothingColour",
                table: "pedestrian_bicyclist_details",
                newName: "clothing_colour");

            migrationBuilder.RenameColumn(
                name: "DetailId",
                table: "pedestrian_bicyclist_details",
                newName: "detail_id");

            migrationBuilder.RenameColumn(
                name: "Comments",
                table: "official_use",
                newName: "comments");

            migrationBuilder.RenameColumn(
                name: "SapsCasNo",
                table: "official_use",
                newName: "saps_cas_no");

            migrationBuilder.RenameColumn(
                name: "OfficeWhereReported",
                table: "official_use",
                newName: "office_where_reported");

            migrationBuilder.RenameColumn(
                name: "OfficeWhereOccurred",
                table: "official_use",
                newName: "office_where_occurred");

            migrationBuilder.RenameColumn(
                name: "OccurrenceBookNo",
                table: "official_use",
                newName: "occurrence_book_no");

            migrationBuilder.RenameColumn(
                name: "InspectedBySurname",
                table: "official_use",
                newName: "inspected_by_surname");

            migrationBuilder.RenameColumn(
                name: "InspectedBySignature",
                table: "official_use",
                newName: "inspected_by_signature");

            migrationBuilder.RenameColumn(
                name: "InspectedByServiceNumber",
                table: "official_use",
                newName: "inspected_by_service_number");

            migrationBuilder.RenameColumn(
                name: "InspectedByRank",
                table: "official_use",
                newName: "inspected_by_rank");

            migrationBuilder.RenameColumn(
                name: "InspectedByInitials",
                table: "official_use",
                newName: "inspected_by_initials");

            migrationBuilder.RenameColumn(
                name: "DepartmentNameReported",
                table: "official_use",
                newName: "department_name_reported");

            migrationBuilder.RenameColumn(
                name: "DepartmentNameOccurred",
                table: "official_use",
                newName: "department_name_occurred");

            migrationBuilder.RenameColumn(
                name: "DateStamp",
                table: "official_use",
                newName: "date_stamp");

            migrationBuilder.RenameColumn(
                name: "CrashId",
                table: "official_use",
                newName: "crash_id");

            migrationBuilder.RenameColumn(
                name: "CompletedTime",
                table: "official_use",
                newName: "completed_time");

            migrationBuilder.RenameColumn(
                name: "CompletedSurname",
                table: "official_use",
                newName: "completed_surname");

            migrationBuilder.RenameColumn(
                name: "CompletedSignature",
                table: "official_use",
                newName: "completed_signature");

            migrationBuilder.RenameColumn(
                name: "CompletedServiceNumber",
                table: "official_use",
                newName: "completed_service_number");

            migrationBuilder.RenameColumn(
                name: "CompletedRank",
                table: "official_use",
                newName: "completed_rank");

            migrationBuilder.RenameColumn(
                name: "CompletedInitials",
                table: "official_use",
                newName: "completed_initials");

            migrationBuilder.RenameColumn(
                name: "CompletedDate",
                table: "official_use",
                newName: "completed_date");

            migrationBuilder.RenameColumn(
                name: "CompletedBy",
                table: "official_use",
                newName: "completed_by");

            migrationBuilder.RenameColumn(
                name: "CapturingNumber",
                table: "official_use",
                newName: "capturing_number");

            migrationBuilder.RenameColumn(
                name: "AccidentRegisterNo",
                table: "official_use",
                newName: "accident_register_no");

            migrationBuilder.RenameColumn(
                name: "OfficialId",
                table: "official_use",
                newName: "official_id");

            migrationBuilder.RenameColumn(
                name: "PrdpCode",
                table: "drivers_licences",
                newName: "prdp_code");

            migrationBuilder.RenameColumn(
                name: "PersonId",
                table: "drivers_licences",
                newName: "person_id");

            migrationBuilder.RenameColumn(
                name: "LicenceType",
                table: "drivers_licences",
                newName: "licence_type");

            migrationBuilder.RenameColumn(
                name: "LicenceNumber",
                table: "drivers_licences",
                newName: "licence_number");

            migrationBuilder.RenameColumn(
                name: "LicenceCode",
                table: "drivers_licences",
                newName: "licence_code");

            migrationBuilder.RenameColumn(
                name: "DateOfIssue",
                table: "drivers_licences",
                newName: "date_of_issue");

            migrationBuilder.RenameColumn(
                name: "LicenceId",
                table: "drivers_licences",
                newName: "licence_id");

            migrationBuilder.RenameColumn(
                name: "VehicleReference",
                table: "dangerous_goods",
                newName: "vehicle_reference");

            migrationBuilder.RenameColumn(
                name: "VapourGasEmission",
                table: "dangerous_goods",
                newName: "vapour_gas_emission");

            migrationBuilder.RenameColumn(
                name: "UnNumber",
                table: "dangerous_goods",
                newName: "un_number");

            migrationBuilder.RenameColumn(
                name: "SpillageObserved",
                table: "dangerous_goods",
                newName: "spillage_observed");

            migrationBuilder.RenameColumn(
                name: "PlacardDisplayed",
                table: "dangerous_goods",
                newName: "placard_displayed");

            migrationBuilder.RenameColumn(
                name: "GoodsCarried",
                table: "dangerous_goods",
                newName: "goods_carried");

            migrationBuilder.RenameColumn(
                name: "EmergencyServicesActivated",
                table: "dangerous_goods",
                newName: "emergency_services_activated");

            migrationBuilder.RenameColumn(
                name: "CrashId",
                table: "dangerous_goods",
                newName: "crash_id");

            migrationBuilder.RenameColumn(
                name: "CompanyName",
                table: "dangerous_goods",
                newName: "company_name");

            migrationBuilder.RenameColumn(
                name: "DgId",
                table: "dangerous_goods",
                newName: "dg_id");

            migrationBuilder.RenameIndex(
                name: "IX_dangerous_goods_CrashId",
                table: "dangerous_goods",
                newName: "IX_dangerous_goods_crash_id");

            migrationBuilder.RenameColumn(
                name: "SpeedLimitKmh",
                table: "crashes",
                newName: "speed_limit_kmh");

            migrationBuilder.RenameColumn(
                name: "RoadNumber",
                table: "crashes",
                newName: "road_number");

            migrationBuilder.RenameColumn(
                name: "ProvinceCode",
                table: "crashes",
                newName: "province_code");

            migrationBuilder.RenameColumn(
                name: "NoOfVehiclesInvolved",
                table: "crashes",
                newName: "no_of_vehicles_involved");

            migrationBuilder.RenameColumn(
                name: "NoOfAppendices",
                table: "crashes",
                newName: "no_of_appendices");

            migrationBuilder.RenameColumn(
                name: "KmMarker",
                table: "crashes",
                newName: "km_marker");

            migrationBuilder.RenameColumn(
                name: "IncidentReportNo",
                table: "crashes",
                newName: "incident_report_no");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "crashes",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "CrashTime",
                table: "crashes",
                newName: "crash_time");

            migrationBuilder.RenameColumn(
                name: "CrashDate",
                table: "crashes",
                newName: "crash_date");

            migrationBuilder.RenameColumn(
                name: "CrNo",
                table: "crashes",
                newName: "cr_no");

            migrationBuilder.RenameColumn(
                name: "CasNo",
                table: "crashes",
                newName: "cas_no");

            migrationBuilder.RenameColumn(
                name: "CapturingNumber",
                table: "crashes",
                newName: "capturing_number");

            migrationBuilder.RenameColumn(
                name: "BriefDescription",
                table: "crashes",
                newName: "brief_description");

            migrationBuilder.RenameColumn(
                name: "CrashId",
                table: "crashes",
                newName: "crash_id");

            migrationBuilder.RenameIndex(
                name: "IX_crashes_ProvinceCode",
                table: "crashes",
                newName: "idx_crashes_province");

            migrationBuilder.RenameIndex(
                name: "IX_crashes_CrashDate",
                table: "crashes",
                newName: "idx_crashes_date");

            migrationBuilder.RenameIndex(
                name: "IX_crashes_CasNo",
                table: "crashes",
                newName: "idx_crashes_cas_no");

            migrationBuilder.RenameColumn(
                name: "WeatherCondition",
                table: "crash_weather",
                newName: "weather_condition");

            migrationBuilder.RenameColumn(
                name: "CrashId",
                table: "crash_weather",
                newName: "crash_id");

            migrationBuilder.RenameColumn(
                name: "VehicleReference",
                table: "crash_vehicles",
                newName: "vehicle_reference");

            migrationBuilder.RenameColumn(
                name: "VehicleManoeuvre",
                table: "crash_vehicles",
                newName: "vehicle_manoeuvre");

            migrationBuilder.RenameColumn(
                name: "VehicleId",
                table: "crash_vehicles",
                newName: "vehicle_id");

            migrationBuilder.RenameColumn(
                name: "SeatbeltUsed",
                table: "crash_vehicles",
                newName: "seatbelt_used");

            migrationBuilder.RenameColumn(
                name: "PositionBeforeCrash",
                table: "crash_vehicles",
                newName: "position_before_crash");

            migrationBuilder.RenameColumn(
                name: "PassengersForReward",
                table: "crash_vehicles",
                newName: "passengers_for_reward");

            migrationBuilder.RenameColumn(
                name: "DrugTestResult",
                table: "crash_vehicles",
                newName: "drug_test_result");

            migrationBuilder.RenameColumn(
                name: "DrugSuspected",
                table: "crash_vehicles",
                newName: "drug_suspected");

            migrationBuilder.RenameColumn(
                name: "DriverPersonId",
                table: "crash_vehicles",
                newName: "driver_person_id");

            migrationBuilder.RenameColumn(
                name: "CrashId",
                table: "crash_vehicles",
                newName: "crash_id");

            migrationBuilder.RenameColumn(
                name: "BreakdownCompany",
                table: "crash_vehicles",
                newName: "breakdown_company");

            migrationBuilder.RenameColumn(
                name: "AlcoholTestResult",
                table: "crash_vehicles",
                newName: "alcohol_test_result");

            migrationBuilder.RenameColumn(
                name: "AlcoholSuspected",
                table: "crash_vehicles",
                newName: "alcohol_suspected");

            migrationBuilder.RenameColumn(
                name: "CrashVehicleId",
                table: "crash_vehicles",
                newName: "crash_vehicle_id");

            migrationBuilder.RenameIndex(
                name: "IX_crash_vehicles_VehicleId",
                table: "crash_vehicles",
                newName: "idx_cv_vehicle");

            migrationBuilder.RenameIndex(
                name: "IX_crash_vehicles_DriverPersonId",
                table: "crash_vehicles",
                newName: "idx_cv_driver");

            migrationBuilder.RenameIndex(
                name: "IX_crash_vehicles_CrashId",
                table: "crash_vehicles",
                newName: "idx_cv_crash");

            migrationBuilder.RenameColumn(
                name: "Notes",
                table: "crash_sketches",
                newName: "notes");

            migrationBuilder.RenameColumn(
                name: "SketchType",
                table: "crash_sketches",
                newName: "sketch_type");

            migrationBuilder.RenameColumn(
                name: "NorthDirection",
                table: "crash_sketches",
                newName: "north_direction");

            migrationBuilder.RenameColumn(
                name: "FilePath",
                table: "crash_sketches",
                newName: "file_path");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "crash_sketches",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "CrashId",
                table: "crash_sketches",
                newName: "crash_id");

            migrationBuilder.RenameColumn(
                name: "SketchId",
                table: "crash_sketches",
                newName: "sketch_id");

            migrationBuilder.RenameIndex(
                name: "IX_crash_sketches_CrashId",
                table: "crash_sketches",
                newName: "IX_crash_sketches_crash_id");

            migrationBuilder.RenameColumn(
                name: "Role",
                table: "crash_persons",
                newName: "role");

            migrationBuilder.RenameColumn(
                name: "Hospital",
                table: "crash_persons",
                newName: "hospital");

            migrationBuilder.RenameColumn(
                name: "VehicleReference",
                table: "crash_persons",
                newName: "vehicle_reference");

            migrationBuilder.RenameColumn(
                name: "SeverityOfInjury",
                table: "crash_persons",
                newName: "severity_of_injury");

            migrationBuilder.RenameColumn(
                name: "SeatingPosition",
                table: "crash_persons",
                newName: "seating_position");

            migrationBuilder.RenameColumn(
                name: "SeatbeltHelmetUsed",
                table: "crash_persons",
                newName: "seatbelt_helmet_used");

            migrationBuilder.RenameColumn(
                name: "PersonReference",
                table: "crash_persons",
                newName: "person_reference");

            migrationBuilder.RenameColumn(
                name: "PersonId",
                table: "crash_persons",
                newName: "person_id");

            migrationBuilder.RenameColumn(
                name: "PassengerNumber",
                table: "crash_persons",
                newName: "passenger_number");

            migrationBuilder.RenameColumn(
                name: "LiquorDrugTestDone",
                table: "crash_persons",
                newName: "liquor_drug_test_done");

            migrationBuilder.RenameColumn(
                name: "LiquorDrugSuspected",
                table: "crash_persons",
                newName: "liquor_drug_suspected");

            migrationBuilder.RenameColumn(
                name: "CrashVehicleId",
                table: "crash_persons",
                newName: "crash_vehicle_id");

            migrationBuilder.RenameColumn(
                name: "CrashId",
                table: "crash_persons",
                newName: "crash_id");

            migrationBuilder.RenameColumn(
                name: "ChildRestraintUsed",
                table: "crash_persons",
                newName: "child_restraint_used");

            migrationBuilder.RenameColumn(
                name: "AmbulanceServiceRef",
                table: "crash_persons",
                newName: "ambulance_service_ref");

            migrationBuilder.RenameColumn(
                name: "CrashPersonId",
                table: "crash_persons",
                newName: "crash_person_id");

            migrationBuilder.RenameIndex(
                name: "IX_crash_persons_SeverityOfInjury",
                table: "crash_persons",
                newName: "idx_cp_injury");

            migrationBuilder.RenameIndex(
                name: "IX_crash_persons_PersonId",
                table: "crash_persons",
                newName: "idx_cp_person");

            migrationBuilder.RenameIndex(
                name: "IX_crash_persons_CrashVehicleId",
                table: "crash_persons",
                newName: "IX_crash_persons_crash_vehicle_id");

            migrationBuilder.RenameIndex(
                name: "IX_crash_persons_CrashId",
                table: "crash_persons",
                newName: "idx_cp_crash");

            migrationBuilder.RenameColumn(
                name: "Suburb",
                table: "crash_locations",
                newName: "suburb");

            migrationBuilder.RenameColumn(
                name: "StreetRoadName",
                table: "crash_locations",
                newName: "street_road_name");

            migrationBuilder.RenameColumn(
                name: "RoadSurfaceType",
                table: "crash_locations",
                newName: "road_surface_type");

            migrationBuilder.RenameColumn(
                name: "RoadSurfaceQuality",
                table: "crash_locations",
                newName: "road_surface_quality");

            migrationBuilder.RenameColumn(
                name: "RoadSurfaceCondition",
                table: "crash_locations",
                newName: "road_surface_condition");

            migrationBuilder.RenameColumn(
                name: "RoadLayout",
                table: "crash_locations",
                newName: "road_layout");

            migrationBuilder.RenameColumn(
                name: "RoadFunctionalClassification",
                table: "crash_locations",
                newName: "road_functional_classification");

            migrationBuilder.RenameColumn(
                name: "NextCityTown",
                table: "crash_locations",
                newName: "next_city_town");

            migrationBuilder.RenameColumn(
                name: "KmMarkerInfo",
                table: "crash_locations",
                newName: "km_marker_info");

            migrationBuilder.RenameColumn(
                name: "JunctionType",
                table: "crash_locations",
                newName: "junction_type");

            migrationBuilder.RenameColumn(
                name: "IntersectionStreet",
                table: "crash_locations",
                newName: "intersection_street");

            migrationBuilder.RenameColumn(
                name: "IntersectionRoadNo",
                table: "crash_locations",
                newName: "intersection_road_no");

            migrationBuilder.RenameColumn(
                name: "GpsYCoordinate",
                table: "crash_locations",
                newName: "gps_y_coordinate");

            migrationBuilder.RenameColumn(
                name: "GpsXCoordinate",
                table: "crash_locations",
                newName: "gps_x_coordinate");

            migrationBuilder.RenameColumn(
                name: "FromPoint",
                table: "crash_locations",
                newName: "from_point");

            migrationBuilder.RenameColumn(
                name: "DistanceKm",
                table: "crash_locations",
                newName: "distance_km");

            migrationBuilder.RenameColumn(
                name: "CrashId",
                table: "crash_locations",
                newName: "crash_id");

            migrationBuilder.RenameColumn(
                name: "CompassDirection",
                table: "crash_locations",
                newName: "compass_direction");

            migrationBuilder.RenameColumn(
                name: "CityTown",
                table: "crash_locations",
                newName: "city_town");

            migrationBuilder.RenameColumn(
                name: "BuiltUpArea",
                table: "crash_locations",
                newName: "built_up_area");

            migrationBuilder.RenameColumn(
                name: "BetweenTo",
                table: "crash_locations",
                newName: "between_to");

            migrationBuilder.RenameColumn(
                name: "BetweenFrom",
                table: "crash_locations",
                newName: "between_from");

            migrationBuilder.RenameColumn(
                name: "AreaType",
                table: "crash_locations",
                newName: "area_type");

            migrationBuilder.RenameColumn(
                name: "LocationId",
                table: "crash_locations",
                newName: "location_id");

            migrationBuilder.RenameColumn(
                name: "VehicleLightsCondition",
                table: "crash_conditions",
                newName: "vehicle_lights_condition");

            migrationBuilder.RenameColumn(
                name: "TyreBurstObserved",
                table: "crash_conditions",
                newName: "tyre_burst_observed");

            migrationBuilder.RenameColumn(
                name: "TrafficControlType",
                table: "crash_conditions",
                newName: "traffic_control_type");

            migrationBuilder.RenameColumn(
                name: "RoadSignsCondition",
                table: "crash_conditions",
                newName: "road_signs_condition");

            migrationBuilder.RenameColumn(
                name: "RoadSegmentGrade",
                table: "crash_conditions",
                newName: "road_segment_grade");

            migrationBuilder.RenameColumn(
                name: "RoadMarkingVisibility",
                table: "crash_conditions",
                newName: "road_marking_visibility");

            migrationBuilder.RenameColumn(
                name: "OvertakingControl",
                table: "crash_conditions",
                newName: "overtaking_control");

            migrationBuilder.RenameColumn(
                name: "OtherObservations",
                table: "crash_conditions",
                newName: "other_observations");

            migrationBuilder.RenameColumn(
                name: "ObstructionType",
                table: "crash_conditions",
                newName: "obstruction_type");

            migrationBuilder.RenameColumn(
                name: "LightCondition",
                table: "crash_conditions",
                newName: "light_condition");

            migrationBuilder.RenameColumn(
                name: "HitAndRun",
                table: "crash_conditions",
                newName: "hit_and_run");

            migrationBuilder.RenameColumn(
                name: "CrashType",
                table: "crash_conditions",
                newName: "crash_type");

            migrationBuilder.RenameColumn(
                name: "CrashId",
                table: "crash_conditions",
                newName: "crash_id");

            migrationBuilder.RenameColumn(
                name: "ConditionId",
                table: "crash_conditions",
                newName: "condition_id");

            migrationBuilder.RenameColumn(
                name: "IsMajorFactor",
                table: "contributory_factors",
                newName: "is_major_factor");

            migrationBuilder.RenameColumn(
                name: "FactorDescription",
                table: "contributory_factors",
                newName: "factor_description");

            migrationBuilder.RenameColumn(
                name: "FactorCategory",
                table: "contributory_factors",
                newName: "factor_category");

            migrationBuilder.RenameColumn(
                name: "CrashId",
                table: "contributory_factors",
                newName: "crash_id");

            migrationBuilder.RenameColumn(
                name: "FactorId",
                table: "contributory_factors",
                newName: "factor_id");

            migrationBuilder.RenameIndex(
                name: "IX_contributory_factors_FactorCategory",
                table: "contributory_factors",
                newName: "idx_cf_category");

            migrationBuilder.RenameIndex(
                name: "IX_contributory_factors_CrashId",
                table: "contributory_factors",
                newName: "idx_cf_crash");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "vehicles",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "(getdate())",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "country_of_registration",
                table: "vehicles",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "RSA",
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AddColumn<string>(
                name: "VehicleType",
                table: "vehicles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "persons",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "(getdate())",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "vehicle_reference",
                table: "dangerous_goods",
                type: "nchar(2)",
                fixedLength: true,
                maxLength: 2,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2)",
                oldMaxLength: 2);

            migrationBuilder.AlterColumn<byte>(
                name: "no_of_vehicles_involved",
                table: "crashes",
                type: "tinyint",
                nullable: true,
                defaultValue: (byte)1,
                oldClrType: typeof(byte),
                oldType: "tinyint");

            migrationBuilder.AlterColumn<byte>(
                name: "no_of_appendices",
                table: "crashes",
                type: "tinyint",
                nullable: true,
                defaultValue: (byte)0,
                oldClrType: typeof(byte),
                oldType: "tinyint");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "crashes",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "(getdate())",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<string>(
                name: "VehicleString",
                table: "crashes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "vehicle_reference",
                table: "crash_vehicles",
                type: "nchar(2)",
                fixedLength: true,
                maxLength: 2,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2)",
                oldMaxLength: 2);

            migrationBuilder.AddColumn<string>(
                name: "vehicle_type",
                table: "crash_vehicles",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "sketch_type",
                table: "crash_sketches",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "accident",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "crash_sketches",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "(getdate())",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "vehicle_reference",
                table: "crash_persons",
                type: "nchar(2)",
                fixedLength: true,
                maxLength: 2,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2)",
                oldMaxLength: 2,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "lkp_crash_types",
                columns: table => new
                {
                    crash_type_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    crash_type_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lkp_crash_types", x => x.crash_type_id);
                });

            migrationBuilder.CreateTable(
                name: "lkp_routes",
                columns: table => new
                {
                    route_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    route_code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    province_code = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lkp_routes", x => x.route_id);
                });

            migrationBuilder.CreateTable(
                name: "lkp_saps_stations",
                columns: table => new
                {
                    station_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    station_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    province_code = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    district = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lkp_saps_stations", x => x.station_id);
                });

            migrationBuilder.CreateTable(
                name: "lkp_vehicle_types",
                columns: table => new
                {
                    vehicle_type_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vehicle_type_code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lkp_vehicle_types", x => x.vehicle_type_id);
                });

            migrationBuilder.CreateTable(
                name: "lkp_locations",
                columns: table => new
                {
                    location_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    location_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    station_id = table.Column<int>(type: "int", nullable: true),
                    province_code = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lkp_locations", x => x.location_id);
                    table.ForeignKey(
                        name: "FK_lkp_locations_lkp_saps_stations_station_id",
                        column: x => x.station_id,
                        principalTable: "lkp_saps_stations",
                        principalColumn: "station_id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_pedestrian_bicyclist_details_crash_person_id",
                table: "pedestrian_bicyclist_details",
                column: "crash_person_id");

            migrationBuilder.CreateIndex(
                name: "IX_official_use_crash_id",
                table: "official_use",
                column: "crash_id");

            migrationBuilder.CreateIndex(
                name: "IX_drivers_licences_person_id",
                table: "drivers_licences",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_crash_locations_crash_id",
                table: "crash_locations",
                column: "crash_id");

            migrationBuilder.CreateIndex(
                name: "IX_crash_conditions_crash_id",
                table: "crash_conditions",
                column: "crash_id");

            migrationBuilder.CreateIndex(
                name: "IX_lkp_locations_station_id",
                table: "lkp_locations",
                column: "station_id");

            migrationBuilder.AddForeignKey(
                name: "fk_cf_crash",
                table: "contributory_factors",
                column: "crash_id",
                principalTable: "crashes",
                principalColumn: "crash_id");

            migrationBuilder.AddForeignKey(
                name: "fk_cc_crash",
                table: "crash_conditions",
                column: "crash_id",
                principalTable: "crashes",
                principalColumn: "crash_id");

            migrationBuilder.AddForeignKey(
                name: "fk_cl_crash",
                table: "crash_locations",
                column: "crash_id",
                principalTable: "crashes",
                principalColumn: "crash_id");

            migrationBuilder.AddForeignKey(
                name: "fk_cp_crash",
                table: "crash_persons",
                column: "crash_id",
                principalTable: "crashes",
                principalColumn: "crash_id");

            migrationBuilder.AddForeignKey(
                name: "fk_cp_crash_vehicle",
                table: "crash_persons",
                column: "crash_vehicle_id",
                principalTable: "crash_vehicles",
                principalColumn: "crash_vehicle_id");

            migrationBuilder.AddForeignKey(
                name: "fk_cp_person",
                table: "crash_persons",
                column: "person_id",
                principalTable: "persons",
                principalColumn: "person_id");

            migrationBuilder.AddForeignKey(
                name: "fk_cs_crash",
                table: "crash_sketches",
                column: "crash_id",
                principalTable: "crashes",
                principalColumn: "crash_id");

            migrationBuilder.AddForeignKey(
                name: "fk_cv_crash",
                table: "crash_vehicles",
                column: "crash_id",
                principalTable: "crashes",
                principalColumn: "crash_id");

            migrationBuilder.AddForeignKey(
                name: "fk_cv_driver",
                table: "crash_vehicles",
                column: "driver_person_id",
                principalTable: "persons",
                principalColumn: "person_id");

            migrationBuilder.AddForeignKey(
                name: "fk_cv_vehicle",
                table: "crash_vehicles",
                column: "vehicle_id",
                principalTable: "vehicles",
                principalColumn: "vehicle_id");

            migrationBuilder.AddForeignKey(
                name: "fk_cw_crash",
                table: "crash_weather",
                column: "crash_id",
                principalTable: "crashes",
                principalColumn: "crash_id");

            migrationBuilder.AddForeignKey(
                name: "fk_dg_crash",
                table: "dangerous_goods",
                column: "crash_id",
                principalTable: "crashes",
                principalColumn: "crash_id");

            migrationBuilder.AddForeignKey(
                name: "fk_dl_person",
                table: "drivers_licences",
                column: "person_id",
                principalTable: "persons",
                principalColumn: "person_id");

            migrationBuilder.AddForeignKey(
                name: "fk_ou_crash",
                table: "official_use",
                column: "crash_id",
                principalTable: "crashes",
                principalColumn: "crash_id");

            migrationBuilder.AddForeignKey(
                name: "fk_pd_cp",
                table: "pedestrian_bicyclist_details",
                column: "crash_person_id",
                principalTable: "crash_persons",
                principalColumn: "crash_person_id");

            migrationBuilder.AddForeignKey(
                name: "fk_vd_crash_vehicle",
                table: "vehicle_damage",
                column: "crash_vehicle_id",
                principalTable: "crash_vehicles",
                principalColumn: "crash_vehicle_id");

            migrationBuilder.AddForeignKey(
                name: "fk_w_crash",
                table: "witnesses",
                column: "crash_id",
                principalTable: "crashes",
                principalColumn: "crash_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_cf_crash",
                table: "contributory_factors");

            migrationBuilder.DropForeignKey(
                name: "fk_cc_crash",
                table: "crash_conditions");

            migrationBuilder.DropForeignKey(
                name: "fk_cl_crash",
                table: "crash_locations");

            migrationBuilder.DropForeignKey(
                name: "fk_cp_crash",
                table: "crash_persons");

            migrationBuilder.DropForeignKey(
                name: "fk_cp_crash_vehicle",
                table: "crash_persons");

            migrationBuilder.DropForeignKey(
                name: "fk_cp_person",
                table: "crash_persons");

            migrationBuilder.DropForeignKey(
                name: "fk_cs_crash",
                table: "crash_sketches");

            migrationBuilder.DropForeignKey(
                name: "fk_cv_crash",
                table: "crash_vehicles");

            migrationBuilder.DropForeignKey(
                name: "fk_cv_driver",
                table: "crash_vehicles");

            migrationBuilder.DropForeignKey(
                name: "fk_cv_vehicle",
                table: "crash_vehicles");

            migrationBuilder.DropForeignKey(
                name: "fk_cw_crash",
                table: "crash_weather");

            migrationBuilder.DropForeignKey(
                name: "fk_dg_crash",
                table: "dangerous_goods");

            migrationBuilder.DropForeignKey(
                name: "fk_dl_person",
                table: "drivers_licences");

            migrationBuilder.DropForeignKey(
                name: "fk_ou_crash",
                table: "official_use");

            migrationBuilder.DropForeignKey(
                name: "fk_pd_cp",
                table: "pedestrian_bicyclist_details");

            migrationBuilder.DropForeignKey(
                name: "fk_vd_crash_vehicle",
                table: "vehicle_damage");

            migrationBuilder.DropForeignKey(
                name: "fk_w_crash",
                table: "witnesses");

            migrationBuilder.DropTable(
                name: "lkp_crash_types");

            migrationBuilder.DropTable(
                name: "lkp_locations");

            migrationBuilder.DropTable(
                name: "lkp_routes");

            migrationBuilder.DropTable(
                name: "lkp_vehicle_types");

            migrationBuilder.DropTable(
                name: "lkp_saps_stations");

            migrationBuilder.DropIndex(
                name: "IX_pedestrian_bicyclist_details_crash_person_id",
                table: "pedestrian_bicyclist_details");

            migrationBuilder.DropIndex(
                name: "IX_official_use_crash_id",
                table: "official_use");

            migrationBuilder.DropIndex(
                name: "IX_drivers_licences_person_id",
                table: "drivers_licences");

            migrationBuilder.DropIndex(
                name: "IX_crash_locations_crash_id",
                table: "crash_locations");

            migrationBuilder.DropIndex(
                name: "IX_crash_conditions_crash_id",
                table: "crash_conditions");

            migrationBuilder.DropColumn(
                name: "VehicleType",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "VehicleString",
                table: "crashes");

            migrationBuilder.DropColumn(
                name: "vehicle_type",
                table: "crash_vehicles");

            migrationBuilder.RenameColumn(
                name: "work_contact_address",
                table: "witnesses",
                newName: "WorkContactAddress");

            migrationBuilder.RenameColumn(
                name: "surname_initials",
                table: "witnesses",
                newName: "SurnameInitials");

            migrationBuilder.RenameColumn(
                name: "other_phone",
                table: "witnesses",
                newName: "OtherPhone");

            migrationBuilder.RenameColumn(
                name: "id_type",
                table: "witnesses",
                newName: "IdType");

            migrationBuilder.RenameColumn(
                name: "id_number",
                table: "witnesses",
                newName: "IdNumber");

            migrationBuilder.RenameColumn(
                name: "crash_id",
                table: "witnesses",
                newName: "CrashId");

            migrationBuilder.RenameColumn(
                name: "cell_phone",
                table: "witnesses",
                newName: "CellPhone");

            migrationBuilder.RenameColumn(
                name: "witness_id",
                table: "witnesses",
                newName: "WitnessId");

            migrationBuilder.RenameIndex(
                name: "IX_witnesses_crash_id",
                table: "witnesses",
                newName: "IX_witnesses_CrashId");

            migrationBuilder.RenameColumn(
                name: "model",
                table: "vehicles",
                newName: "Model");

            migrationBuilder.RenameColumn(
                name: "make",
                table: "vehicles",
                newName: "Make");

            migrationBuilder.RenameColumn(
                name: "colour",
                table: "vehicles",
                newName: "Colour");

            migrationBuilder.RenameColumn(
                name: "vin_number",
                table: "vehicles",
                newName: "VinNumber");

            migrationBuilder.RenameColumn(
                name: "vehicle_type_code",
                table: "vehicles",
                newName: "VehicleTypeCode");

            migrationBuilder.RenameColumn(
                name: "vehicle_category",
                table: "vehicles",
                newName: "VehicleCategory");

            migrationBuilder.RenameColumn(
                name: "trailer_licence_number",
                table: "vehicles",
                newName: "TrailerLicenceNumber");

            migrationBuilder.RenameColumn(
                name: "special_function",
                table: "vehicles",
                newName: "SpecialFunction");

            migrationBuilder.RenameColumn(
                name: "private_or_business",
                table: "vehicles",
                newName: "PrivateOrBusiness");

            migrationBuilder.RenameColumn(
                name: "licence_type_fitting",
                table: "vehicles",
                newName: "LicenceTypeFitting");

            migrationBuilder.RenameColumn(
                name: "licence_disc_number",
                table: "vehicles",
                newName: "LicenceDiscNumber");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "vehicles",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "country_of_registration",
                table: "vehicles",
                newName: "CountryOfRegistration");

            migrationBuilder.RenameColumn(
                name: "vehicle_id",
                table: "vehicles",
                newName: "VehicleId");

            migrationBuilder.RenameIndex(
                name: "idx_vehicles_vin",
                table: "vehicles",
                newName: "IX_vehicles_VinNumber");

            migrationBuilder.RenameIndex(
                name: "idx_vehicles_disc",
                table: "vehicles",
                newName: "IX_vehicles_LicenceDiscNumber");

            migrationBuilder.RenameColumn(
                name: "damage_point",
                table: "vehicle_damage",
                newName: "DamagePoint");

            migrationBuilder.RenameColumn(
                name: "crash_vehicle_id",
                table: "vehicle_damage",
                newName: "CrashVehicleId");

            migrationBuilder.RenameColumn(
                name: "damage_id",
                table: "vehicle_damage",
                newName: "DamageId");

            migrationBuilder.RenameIndex(
                name: "IX_vehicle_damage_crash_vehicle_id",
                table: "vehicle_damage",
                newName: "IX_vehicle_damage_CrashVehicleId");

            migrationBuilder.RenameColumn(
                name: "surname",
                table: "persons",
                newName: "Surname");

            migrationBuilder.RenameColumn(
                name: "nationality",
                table: "persons",
                newName: "Nationality");

            migrationBuilder.RenameColumn(
                name: "gender",
                table: "persons",
                newName: "Gender");

            migrationBuilder.RenameColumn(
                name: "age",
                table: "persons",
                newName: "Age");

            migrationBuilder.RenameColumn(
                name: "work_contact_address",
                table: "persons",
                newName: "WorkContactAddress");

            migrationBuilder.RenameColumn(
                name: "population_group",
                table: "persons",
                newName: "PopulationGroup");

            migrationBuilder.RenameColumn(
                name: "other_phone",
                table: "persons",
                newName: "OtherPhone");

            migrationBuilder.RenameColumn(
                name: "id_type",
                table: "persons",
                newName: "IdType");

            migrationBuilder.RenameColumn(
                name: "id_number",
                table: "persons",
                newName: "IdNumber");

            migrationBuilder.RenameColumn(
                name: "home_address",
                table: "persons",
                newName: "HomeAddress");

            migrationBuilder.RenameColumn(
                name: "full_names",
                table: "persons",
                newName: "FullNames");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "persons",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "country_of_origin",
                table: "persons",
                newName: "CountryOfOrigin");

            migrationBuilder.RenameColumn(
                name: "cell_phone",
                table: "persons",
                newName: "CellPhone");

            migrationBuilder.RenameColumn(
                name: "person_id",
                table: "persons",
                newName: "PersonId");

            migrationBuilder.RenameIndex(
                name: "idx_persons_surname",
                table: "persons",
                newName: "IX_persons_Surname");

            migrationBuilder.RenameIndex(
                name: "idx_persons_id_number",
                table: "persons",
                newName: "IX_persons_IdNumber");

            migrationBuilder.RenameColumn(
                name: "manoeuvre",
                table: "pedestrian_bicyclist_details",
                newName: "Manoeuvre");

            migrationBuilder.RenameColumn(
                name: "position_on_road",
                table: "pedestrian_bicyclist_details",
                newName: "PositionOnRoad");

            migrationBuilder.RenameColumn(
                name: "pedestrian_action",
                table: "pedestrian_bicyclist_details",
                newName: "PedestrianAction");

            migrationBuilder.RenameColumn(
                name: "location_re_crossing",
                table: "pedestrian_bicyclist_details",
                newName: "LocationReCrossing");

            migrationBuilder.RenameColumn(
                name: "crash_person_id",
                table: "pedestrian_bicyclist_details",
                newName: "CrashPersonId");

            migrationBuilder.RenameColumn(
                name: "clothing_colour",
                table: "pedestrian_bicyclist_details",
                newName: "ClothingColour");

            migrationBuilder.RenameColumn(
                name: "detail_id",
                table: "pedestrian_bicyclist_details",
                newName: "DetailId");

            migrationBuilder.RenameColumn(
                name: "comments",
                table: "official_use",
                newName: "Comments");

            migrationBuilder.RenameColumn(
                name: "saps_cas_no",
                table: "official_use",
                newName: "SapsCasNo");

            migrationBuilder.RenameColumn(
                name: "office_where_reported",
                table: "official_use",
                newName: "OfficeWhereReported");

            migrationBuilder.RenameColumn(
                name: "office_where_occurred",
                table: "official_use",
                newName: "OfficeWhereOccurred");

            migrationBuilder.RenameColumn(
                name: "occurrence_book_no",
                table: "official_use",
                newName: "OccurrenceBookNo");

            migrationBuilder.RenameColumn(
                name: "inspected_by_surname",
                table: "official_use",
                newName: "InspectedBySurname");

            migrationBuilder.RenameColumn(
                name: "inspected_by_signature",
                table: "official_use",
                newName: "InspectedBySignature");

            migrationBuilder.RenameColumn(
                name: "inspected_by_service_number",
                table: "official_use",
                newName: "InspectedByServiceNumber");

            migrationBuilder.RenameColumn(
                name: "inspected_by_rank",
                table: "official_use",
                newName: "InspectedByRank");

            migrationBuilder.RenameColumn(
                name: "inspected_by_initials",
                table: "official_use",
                newName: "InspectedByInitials");

            migrationBuilder.RenameColumn(
                name: "department_name_reported",
                table: "official_use",
                newName: "DepartmentNameReported");

            migrationBuilder.RenameColumn(
                name: "department_name_occurred",
                table: "official_use",
                newName: "DepartmentNameOccurred");

            migrationBuilder.RenameColumn(
                name: "date_stamp",
                table: "official_use",
                newName: "DateStamp");

            migrationBuilder.RenameColumn(
                name: "crash_id",
                table: "official_use",
                newName: "CrashId");

            migrationBuilder.RenameColumn(
                name: "completed_time",
                table: "official_use",
                newName: "CompletedTime");

            migrationBuilder.RenameColumn(
                name: "completed_surname",
                table: "official_use",
                newName: "CompletedSurname");

            migrationBuilder.RenameColumn(
                name: "completed_signature",
                table: "official_use",
                newName: "CompletedSignature");

            migrationBuilder.RenameColumn(
                name: "completed_service_number",
                table: "official_use",
                newName: "CompletedServiceNumber");

            migrationBuilder.RenameColumn(
                name: "completed_rank",
                table: "official_use",
                newName: "CompletedRank");

            migrationBuilder.RenameColumn(
                name: "completed_initials",
                table: "official_use",
                newName: "CompletedInitials");

            migrationBuilder.RenameColumn(
                name: "completed_date",
                table: "official_use",
                newName: "CompletedDate");

            migrationBuilder.RenameColumn(
                name: "completed_by",
                table: "official_use",
                newName: "CompletedBy");

            migrationBuilder.RenameColumn(
                name: "capturing_number",
                table: "official_use",
                newName: "CapturingNumber");

            migrationBuilder.RenameColumn(
                name: "accident_register_no",
                table: "official_use",
                newName: "AccidentRegisterNo");

            migrationBuilder.RenameColumn(
                name: "official_id",
                table: "official_use",
                newName: "OfficialId");

            migrationBuilder.RenameColumn(
                name: "prdp_code",
                table: "drivers_licences",
                newName: "PrdpCode");

            migrationBuilder.RenameColumn(
                name: "person_id",
                table: "drivers_licences",
                newName: "PersonId");

            migrationBuilder.RenameColumn(
                name: "licence_type",
                table: "drivers_licences",
                newName: "LicenceType");

            migrationBuilder.RenameColumn(
                name: "licence_number",
                table: "drivers_licences",
                newName: "LicenceNumber");

            migrationBuilder.RenameColumn(
                name: "licence_code",
                table: "drivers_licences",
                newName: "LicenceCode");

            migrationBuilder.RenameColumn(
                name: "date_of_issue",
                table: "drivers_licences",
                newName: "DateOfIssue");

            migrationBuilder.RenameColumn(
                name: "licence_id",
                table: "drivers_licences",
                newName: "LicenceId");

            migrationBuilder.RenameColumn(
                name: "vehicle_reference",
                table: "dangerous_goods",
                newName: "VehicleReference");

            migrationBuilder.RenameColumn(
                name: "vapour_gas_emission",
                table: "dangerous_goods",
                newName: "VapourGasEmission");

            migrationBuilder.RenameColumn(
                name: "un_number",
                table: "dangerous_goods",
                newName: "UnNumber");

            migrationBuilder.RenameColumn(
                name: "spillage_observed",
                table: "dangerous_goods",
                newName: "SpillageObserved");

            migrationBuilder.RenameColumn(
                name: "placard_displayed",
                table: "dangerous_goods",
                newName: "PlacardDisplayed");

            migrationBuilder.RenameColumn(
                name: "goods_carried",
                table: "dangerous_goods",
                newName: "GoodsCarried");

            migrationBuilder.RenameColumn(
                name: "emergency_services_activated",
                table: "dangerous_goods",
                newName: "EmergencyServicesActivated");

            migrationBuilder.RenameColumn(
                name: "crash_id",
                table: "dangerous_goods",
                newName: "CrashId");

            migrationBuilder.RenameColumn(
                name: "company_name",
                table: "dangerous_goods",
                newName: "CompanyName");

            migrationBuilder.RenameColumn(
                name: "dg_id",
                table: "dangerous_goods",
                newName: "DgId");

            migrationBuilder.RenameIndex(
                name: "IX_dangerous_goods_crash_id",
                table: "dangerous_goods",
                newName: "IX_dangerous_goods_CrashId");

            migrationBuilder.RenameColumn(
                name: "speed_limit_kmh",
                table: "crashes",
                newName: "SpeedLimitKmh");

            migrationBuilder.RenameColumn(
                name: "road_number",
                table: "crashes",
                newName: "RoadNumber");

            migrationBuilder.RenameColumn(
                name: "province_code",
                table: "crashes",
                newName: "ProvinceCode");

            migrationBuilder.RenameColumn(
                name: "no_of_vehicles_involved",
                table: "crashes",
                newName: "NoOfVehiclesInvolved");

            migrationBuilder.RenameColumn(
                name: "no_of_appendices",
                table: "crashes",
                newName: "NoOfAppendices");

            migrationBuilder.RenameColumn(
                name: "km_marker",
                table: "crashes",
                newName: "KmMarker");

            migrationBuilder.RenameColumn(
                name: "incident_report_no",
                table: "crashes",
                newName: "IncidentReportNo");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "crashes",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "crash_time",
                table: "crashes",
                newName: "CrashTime");

            migrationBuilder.RenameColumn(
                name: "crash_date",
                table: "crashes",
                newName: "CrashDate");

            migrationBuilder.RenameColumn(
                name: "cr_no",
                table: "crashes",
                newName: "CrNo");

            migrationBuilder.RenameColumn(
                name: "cas_no",
                table: "crashes",
                newName: "CasNo");

            migrationBuilder.RenameColumn(
                name: "capturing_number",
                table: "crashes",
                newName: "CapturingNumber");

            migrationBuilder.RenameColumn(
                name: "brief_description",
                table: "crashes",
                newName: "BriefDescription");

            migrationBuilder.RenameColumn(
                name: "crash_id",
                table: "crashes",
                newName: "CrashId");

            migrationBuilder.RenameIndex(
                name: "idx_crashes_province",
                table: "crashes",
                newName: "IX_crashes_ProvinceCode");

            migrationBuilder.RenameIndex(
                name: "idx_crashes_date",
                table: "crashes",
                newName: "IX_crashes_CrashDate");

            migrationBuilder.RenameIndex(
                name: "idx_crashes_cas_no",
                table: "crashes",
                newName: "IX_crashes_CasNo");

            migrationBuilder.RenameColumn(
                name: "weather_condition",
                table: "crash_weather",
                newName: "WeatherCondition");

            migrationBuilder.RenameColumn(
                name: "crash_id",
                table: "crash_weather",
                newName: "CrashId");

            migrationBuilder.RenameColumn(
                name: "vehicle_reference",
                table: "crash_vehicles",
                newName: "VehicleReference");

            migrationBuilder.RenameColumn(
                name: "vehicle_manoeuvre",
                table: "crash_vehicles",
                newName: "VehicleManoeuvre");

            migrationBuilder.RenameColumn(
                name: "vehicle_id",
                table: "crash_vehicles",
                newName: "VehicleId");

            migrationBuilder.RenameColumn(
                name: "seatbelt_used",
                table: "crash_vehicles",
                newName: "SeatbeltUsed");

            migrationBuilder.RenameColumn(
                name: "position_before_crash",
                table: "crash_vehicles",
                newName: "PositionBeforeCrash");

            migrationBuilder.RenameColumn(
                name: "passengers_for_reward",
                table: "crash_vehicles",
                newName: "PassengersForReward");

            migrationBuilder.RenameColumn(
                name: "drug_test_result",
                table: "crash_vehicles",
                newName: "DrugTestResult");

            migrationBuilder.RenameColumn(
                name: "drug_suspected",
                table: "crash_vehicles",
                newName: "DrugSuspected");

            migrationBuilder.RenameColumn(
                name: "driver_person_id",
                table: "crash_vehicles",
                newName: "DriverPersonId");

            migrationBuilder.RenameColumn(
                name: "crash_id",
                table: "crash_vehicles",
                newName: "CrashId");

            migrationBuilder.RenameColumn(
                name: "breakdown_company",
                table: "crash_vehicles",
                newName: "BreakdownCompany");

            migrationBuilder.RenameColumn(
                name: "alcohol_test_result",
                table: "crash_vehicles",
                newName: "AlcoholTestResult");

            migrationBuilder.RenameColumn(
                name: "alcohol_suspected",
                table: "crash_vehicles",
                newName: "AlcoholSuspected");

            migrationBuilder.RenameColumn(
                name: "crash_vehicle_id",
                table: "crash_vehicles",
                newName: "CrashVehicleId");

            migrationBuilder.RenameIndex(
                name: "idx_cv_vehicle",
                table: "crash_vehicles",
                newName: "IX_crash_vehicles_VehicleId");

            migrationBuilder.RenameIndex(
                name: "idx_cv_driver",
                table: "crash_vehicles",
                newName: "IX_crash_vehicles_DriverPersonId");

            migrationBuilder.RenameIndex(
                name: "idx_cv_crash",
                table: "crash_vehicles",
                newName: "IX_crash_vehicles_CrashId");

            migrationBuilder.RenameColumn(
                name: "notes",
                table: "crash_sketches",
                newName: "Notes");

            migrationBuilder.RenameColumn(
                name: "sketch_type",
                table: "crash_sketches",
                newName: "SketchType");

            migrationBuilder.RenameColumn(
                name: "north_direction",
                table: "crash_sketches",
                newName: "NorthDirection");

            migrationBuilder.RenameColumn(
                name: "file_path",
                table: "crash_sketches",
                newName: "FilePath");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "crash_sketches",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "crash_id",
                table: "crash_sketches",
                newName: "CrashId");

            migrationBuilder.RenameColumn(
                name: "sketch_id",
                table: "crash_sketches",
                newName: "SketchId");

            migrationBuilder.RenameIndex(
                name: "IX_crash_sketches_crash_id",
                table: "crash_sketches",
                newName: "IX_crash_sketches_CrashId");

            migrationBuilder.RenameColumn(
                name: "role",
                table: "crash_persons",
                newName: "Role");

            migrationBuilder.RenameColumn(
                name: "hospital",
                table: "crash_persons",
                newName: "Hospital");

            migrationBuilder.RenameColumn(
                name: "vehicle_reference",
                table: "crash_persons",
                newName: "VehicleReference");

            migrationBuilder.RenameColumn(
                name: "severity_of_injury",
                table: "crash_persons",
                newName: "SeverityOfInjury");

            migrationBuilder.RenameColumn(
                name: "seating_position",
                table: "crash_persons",
                newName: "SeatingPosition");

            migrationBuilder.RenameColumn(
                name: "seatbelt_helmet_used",
                table: "crash_persons",
                newName: "SeatbeltHelmetUsed");

            migrationBuilder.RenameColumn(
                name: "person_reference",
                table: "crash_persons",
                newName: "PersonReference");

            migrationBuilder.RenameColumn(
                name: "person_id",
                table: "crash_persons",
                newName: "PersonId");

            migrationBuilder.RenameColumn(
                name: "passenger_number",
                table: "crash_persons",
                newName: "PassengerNumber");

            migrationBuilder.RenameColumn(
                name: "liquor_drug_test_done",
                table: "crash_persons",
                newName: "LiquorDrugTestDone");

            migrationBuilder.RenameColumn(
                name: "liquor_drug_suspected",
                table: "crash_persons",
                newName: "LiquorDrugSuspected");

            migrationBuilder.RenameColumn(
                name: "crash_vehicle_id",
                table: "crash_persons",
                newName: "CrashVehicleId");

            migrationBuilder.RenameColumn(
                name: "crash_id",
                table: "crash_persons",
                newName: "CrashId");

            migrationBuilder.RenameColumn(
                name: "child_restraint_used",
                table: "crash_persons",
                newName: "ChildRestraintUsed");

            migrationBuilder.RenameColumn(
                name: "ambulance_service_ref",
                table: "crash_persons",
                newName: "AmbulanceServiceRef");

            migrationBuilder.RenameColumn(
                name: "crash_person_id",
                table: "crash_persons",
                newName: "CrashPersonId");

            migrationBuilder.RenameIndex(
                name: "IX_crash_persons_crash_vehicle_id",
                table: "crash_persons",
                newName: "IX_crash_persons_CrashVehicleId");

            migrationBuilder.RenameIndex(
                name: "idx_cp_person",
                table: "crash_persons",
                newName: "IX_crash_persons_PersonId");

            migrationBuilder.RenameIndex(
                name: "idx_cp_injury",
                table: "crash_persons",
                newName: "IX_crash_persons_SeverityOfInjury");

            migrationBuilder.RenameIndex(
                name: "idx_cp_crash",
                table: "crash_persons",
                newName: "IX_crash_persons_CrashId");

            migrationBuilder.RenameColumn(
                name: "suburb",
                table: "crash_locations",
                newName: "Suburb");

            migrationBuilder.RenameColumn(
                name: "street_road_name",
                table: "crash_locations",
                newName: "StreetRoadName");

            migrationBuilder.RenameColumn(
                name: "road_surface_type",
                table: "crash_locations",
                newName: "RoadSurfaceType");

            migrationBuilder.RenameColumn(
                name: "road_surface_quality",
                table: "crash_locations",
                newName: "RoadSurfaceQuality");

            migrationBuilder.RenameColumn(
                name: "road_surface_condition",
                table: "crash_locations",
                newName: "RoadSurfaceCondition");

            migrationBuilder.RenameColumn(
                name: "road_layout",
                table: "crash_locations",
                newName: "RoadLayout");

            migrationBuilder.RenameColumn(
                name: "road_functional_classification",
                table: "crash_locations",
                newName: "RoadFunctionalClassification");

            migrationBuilder.RenameColumn(
                name: "next_city_town",
                table: "crash_locations",
                newName: "NextCityTown");

            migrationBuilder.RenameColumn(
                name: "km_marker_info",
                table: "crash_locations",
                newName: "KmMarkerInfo");

            migrationBuilder.RenameColumn(
                name: "junction_type",
                table: "crash_locations",
                newName: "JunctionType");

            migrationBuilder.RenameColumn(
                name: "intersection_street",
                table: "crash_locations",
                newName: "IntersectionStreet");

            migrationBuilder.RenameColumn(
                name: "intersection_road_no",
                table: "crash_locations",
                newName: "IntersectionRoadNo");

            migrationBuilder.RenameColumn(
                name: "gps_y_coordinate",
                table: "crash_locations",
                newName: "GpsYCoordinate");

            migrationBuilder.RenameColumn(
                name: "gps_x_coordinate",
                table: "crash_locations",
                newName: "GpsXCoordinate");

            migrationBuilder.RenameColumn(
                name: "from_point",
                table: "crash_locations",
                newName: "FromPoint");

            migrationBuilder.RenameColumn(
                name: "distance_km",
                table: "crash_locations",
                newName: "DistanceKm");

            migrationBuilder.RenameColumn(
                name: "crash_id",
                table: "crash_locations",
                newName: "CrashId");

            migrationBuilder.RenameColumn(
                name: "compass_direction",
                table: "crash_locations",
                newName: "CompassDirection");

            migrationBuilder.RenameColumn(
                name: "city_town",
                table: "crash_locations",
                newName: "CityTown");

            migrationBuilder.RenameColumn(
                name: "built_up_area",
                table: "crash_locations",
                newName: "BuiltUpArea");

            migrationBuilder.RenameColumn(
                name: "between_to",
                table: "crash_locations",
                newName: "BetweenTo");

            migrationBuilder.RenameColumn(
                name: "between_from",
                table: "crash_locations",
                newName: "BetweenFrom");

            migrationBuilder.RenameColumn(
                name: "area_type",
                table: "crash_locations",
                newName: "AreaType");

            migrationBuilder.RenameColumn(
                name: "location_id",
                table: "crash_locations",
                newName: "LocationId");

            migrationBuilder.RenameColumn(
                name: "vehicle_lights_condition",
                table: "crash_conditions",
                newName: "VehicleLightsCondition");

            migrationBuilder.RenameColumn(
                name: "tyre_burst_observed",
                table: "crash_conditions",
                newName: "TyreBurstObserved");

            migrationBuilder.RenameColumn(
                name: "traffic_control_type",
                table: "crash_conditions",
                newName: "TrafficControlType");

            migrationBuilder.RenameColumn(
                name: "road_signs_condition",
                table: "crash_conditions",
                newName: "RoadSignsCondition");

            migrationBuilder.RenameColumn(
                name: "road_segment_grade",
                table: "crash_conditions",
                newName: "RoadSegmentGrade");

            migrationBuilder.RenameColumn(
                name: "road_marking_visibility",
                table: "crash_conditions",
                newName: "RoadMarkingVisibility");

            migrationBuilder.RenameColumn(
                name: "overtaking_control",
                table: "crash_conditions",
                newName: "OvertakingControl");

            migrationBuilder.RenameColumn(
                name: "other_observations",
                table: "crash_conditions",
                newName: "OtherObservations");

            migrationBuilder.RenameColumn(
                name: "obstruction_type",
                table: "crash_conditions",
                newName: "ObstructionType");

            migrationBuilder.RenameColumn(
                name: "light_condition",
                table: "crash_conditions",
                newName: "LightCondition");

            migrationBuilder.RenameColumn(
                name: "hit_and_run",
                table: "crash_conditions",
                newName: "HitAndRun");

            migrationBuilder.RenameColumn(
                name: "crash_type",
                table: "crash_conditions",
                newName: "CrashType");

            migrationBuilder.RenameColumn(
                name: "crash_id",
                table: "crash_conditions",
                newName: "CrashId");

            migrationBuilder.RenameColumn(
                name: "condition_id",
                table: "crash_conditions",
                newName: "ConditionId");

            migrationBuilder.RenameColumn(
                name: "is_major_factor",
                table: "contributory_factors",
                newName: "IsMajorFactor");

            migrationBuilder.RenameColumn(
                name: "factor_description",
                table: "contributory_factors",
                newName: "FactorDescription");

            migrationBuilder.RenameColumn(
                name: "factor_category",
                table: "contributory_factors",
                newName: "FactorCategory");

            migrationBuilder.RenameColumn(
                name: "crash_id",
                table: "contributory_factors",
                newName: "CrashId");

            migrationBuilder.RenameColumn(
                name: "factor_id",
                table: "contributory_factors",
                newName: "FactorId");

            migrationBuilder.RenameIndex(
                name: "idx_cf_crash",
                table: "contributory_factors",
                newName: "IX_contributory_factors_CrashId");

            migrationBuilder.RenameIndex(
                name: "idx_cf_category",
                table: "contributory_factors",
                newName: "IX_contributory_factors_FactorCategory");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "vehicles",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "(getdate())");

            migrationBuilder.AlterColumn<string>(
                name: "CountryOfRegistration",
                table: "vehicles",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldDefaultValue: "RSA");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "persons",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "(getdate())");

            migrationBuilder.AlterColumn<string>(
                name: "VehicleReference",
                table: "dangerous_goods",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nchar(2)",
                oldFixedLength: true,
                oldMaxLength: 2);

            migrationBuilder.AlterColumn<byte>(
                name: "NoOfVehiclesInvolved",
                table: "crashes",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0,
                oldClrType: typeof(byte),
                oldType: "tinyint",
                oldNullable: true,
                oldDefaultValue: (byte)1);

            migrationBuilder.AlterColumn<byte>(
                name: "NoOfAppendices",
                table: "crashes",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0,
                oldClrType: typeof(byte),
                oldType: "tinyint",
                oldNullable: true,
                oldDefaultValue: (byte)0);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "crashes",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "(getdate())");

            migrationBuilder.AlterColumn<string>(
                name: "VehicleReference",
                table: "crash_vehicles",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nchar(2)",
                oldFixedLength: true,
                oldMaxLength: 2);

            migrationBuilder.AlterColumn<string>(
                name: "SketchType",
                table: "crash_sketches",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "accident");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "crash_sketches",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "(getdate())");

            migrationBuilder.AlterColumn<string>(
                name: "VehicleReference",
                table: "crash_persons",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nchar(2)",
                oldFixedLength: true,
                oldMaxLength: 2,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_pedestrian_bicyclist_details_CrashPersonId",
                table: "pedestrian_bicyclist_details",
                column: "CrashPersonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_official_use_CrashId",
                table: "official_use",
                column: "CrashId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_drivers_licences_PersonId",
                table: "drivers_licences",
                column: "PersonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_crash_locations_CrashId",
                table: "crash_locations",
                column: "CrashId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_crash_conditions_CrashId",
                table: "crash_conditions",
                column: "CrashId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_contributory_factors_crashes_CrashId",
                table: "contributory_factors",
                column: "CrashId",
                principalTable: "crashes",
                principalColumn: "CrashId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_crash_conditions_crashes_CrashId",
                table: "crash_conditions",
                column: "CrashId",
                principalTable: "crashes",
                principalColumn: "CrashId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_crash_locations_crashes_CrashId",
                table: "crash_locations",
                column: "CrashId",
                principalTable: "crashes",
                principalColumn: "CrashId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_crash_persons_crash_vehicles_CrashVehicleId",
                table: "crash_persons",
                column: "CrashVehicleId",
                principalTable: "crash_vehicles",
                principalColumn: "CrashVehicleId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_crash_persons_crashes_CrashId",
                table: "crash_persons",
                column: "CrashId",
                principalTable: "crashes",
                principalColumn: "CrashId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_crash_persons_persons_PersonId",
                table: "crash_persons",
                column: "PersonId",
                principalTable: "persons",
                principalColumn: "PersonId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_crash_sketches_crashes_CrashId",
                table: "crash_sketches",
                column: "CrashId",
                principalTable: "crashes",
                principalColumn: "CrashId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_crash_vehicles_crashes_CrashId",
                table: "crash_vehicles",
                column: "CrashId",
                principalTable: "crashes",
                principalColumn: "CrashId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_crash_vehicles_persons_DriverPersonId",
                table: "crash_vehicles",
                column: "DriverPersonId",
                principalTable: "persons",
                principalColumn: "PersonId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_crash_vehicles_vehicles_VehicleId",
                table: "crash_vehicles",
                column: "VehicleId",
                principalTable: "vehicles",
                principalColumn: "VehicleId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_crash_weather_crashes_CrashId",
                table: "crash_weather",
                column: "CrashId",
                principalTable: "crashes",
                principalColumn: "CrashId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_dangerous_goods_crashes_CrashId",
                table: "dangerous_goods",
                column: "CrashId",
                principalTable: "crashes",
                principalColumn: "CrashId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_drivers_licences_persons_PersonId",
                table: "drivers_licences",
                column: "PersonId",
                principalTable: "persons",
                principalColumn: "PersonId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_official_use_crashes_CrashId",
                table: "official_use",
                column: "CrashId",
                principalTable: "crashes",
                principalColumn: "CrashId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_pedestrian_bicyclist_details_crash_persons_CrashPersonId",
                table: "pedestrian_bicyclist_details",
                column: "CrashPersonId",
                principalTable: "crash_persons",
                principalColumn: "CrashPersonId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_vehicle_damage_crash_vehicles_CrashVehicleId",
                table: "vehicle_damage",
                column: "CrashVehicleId",
                principalTable: "crash_vehicles",
                principalColumn: "CrashVehicleId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_witnesses_crashes_CrashId",
                table: "witnesses",
                column: "CrashId",
                principalTable: "crashes",
                principalColumn: "CrashId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
