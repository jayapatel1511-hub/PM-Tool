using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CoordinationIssueType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "issue_type",
                schema: "hub",
                table: "issue",
                type: "text",
                nullable: false,
                defaultValue: "General");

            // FR-LOC-01 decision: issues that already carry a location or drawing/model reference keep their verification
            // gate as Coordination issues; every other existing issue is General.
            migrationBuilder.Sql("""
                UPDATE hub.issue i SET issue_type = 'Coordination'
                WHERE EXISTS (SELECT 1 FROM hub.issue_location l WHERE l.issue_id = i.id)
                   OR EXISTS (SELECT 1 FROM hub.issue_document_reference d WHERE d.issue_id = i.id);
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_issue_type",
                schema: "hub",
                table: "issue",
                sql: "issue_type IN ('General','Coordination')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_issue_type",
                schema: "hub",
                table: "issue");

            migrationBuilder.DropColumn(
                name: "issue_type",
                schema: "hub",
                table: "issue");
        }
    }
}
