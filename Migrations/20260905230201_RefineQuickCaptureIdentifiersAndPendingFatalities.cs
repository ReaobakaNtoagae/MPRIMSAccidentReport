using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrashReport.Migrations
{
    /// <inheritdoc />
    public partial class RefineQuickCaptureIdentifiersAndPendingFatalities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Some existing databases predate this model index even though the snapshot
            // contains it. A conditional drop makes the migration safe for both shapes.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.indexes
                           WHERE name = N'IX_crash_summaries_cr_no_source_file'
                             AND object_id = OBJECT_ID(N'[crash_summaries]'))
                    DROP INDEX [IX_crash_summaries_cr_no_source_file] ON [crash_summaries];
                """);

            migrationBuilder.AlterColumn<string>(
                name: "role",
                table: "crash_summary_injuries",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<int>(
                name: "fatalities_total",
                table: "crash_summaries",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_crash_summaries_station_cas_no",
                table: "crash_summaries",
                columns: new[] { "station", "cas_no" },
                unique: true,
                filter: "[cas_no] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_crash_summaries_station_cr_no",
                table: "crash_summaries",
                columns: new[] { "station", "cr_no" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_crash_summaries_station_cas_no",
                table: "crash_summaries");

            migrationBuilder.DropIndex(
                name: "IX_crash_summaries_station_cr_no",
                table: "crash_summaries");

            migrationBuilder.DropColumn(
                name: "fatalities_total",
                table: "crash_summaries");

            migrationBuilder.AlterColumn<string>(
                name: "role",
                table: "crash_summary_injuries",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_crash_summaries_cr_no_source_file",
                table: "crash_summaries",
                columns: new[] { "cr_no", "source_file" },
                unique: true,
                filter: "[source_file] IS NOT NULL");
        }
    }
}
