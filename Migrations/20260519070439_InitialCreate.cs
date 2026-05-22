using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrashReport.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "crashes",
                columns: table => new
                {
                    CrashId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CasNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CrNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    IncidentReportNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CapturingNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CrashDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CrashTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    NoOfAppendices = table.Column<byte>(type: "tinyint", nullable: false),
                    NoOfVehiclesInvolved = table.Column<byte>(type: "tinyint", nullable: false),
                    ProvinceCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    SpeedLimitKmh = table.Column<short>(type: "smallint", nullable: true),
                    RoadNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    KmMarker = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    BriefDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crashes", x => x.CrashId);
                });

            migrationBuilder.CreateTable(
                name: "persons",
                columns: table => new
                {
                    PersonId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IdNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Age = table.Column<byte>(type: "tinyint", nullable: true),
                    Surname = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FullNames = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CountryOfOrigin = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Nationality = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PopulationGroup = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Gender = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    HomeAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CellPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    OtherPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    WorkContactAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_persons", x => x.PersonId);
                });

            migrationBuilder.CreateTable(
                name: "vehicles",
                columns: table => new
                {
                    VehicleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CountryOfRegistration = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    LicenceDiscNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Colour = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Make = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VinNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TrailerLicenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    VehicleCategory = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    VehicleTypeCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    SpecialFunction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PrivateOrBusiness = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    LicenceTypeFitting = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicles", x => x.VehicleId);
                });

            migrationBuilder.CreateTable(
                name: "contributory_factors",
                columns: table => new
                {
                    FactorId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CrashId = table.Column<int>(type: "int", nullable: false),
                    FactorCategory = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FactorDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsMajorFactor = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contributory_factors", x => x.FactorId);
                    table.ForeignKey(
                        name: "FK_contributory_factors_crashes_CrashId",
                        column: x => x.CrashId,
                        principalTable: "crashes",
                        principalColumn: "CrashId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "crash_conditions",
                columns: table => new
                {
                    ConditionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CrashId = table.Column<int>(type: "int", nullable: false),
                    LightCondition = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ObstructionType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    TrafficControlType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RoadSignsCondition = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RoadMarkingVisibility = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    OvertakingControl = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RoadSegmentGrade = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CrashType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    HitAndRun = table.Column<bool>(type: "bit", nullable: true),
                    TyreBurstObserved = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    VehicleLightsCondition = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    OtherObservations = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crash_conditions", x => x.ConditionId);
                    table.ForeignKey(
                        name: "FK_crash_conditions_crashes_CrashId",
                        column: x => x.CrashId,
                        principalTable: "crashes",
                        principalColumn: "CrashId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "crash_locations",
                columns: table => new
                {
                    LocationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CrashId = table.Column<int>(type: "int", nullable: false),
                    BuiltUpArea = table.Column<bool>(type: "bit", nullable: true),
                    AreaType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    StreetRoadName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GpsXCoordinate = table.Column<decimal>(type: "decimal(10,7)", nullable: true),
                    GpsYCoordinate = table.Column<decimal>(type: "decimal(10,7)", nullable: true),
                    IntersectionStreet = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IntersectionRoadNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BetweenFrom = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BetweenTo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Suburb = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CityTown = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DistanceKm = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    CompassDirection = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    FromPoint = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    KmMarkerInfo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NextCityTown = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RoadFunctionalClassification = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    JunctionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RoadLayout = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RoadSurfaceType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RoadSurfaceQuality = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RoadSurfaceCondition = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crash_locations", x => x.LocationId);
                    table.ForeignKey(
                        name: "FK_crash_locations_crashes_CrashId",
                        column: x => x.CrashId,
                        principalTable: "crashes",
                        principalColumn: "CrashId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "crash_sketches",
                columns: table => new
                {
                    SketchId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CrashId = table.Column<int>(type: "int", nullable: false),
                    SketchType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NorthDirection = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crash_sketches", x => x.SketchId);
                    table.ForeignKey(
                        name: "FK_crash_sketches_crashes_CrashId",
                        column: x => x.CrashId,
                        principalTable: "crashes",
                        principalColumn: "CrashId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "crash_weather",
                columns: table => new
                {
                    CrashId = table.Column<int>(type: "int", nullable: false),
                    WeatherCondition = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crash_weather", x => new { x.CrashId, x.WeatherCondition });
                    table.ForeignKey(
                        name: "FK_crash_weather_crashes_CrashId",
                        column: x => x.CrashId,
                        principalTable: "crashes",
                        principalColumn: "CrashId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dangerous_goods",
                columns: table => new
                {
                    DgId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CrashId = table.Column<int>(type: "int", nullable: false),
                    VehicleReference = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    GoodsCarried = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    SpillageObserved = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    VapourGasEmission = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PlacardDisplayed = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UnNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EmergencyServicesActivated = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dangerous_goods", x => x.DgId);
                    table.ForeignKey(
                        name: "FK_dangerous_goods_crashes_CrashId",
                        column: x => x.CrashId,
                        principalTable: "crashes",
                        principalColumn: "CrashId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "official_use",
                columns: table => new
                {
                    OfficialId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CrashId = table.Column<int>(type: "int", nullable: false),
                    OfficeWhereOccurred = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OccurrenceBookNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AccidentRegisterNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SapsCasNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DepartmentNameOccurred = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DateStamp = table.Column<DateOnly>(type: "date", nullable: true),
                    InspectedByInitials = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    InspectedByRank = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InspectedBySurname = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InspectedByServiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InspectedBySignature = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OfficeWhereReported = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DepartmentNameReported = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CompletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CompletedInitials = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CompletedRank = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CompletedSurname = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CompletedServiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CompletedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CompletedTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    CompletedSignature = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CapturingNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_official_use", x => x.OfficialId);
                    table.ForeignKey(
                        name: "FK_official_use_crashes_CrashId",
                        column: x => x.CrashId,
                        principalTable: "crashes",
                        principalColumn: "CrashId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "witnesses",
                columns: table => new
                {
                    WitnessId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CrashId = table.Column<int>(type: "int", nullable: false),
                    SurnameInitials = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IdType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IdNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    WorkContactAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CellPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    OtherPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_witnesses", x => x.WitnessId);
                    table.ForeignKey(
                        name: "FK_witnesses_crashes_CrashId",
                        column: x => x.CrashId,
                        principalTable: "crashes",
                        principalColumn: "CrashId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "drivers_licences",
                columns: table => new
                {
                    LicenceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    LicenceType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    LicenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LicenceCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DateOfIssue = table.Column<DateOnly>(type: "date", nullable: true),
                    PrdpCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_drivers_licences", x => x.LicenceId);
                    table.ForeignKey(
                        name: "FK_drivers_licences_persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "persons",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "crash_vehicles",
                columns: table => new
                {
                    CrashVehicleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CrashId = table.Column<int>(type: "int", nullable: false),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    DriverPersonId = table.Column<int>(type: "int", nullable: true),
                    VehicleReference = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    SeatbeltUsed = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    AlcoholSuspected = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    AlcoholTestResult = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DrugSuspected = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DrugTestResult = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    VehicleManoeuvre = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PositionBeforeCrash = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    PassengersForReward = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    BreakdownCompany = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crash_vehicles", x => x.CrashVehicleId);
                    table.ForeignKey(
                        name: "FK_crash_vehicles_crashes_CrashId",
                        column: x => x.CrashId,
                        principalTable: "crashes",
                        principalColumn: "CrashId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_crash_vehicles_persons_DriverPersonId",
                        column: x => x.DriverPersonId,
                        principalTable: "persons",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_crash_vehicles_vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "vehicles",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "crash_persons",
                columns: table => new
                {
                    CrashPersonId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CrashId = table.Column<int>(type: "int", nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    CrashVehicleId = table.Column<int>(type: "int", nullable: true),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VehicleReference = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    PersonReference = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    PassengerNumber = table.Column<byte>(type: "tinyint", nullable: true),
                    SeatingPosition = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SeverityOfInjury = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SeatbeltHelmetUsed = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ChildRestraintUsed = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LiquorDrugSuspected = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    LiquorDrugTestDone = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    AmbulanceServiceRef = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Hospital = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crash_persons", x => x.CrashPersonId);
                    table.ForeignKey(
                        name: "FK_crash_persons_crash_vehicles_CrashVehicleId",
                        column: x => x.CrashVehicleId,
                        principalTable: "crash_vehicles",
                        principalColumn: "CrashVehicleId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_crash_persons_crashes_CrashId",
                        column: x => x.CrashId,
                        principalTable: "crashes",
                        principalColumn: "CrashId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_crash_persons_persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "persons",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_damage",
                columns: table => new
                {
                    DamageId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CrashVehicleId = table.Column<int>(type: "int", nullable: false),
                    DamagePoint = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_damage", x => x.DamageId);
                    table.ForeignKey(
                        name: "FK_vehicle_damage_crash_vehicles_CrashVehicleId",
                        column: x => x.CrashVehicleId,
                        principalTable: "crash_vehicles",
                        principalColumn: "CrashVehicleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pedestrian_bicyclist_details",
                columns: table => new
                {
                    DetailId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CrashPersonId = table.Column<int>(type: "int", nullable: false),
                    PositionOnRoad = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    LocationReCrossing = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Manoeuvre = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    PedestrianAction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ClothingColour = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pedestrian_bicyclist_details", x => x.DetailId);
                    table.ForeignKey(
                        name: "FK_pedestrian_bicyclist_details_crash_persons_CrashPersonId",
                        column: x => x.CrashPersonId,
                        principalTable: "crash_persons",
                        principalColumn: "CrashPersonId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contributory_factors_CrashId",
                table: "contributory_factors",
                column: "CrashId");

            migrationBuilder.CreateIndex(
                name: "IX_contributory_factors_FactorCategory",
                table: "contributory_factors",
                column: "FactorCategory");

            migrationBuilder.CreateIndex(
                name: "IX_crash_conditions_CrashId",
                table: "crash_conditions",
                column: "CrashId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_crash_locations_CrashId",
                table: "crash_locations",
                column: "CrashId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_crash_persons_CrashId",
                table: "crash_persons",
                column: "CrashId");

            migrationBuilder.CreateIndex(
                name: "IX_crash_persons_CrashVehicleId",
                table: "crash_persons",
                column: "CrashVehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_crash_persons_PersonId",
                table: "crash_persons",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_crash_persons_SeverityOfInjury",
                table: "crash_persons",
                column: "SeverityOfInjury");

            migrationBuilder.CreateIndex(
                name: "IX_crash_sketches_CrashId",
                table: "crash_sketches",
                column: "CrashId");

            migrationBuilder.CreateIndex(
                name: "IX_crash_vehicles_CrashId",
                table: "crash_vehicles",
                column: "CrashId");

            migrationBuilder.CreateIndex(
                name: "IX_crash_vehicles_DriverPersonId",
                table: "crash_vehicles",
                column: "DriverPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_crash_vehicles_VehicleId",
                table: "crash_vehicles",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_crashes_CasNo",
                table: "crashes",
                column: "CasNo");

            migrationBuilder.CreateIndex(
                name: "IX_crashes_CrashDate",
                table: "crashes",
                column: "CrashDate");

            migrationBuilder.CreateIndex(
                name: "IX_crashes_ProvinceCode",
                table: "crashes",
                column: "ProvinceCode");

            migrationBuilder.CreateIndex(
                name: "IX_dangerous_goods_CrashId",
                table: "dangerous_goods",
                column: "CrashId");

            migrationBuilder.CreateIndex(
                name: "IX_drivers_licences_PersonId",
                table: "drivers_licences",
                column: "PersonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_official_use_CrashId",
                table: "official_use",
                column: "CrashId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pedestrian_bicyclist_details_CrashPersonId",
                table: "pedestrian_bicyclist_details",
                column: "CrashPersonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_persons_IdNumber",
                table: "persons",
                column: "IdNumber");

            migrationBuilder.CreateIndex(
                name: "IX_persons_Surname",
                table: "persons",
                column: "Surname");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_damage_CrashVehicleId",
                table: "vehicle_damage",
                column: "CrashVehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_LicenceDiscNumber",
                table: "vehicles",
                column: "LicenceDiscNumber");

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_VinNumber",
                table: "vehicles",
                column: "VinNumber");

            migrationBuilder.CreateIndex(
                name: "IX_witnesses_CrashId",
                table: "witnesses",
                column: "CrashId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contributory_factors");

            migrationBuilder.DropTable(
                name: "crash_conditions");

            migrationBuilder.DropTable(
                name: "crash_locations");

            migrationBuilder.DropTable(
                name: "crash_sketches");

            migrationBuilder.DropTable(
                name: "crash_weather");

            migrationBuilder.DropTable(
                name: "dangerous_goods");

            migrationBuilder.DropTable(
                name: "drivers_licences");

            migrationBuilder.DropTable(
                name: "official_use");

            migrationBuilder.DropTable(
                name: "pedestrian_bicyclist_details");

            migrationBuilder.DropTable(
                name: "vehicle_damage");

            migrationBuilder.DropTable(
                name: "witnesses");

            migrationBuilder.DropTable(
                name: "crash_persons");

            migrationBuilder.DropTable(
                name: "crash_vehicles");

            migrationBuilder.DropTable(
                name: "crashes");

            migrationBuilder.DropTable(
                name: "persons");

            migrationBuilder.DropTable(
                name: "vehicles");
        }
    }
}
