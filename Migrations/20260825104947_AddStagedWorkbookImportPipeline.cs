using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrashReport.Migrations
{
    /// <inheritdoc />
    public partial class AddStagedWorkbookImportPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The database already contains several older model changes that were created
            // outside EF migrations. Only genuinely missing objects are created here.
            migrationBuilder.CreateTable(
                name: "crash_fatalities",
                columns: table => new
                {
                    fatality_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    summary_id = table.Column<int>(type: "int", nullable: false),
                    age = table.Column<byte>(type: "tinyint", nullable: false),
                    gender = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    race = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crash_fatalities", x => x.fatality_id);
                    table.ForeignKey(
                        name: "FK_crash_fatalities_crash_summaries_summary_id",
                        column: x => x.summary_id,
                        principalTable: "crash_summaries",
                        principalColumn: "summary_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "import_batches",
                columns: table => new
                {
                    import_batch_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    original_file_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    stored_file_reference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    file_sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    selected_region = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    detected_template = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    template_detection_confidence = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    reporting_month = table.Column<byte>(type: "tinyint", nullable: false),
                    reporting_year = table.Column<short>(type: "smallint", nullable: false),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    uploaded_by_user_id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    reviewed_by_user_id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    imported_by_user_id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    imported_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    failure_reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_batches", x => x.import_batch_id);
                    table.ForeignKey(
                        name: "FK_import_batches_AspNetUsers_uploaded_by_user_id",
                        column: x => x.uploaded_by_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staging_crash_summaries",
                columns: table => new
                {
                    staging_summary_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    import_batch_id = table.Column<int>(type: "int", nullable: false),
                    worksheet_name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    source_row_number = table.Column<int>(type: "int", nullable: false),
                    raw_row_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    original_station = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    station = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    original_ar_number = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ar_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    original_cas_number = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    cas_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    original_date = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    crash_date = table.Column<DateOnly>(type: "date", nullable: true),
                    original_day = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    calculated_day = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    original_time = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    crash_time = table.Column<TimeOnly>(type: "time", nullable: true),
                    original_route = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    route = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    original_location = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    location = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    original_crash_type = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    crash_type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    original_vehicles = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    vehicles_string = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    vehicle_count = table.Column<byte>(type: "tinyint", nullable: true),
                    fatal_drivers = table.Column<byte>(type: "tinyint", nullable: true),
                    fatal_passengers = table.Column<byte>(type: "tinyint", nullable: true),
                    fatal_pedestrians = table.Column<byte>(type: "tinyint", nullable: true),
                    fatal_cyclists = table.Column<byte>(type: "tinyint", nullable: true),
                    fatal_male = table.Column<byte>(type: "tinyint", nullable: true),
                    fatal_female = table.Column<byte>(type: "tinyint", nullable: true),
                    serious_drivers = table.Column<byte>(type: "tinyint", nullable: true),
                    serious_passengers = table.Column<byte>(type: "tinyint", nullable: true),
                    serious_pedestrians = table.Column<byte>(type: "tinyint", nullable: true),
                    serious_cyclists = table.Column<byte>(type: "tinyint", nullable: true),
                    slight_drivers = table.Column<byte>(type: "tinyint", nullable: true),
                    slight_passengers = table.Column<byte>(type: "tinyint", nullable: true),
                    slight_pedestrians = table.Column<byte>(type: "tinyint", nullable: true),
                    slight_cyclists = table.Column<byte>(type: "tinyint", nullable: true),
                    validation_status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    duplicate_status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    review_status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    import_status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    review_notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    reviewed_by_user_id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    production_summary_id = table.Column<int>(type: "int", nullable: true),
                    imported_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staging_crash_summaries", x => x.staging_summary_id);
                    table.ForeignKey(
                        name: "FK_staging_crash_summaries_crash_summaries_production_summary_id",
                        column: x => x.production_summary_id,
                        principalTable: "crash_summaries",
                        principalColumn: "summary_id");
                    table.ForeignKey(
                        name: "FK_staging_crash_summaries_import_batches_import_batch_id",
                        column: x => x.import_batch_id,
                        principalTable: "import_batches",
                        principalColumn: "import_batch_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "staging_import_demographics",
                columns: table => new
                {
                    staging_demographics_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    import_batch_id = table.Column<int>(type: "int", nullable: false),
                    worksheet_name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    raw_section_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    period_from = table.Column<DateOnly>(type: "date", nullable: false),
                    period_to = table.Column<DateOnly>(type: "date", nullable: false),
                    province_code = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    region = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    age_0_7 = table.Column<int>(type: "int", nullable: true),
                    age_8_12 = table.Column<int>(type: "int", nullable: true),
                    age_13_18 = table.Column<int>(type: "int", nullable: true),
                    age_19_35 = table.Column<int>(type: "int", nullable: true),
                    age_36_plus = table.Column<int>(type: "int", nullable: true),
                    driver_male = table.Column<int>(type: "int", nullable: true),
                    driver_female = table.Column<int>(type: "int", nullable: true),
                    passenger_male = table.Column<int>(type: "int", nullable: true),
                    passenger_female = table.Column<int>(type: "int", nullable: true),
                    pedestrian_male = table.Column<int>(type: "int", nullable: true),
                    pedestrian_female = table.Column<int>(type: "int", nullable: true),
                    cyclist_male = table.Column<int>(type: "int", nullable: true),
                    cyclist_female = table.Column<int>(type: "int", nullable: true),
                    race_black = table.Column<int>(type: "int", nullable: true),
                    race_coloured = table.Column<int>(type: "int", nullable: true),
                    race_white = table.Column<int>(type: "int", nullable: true),
                    race_indian = table.Column<int>(type: "int", nullable: true),
                    race_other = table.Column<int>(type: "int", nullable: true),
                    validation_status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    production_demographics_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staging_import_demographics", x => x.staging_demographics_id);
                    table.ForeignKey(
                        name: "FK_staging_import_demographics_crash_demographics_production_demographics_id",
                        column: x => x.production_demographics_id,
                        principalTable: "crash_demographics",
                        principalColumn: "demo_id");
                    table.ForeignKey(
                        name: "FK_staging_import_demographics_import_batches_import_batch_id",
                        column: x => x.import_batch_id,
                        principalTable: "import_batches",
                        principalColumn: "import_batch_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "import_data_quality_issues",
                columns: table => new
                {
                    issue_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    import_batch_id = table.Column<int>(type: "int", nullable: false),
                    staging_summary_id = table.Column<long>(type: "bigint", nullable: true),
                    field_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    issue_code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    is_blocking = table.Column<bool>(type: "bit", nullable: false),
                    description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    original_value = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    suggested_value = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    resolution_status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    resolution_notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    resolved_by_user_id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    resolved_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_data_quality_issues", x => x.issue_id);
                    table.ForeignKey(
                        name: "FK_import_data_quality_issues_import_batches_import_batch_id",
                        column: x => x.import_batch_id,
                        principalTable: "import_batches",
                        principalColumn: "import_batch_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_import_data_quality_issues_staging_crash_summaries_staging_summary_id",
                        column: x => x.staging_summary_id,
                        principalTable: "staging_crash_summaries",
                        principalColumn: "staging_summary_id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_crash_fatalities_summary_id",
                table: "crash_fatalities",
                column: "summary_id");

            migrationBuilder.CreateIndex(
                name: "IX_import_batches_uploaded_by_user_id",
                table: "import_batches",
                column: "uploaded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_import_batches_fingerprint",
                table: "import_batches",
                columns: new[] { "file_sha256", "selected_region", "reporting_year", "reporting_month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_import_batches_status",
                table: "import_batches",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_import_issue_resolution",
                table: "import_data_quality_issues",
                columns: new[] { "import_batch_id", "resolution_status", "is_blocking" });

            migrationBuilder.CreateIndex(
                name: "IX_import_data_quality_issues_staging_summary_id",
                table: "import_data_quality_issues",
                column: "staging_summary_id");

            migrationBuilder.CreateIndex(
                name: "idx_staging_crash_incident",
                table: "staging_crash_summaries",
                columns: new[] { "station", "crash_date", "crash_time" });

            migrationBuilder.CreateIndex(
                name: "idx_staging_crash_review",
                table: "staging_crash_summaries",
                columns: new[] { "import_batch_id", "validation_status", "review_status" });

            migrationBuilder.CreateIndex(
                name: "idx_staging_crash_station_ar",
                table: "staging_crash_summaries",
                columns: new[] { "station", "ar_number" });

            migrationBuilder.CreateIndex(
                name: "IX_staging_crash_summaries_production_summary_id",
                table: "staging_crash_summaries",
                column: "production_summary_id");

            migrationBuilder.CreateIndex(
                name: "ux_staging_crash_source_row",
                table: "staging_crash_summaries",
                columns: new[] { "import_batch_id", "worksheet_name", "source_row_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staging_import_demographics_production_demographics_id",
                table: "staging_import_demographics",
                column: "production_demographics_id");

            migrationBuilder.CreateIndex(
                name: "ux_staging_demographics_batch_sheet",
                table: "staging_import_demographics",
                columns: new[] { "import_batch_id", "worksheet_name" },
                unique: true);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "crash_fatalities");

            migrationBuilder.DropTable(
                name: "import_data_quality_issues");

            migrationBuilder.DropTable(
                name: "staging_import_demographics");

            migrationBuilder.DropTable(
                name: "staging_crash_summaries");

            migrationBuilder.DropTable(
                name: "import_batches");

        }
    }
}
