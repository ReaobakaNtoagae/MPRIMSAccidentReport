using CrashReport.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrashReport.Migrations;

/// <summary>
/// Adds a controlled fallback for rows that report a vehicle count without giving
/// the vehicle description. This preserves the incomplete source data without
/// inventing a more specific type or violating the vehicle-type foreign key.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260903114000_AddUnknownVehicleTypeLookup")]
public sealed class AddUnknownVehicleTypeLookup : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The conditional insert keeps the migration safe if Lookup Data was used
        // to add UNKNOWN manually before this migration is deployed.
        migrationBuilder.Sql("""
            IF NOT EXISTS (
                SELECT 1 FROM dbo.lkp_vehicle_types WHERE vehicle_type_code = N'UNKNOWN'
            )
            BEGIN
                INSERT INTO dbo.lkp_vehicle_types
                    (vehicle_type_code, description, full_name, is_active, created_at)
                VALUES
                    (N'UNKNOWN', N'Unknown or unspecified', N'Unknown/unspecified vehicle', 1, GETDATE());
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Never delete the lookup during rollback when imported records reference it.
        migrationBuilder.Sql("""
            IF NOT EXISTS (
                SELECT 1 FROM dbo.crash_summary_vehicles WHERE vehicle_type_code = N'UNKNOWN'
            )
            AND NOT EXISTS (
                SELECT 1 FROM dbo.vehicles WHERE vehicle_type_code = N'UNKNOWN'
            )
            BEGIN
                DELETE FROM dbo.lkp_vehicle_types WHERE vehicle_type_code = N'UNKNOWN';
            END
            """);
    }
}
