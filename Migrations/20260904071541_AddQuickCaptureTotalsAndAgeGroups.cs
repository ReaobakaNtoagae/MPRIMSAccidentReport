using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrashReport.Migrations
{
    /// <inheritdoc />
    public partial class AddQuickCaptureTotalsAndAgeGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "age_group_code",
                table: "crash_summary_injuries",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "serious_injuries_total",
                table: "crash_summaries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "slight_injuries_total",
                table: "crash_summaries",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "age_group_code",
                table: "crash_summary_injuries");

            migrationBuilder.DropColumn(
                name: "serious_injuries_total",
                table: "crash_summaries");

            migrationBuilder.DropColumn(
                name: "slight_injuries_total",
                table: "crash_summaries");
        }
    }
}
