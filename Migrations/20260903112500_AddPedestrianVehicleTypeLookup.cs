using CrashReport.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrashReport.Migrations;

/// <summary>
/// Adds the P/D abbreviation used by the monthly workbooks to the controlled
/// vehicle-type lookup. The guard makes this safe where an administrator has
/// already created the same code manually.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260903112500_AddPedestrianVehicleTypeLookup")]
public sealed class AddPedestrianVehicleTypeLookup : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF NOT EXISTS (
                SELECT 1 FROM dbo.lkp_vehicle_types WHERE vehicle_type_code = N'P/D'
            )
            BEGIN
                INSERT INTO dbo.lkp_vehicle_types
                    (vehicle_type_code, description, full_name, is_active, created_at)
                VALUES
                    (N'P/D', N'Pedestrian', N'Pedestrian', 1, GETDATE());
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Do not remove controlled lookup data after production rows reference it.
        migrationBuilder.Sql("""
            IF NOT EXISTS (
                SELECT 1 FROM dbo.crash_summary_vehicles WHERE vehicle_type_code = N'P/D'
            )
            AND NOT EXISTS (
                SELECT 1 FROM dbo.vehicles WHERE vehicle_type_code = N'P/D'
            )
            BEGIN
                DELETE FROM dbo.lkp_vehicle_types WHERE vehicle_type_code = N'P/D';
            END
            """);
    }
}
