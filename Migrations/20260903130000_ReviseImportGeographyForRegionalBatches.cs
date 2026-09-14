using CrashReport.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrashReport.Migrations;

/// <summary>
/// Revises geography synchronisation for workbooks such as Ehlanzeni that span
/// multiple operational districts, and permits regional demographic summaries.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260903130000_ReviseImportGeographyForRegionalBatches")]
public sealed class ReviseImportGeographyForRegionalBatches : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // A regional summary cannot truthfully reference one district. Existing
        // district-level rows retain their IDs; only the constraint becomes optional.
        migrationBuilder.Sql("""
            IF EXISTS (
                SELECT 1
                FROM sys.columns
                WHERE object_id = OBJECT_ID(N'dbo.crash_demographics')
                  AND name = N'district_id'
                  AND is_nullable = 0
            )
                ALTER TABLE dbo.crash_demographics ALTER COLUMN district_id int NULL;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER PROCEDURE dbo.usp_SyncImportGeography
                @ImportBatchId int,
                @DistrictId int = NULL
            AS
            BEGIN
                SET NOCOUNT ON;
                SET XACT_ABORT ON;

                DECLARE @ProvinceCode nvarchar(5);

                IF NOT EXISTS (
                    SELECT 1 FROM dbo.import_batches WHERE import_batch_id = @ImportBatchId
                )
                    THROW 51000, 'The requested import batch does not exist.', 1;

                IF @DistrictId IS NOT NULL AND NOT EXISTS (
                    SELECT 1 FROM dbo.lkp_district
                    WHERE district_id = @DistrictId AND is_active = 1
                )
                    THROW 51002, 'The supplied district is not an active lookup district.', 1;

                -- Preserve the geographic work already captured in the legacy district
                -- text column by converting it to the proper district foreign key.
                UPDATE station
                SET station.district_id = district.district_id,
                    station.province_code = COALESCE(station.province_code, district.province_code)
                FROM dbo.lkp_saps_stations AS station
                INNER JOIN dbo.lkp_district AS district
                    ON UPPER(LTRIM(RTRIM(station.district))) =
                       UPPER(LTRIM(RTRIM(district.district_name)))
                WHERE station.district_id IS NULL
                  AND district.is_active = 1;

                SELECT @ProvinceCode = CASE
                    WHEN @DistrictId IS NOT NULL THEN district.province_code
                    ELSE batch.selected_region
                END
                FROM dbo.import_batches AS batch
                LEFT JOIN dbo.lkp_district AS district
                    ON district.district_id = @DistrictId
                WHERE batch.import_batch_id = @ImportBatchId;

                -- Without an explicit district, every unknown station must have one
                -- sufficiently close known station with a district. DIFFERENCE handles
                -- common historic spelling errors such as PIENAR/PIENAAR.
                IF @DistrictId IS NULL AND EXISTS (
                    SELECT 1
                    FROM dbo.staging_crash_summaries AS staged
                    WHERE staged.import_batch_id = @ImportBatchId
                      AND staged.review_status = N'Approved'
                      AND staged.import_status = N'NotImported'
                      AND NULLIF(LTRIM(RTRIM(staged.station)), N'') IS NOT NULL
                      AND NOT EXISTS (
                          SELECT 1 FROM dbo.lkp_saps_stations AS exact_station
                          WHERE UPPER(LTRIM(RTRIM(exact_station.station_name))) =
                                UPPER(LTRIM(RTRIM(staged.station)))
                      )
                      AND NOT EXISTS (
                          SELECT 1 FROM dbo.lkp_saps_stations AS similar_station
                          WHERE similar_station.is_active = 1
                            AND similar_station.district_id IS NOT NULL
                            AND DIFFERENCE(similar_station.station_name, staged.station) >= 3
                      )
                )
                    THROW 51003, 'At least one unknown station has no reliable district match. Correct it or supply a district before synchronising geography.', 1;

                INSERT INTO dbo.lkp_saps_stations
                    (station_name, province_code, district, district_id, is_active, created_at)
                SELECT DISTINCT
                    UPPER(LTRIM(RTRIM(staged.station))),
                    COALESCE(district.province_code, @ProvinceCode),
                    district.district_name,
                    COALESCE(@DistrictId, closest.district_id),
                    1,
                    GETDATE()
                FROM dbo.staging_crash_summaries AS staged
                OUTER APPLY (
                    SELECT TOP (1)
                        known.district_id,
                        DIFFERENCE(known.station_name, staged.station) AS match_score
                    FROM dbo.lkp_saps_stations AS known
                    WHERE known.is_active = 1
                      AND known.district_id IS NOT NULL
                      AND DIFFERENCE(known.station_name, staged.station) >= 3
                    ORDER BY DIFFERENCE(known.station_name, staged.station) DESC,
                             ABS(LEN(known.station_name) - LEN(staged.station)),
                             known.station_id
                ) AS closest
                LEFT JOIN dbo.lkp_district AS district
                    ON district.district_id = COALESCE(@DistrictId, closest.district_id)
                WHERE staged.import_batch_id = @ImportBatchId
                  AND staged.review_status = N'Approved'
                  AND staged.import_status = N'NotImported'
                  AND NULLIF(LTRIM(RTRIM(staged.station)), N'') IS NOT NULL
                  AND COALESCE(@DistrictId, closest.district_id) IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM dbo.lkp_saps_stations AS existing WITH (UPDLOCK, HOLDLOCK)
                      WHERE UPPER(LTRIM(RTRIM(existing.station_name))) =
                            UPPER(LTRIM(RTRIM(staged.station)))
                  );

                DECLARE @StationsInserted int = @@ROWCOUNT;

                INSERT INTO dbo.lkp_routes
                    (route_code, description, province_code, is_active, created_at)
                SELECT DISTINCT
                    UPPER(LTRIM(RTRIM(staged.route))), NULL, @ProvinceCode, 1, GETDATE()
                FROM dbo.staging_crash_summaries AS staged
                WHERE staged.import_batch_id = @ImportBatchId
                  AND staged.review_status = N'Approved'
                  AND staged.import_status = N'NotImported'
                  AND NULLIF(LTRIM(RTRIM(staged.route)), N'') IS NOT NULL
                  AND LEN(LTRIM(RTRIM(staged.route))) <= 20
                  AND NOT EXISTS (
                      SELECT 1
                      FROM dbo.lkp_routes AS existing WITH (UPDLOCK, HOLDLOCK)
                      WHERE UPPER(LTRIM(RTRIM(existing.route_code))) =
                            UPPER(LTRIM(RTRIM(staged.route)))
                  );

                SELECT @StationsInserted AS stations_inserted, @@ROWCOUNT AS routes_inserted;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Reinstating NOT NULL would be unsafe after regional rows have been stored.
        // Keep the data-compatible column shape; restore the earlier procedure body by
        // reapplying the preceding migration if an operational rollback is required.
    }
}
