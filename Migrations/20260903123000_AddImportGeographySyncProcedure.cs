using CrashReport.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrashReport.Migrations;

/// <summary>
/// Adds the controlled database operation used to extend station and route lookup
/// data from reviewed import rows. Keeping it in a migration versions the procedure
/// alongside the application code that calls it.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260903123000_AddImportGeographySyncProcedure")]
public sealed class AddImportGeographySyncProcedure : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE OR ALTER PROCEDURE dbo.usp_SyncImportGeography
                @ImportBatchId int,
                @DistrictId int = NULL
            AS
            BEGIN
                SET NOCOUNT ON;
                SET XACT_ABORT ON;

                DECLARE @ResolvedDistrictId int = @DistrictId;
                DECLARE @DistrictCount int;
                DECLARE @ProvinceCode nvarchar(5);
                DECLARE @DistrictName nvarchar(100);

                IF NOT EXISTS (
                    SELECT 1 FROM dbo.import_batches WHERE import_batch_id = @ImportBatchId
                )
                    THROW 51000, 'The requested import batch does not exist.', 1;

                -- When the caller does not provide a district, infer it only from known
                -- stations in approved rows. Exactly one district is required.
                IF @ResolvedDistrictId IS NULL
                BEGIN
                    SELECT
                        @DistrictCount = COUNT(DISTINCT station.district_id),
                        @ResolvedDistrictId = MIN(station.district_id)
                    FROM dbo.staging_crash_summaries AS staged
                    INNER JOIN dbo.lkp_saps_stations AS station
                        ON UPPER(LTRIM(RTRIM(station.station_name))) =
                           UPPER(LTRIM(RTRIM(staged.station)))
                    WHERE staged.import_batch_id = @ImportBatchId
                      AND staged.review_status = N'Approved'
                      AND staged.import_status = N'NotImported'
                      AND station.is_active = 1
                      AND station.district_id IS NOT NULL;

                    IF ISNULL(@DistrictCount, 0) <> 1
                        THROW 51001, 'The batch district cannot be inferred uniquely from its known stations. Supply a district before synchronising geography.', 1;
                END

                SELECT
                    @ProvinceCode = province_code,
                    @DistrictName = district_name
                FROM dbo.lkp_district
                WHERE district_id = @ResolvedDistrictId
                  AND is_active = 1;

                IF @DistrictName IS NULL
                    THROW 51002, 'The supplied or inferred district is not an active lookup district.', 1;

                -- Add only reviewed stations. Case and surrounding whitespace do not
                -- create separate lookup rows. A transaction/lock prevents concurrent
                -- imports from inserting the same station twice.
                INSERT INTO dbo.lkp_saps_stations
                    (station_name, province_code, district, district_id, is_active, created_at)
                SELECT DISTINCT
                    UPPER(LTRIM(RTRIM(staged.station))),
                    @ProvinceCode,
                    @DistrictName,
                    @ResolvedDistrictId,
                    1,
                    GETDATE()
                FROM dbo.staging_crash_summaries AS staged
                WHERE staged.import_batch_id = @ImportBatchId
                  AND staged.review_status = N'Approved'
                  AND staged.import_status = N'NotImported'
                  AND NULLIF(LTRIM(RTRIM(staged.station)), N'') IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM dbo.lkp_saps_stations AS existing WITH (UPDLOCK, HOLDLOCK)
                      WHERE UPPER(LTRIM(RTRIM(existing.station_name))) =
                            UPPER(LTRIM(RTRIM(staged.station)))
                  );

                DECLARE @StationsInserted int = @@ROWCOUNT;

                -- Routes are province-level in the current schema; there is no district_id
                -- on lkp_routes. Retain that design and prevent case-only duplicates.
                INSERT INTO dbo.lkp_routes
                    (route_code, description, province_code, is_active, created_at)
                SELECT DISTINCT
                    UPPER(LTRIM(RTRIM(staged.route))),
                    NULL,
                    @ProvinceCode,
                    1,
                    GETDATE()
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
                        AND ISNULL(existing.province_code, N'') = ISNULL(@ProvinceCode, N'')
                  );

                DECLARE @RoutesInserted int = @@ROWCOUNT;

                SELECT
                    @ResolvedDistrictId AS district_id,
                    @StationsInserted AS stations_inserted,
                    @RoutesInserted AS routes_inserted;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The lookup rows are retained because production records may depend on them.
        migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_SyncImportGeography;");
    }
}
