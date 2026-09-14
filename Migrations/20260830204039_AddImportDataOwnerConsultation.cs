using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrashReport.Migrations;

/// <summary>
/// Adds only the audit fields used when an import finding is referred to a data owner.
/// The staged-import tables themselves are created by the earlier pipeline migration.
/// </summary>
public partial class AddImportDataOwnerConsultation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>("requires_data_owner", "import_data_quality_issues", type: "bit", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<DateTime>("referred_at", "import_data_quality_issues", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<string>("referred_by_user_id", "import_data_quality_issues", type: "nvarchar(450)", maxLength: 450, nullable: true);
        migrationBuilder.AddColumn<string>("referred_to", "import_data_quality_issues", type: "nvarchar(200)", maxLength: 200, nullable: true);
        migrationBuilder.AddColumn<DateTime>("response_due_at", "import_data_quality_issues", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<string>("referral_question", "import_data_quality_issues", type: "nvarchar(2000)", maxLength: 2000, nullable: true);
        migrationBuilder.AddColumn<string>("data_owner_response", "import_data_quality_issues", type: "nvarchar(2000)", maxLength: 2000, nullable: true);
        migrationBuilder.AddColumn<DateTime>("responded_at", "import_data_quality_issues", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<string>("response_recorded_by_user_id", "import_data_quality_issues", type: "nvarchar(450)", maxLength: 450, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("requires_data_owner", "import_data_quality_issues");
        migrationBuilder.DropColumn("referred_at", "import_data_quality_issues");
        migrationBuilder.DropColumn("referred_by_user_id", "import_data_quality_issues");
        migrationBuilder.DropColumn("referred_to", "import_data_quality_issues");
        migrationBuilder.DropColumn("response_due_at", "import_data_quality_issues");
        migrationBuilder.DropColumn("referral_question", "import_data_quality_issues");
        migrationBuilder.DropColumn("data_owner_response", "import_data_quality_issues");
        migrationBuilder.DropColumn("responded_at", "import_data_quality_issues");
        migrationBuilder.DropColumn("response_recorded_by_user_id", "import_data_quality_issues");
    }
}
