using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrashReport.Migrations
{
    /// <inheritdoc />
    public partial class ChangeUniqueConstraintOnCrashSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Drop the old unique constraint (name may vary – adjust if needed)
            migrationBuilder.Sql(
                "ALTER TABLE crash_summaries DROP CONSTRAINT IF EXISTS UQ_crash_summaries_cr_no;");

            // 2. Add the new composite unique constraint on (cr_no, source_file)
            migrationBuilder.Sql(
                "ALTER TABLE crash_summaries ADD CONSTRAINT UQ_crash_summaries_cr_no_source_file UNIQUE (cr_no, source_file);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse: drop the composite, restore the single‑column constraint
            migrationBuilder.Sql(
                "ALTER TABLE crash_summaries DROP CONSTRAINT IF EXISTS UQ_crash_summaries_cr_no_source_file;");

            migrationBuilder.Sql(
                "ALTER TABLE crash_summaries ADD CONSTRAINT UQ_crash_summaries_cr_no UNIQUE (cr_no);");
        }
    }
}