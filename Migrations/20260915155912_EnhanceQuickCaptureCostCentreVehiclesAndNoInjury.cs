using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrashReport.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceQuickCaptureCostCentreVehiclesAndNoInjury : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "make",
                table: "crash_summary_vehicles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cost_centre_id",
                table: "crash_summaries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "no_injuries_total",
                table: "crash_summaries",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // lkp_cost_centres is an existing operational lookup table. This
            // migration only connects crash_summaries to it; it must not recreate it.

            migrationBuilder.CreateIndex(
                name: "IX_crash_summaries_cost_centre_id",
                table: "crash_summaries",
                column: "cost_centre_id");

            migrationBuilder.AddForeignKey(
                name: "fk_crash_summary_cost_centre",
                table: "crash_summaries",
                column: "cost_centre_id",
                principalTable: "lkp_cost_centres",
                principalColumn: "cost_centre_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_crash_summary_cost_centre",
                table: "crash_summaries");

            migrationBuilder.DropIndex(
                name: "IX_crash_summaries_cost_centre_id",
                table: "crash_summaries");

            migrationBuilder.DropColumn(
                name: "make",
                table: "crash_summary_vehicles");

            migrationBuilder.DropColumn(
                name: "cost_centre_id",
                table: "crash_summaries");

            migrationBuilder.DropColumn(
                name: "no_injuries_total",
                table: "crash_summaries");
        }
    }
}
