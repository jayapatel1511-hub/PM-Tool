using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class IssueMetadataIssueVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "issue_row_version",
                schema: "hub",
                table: "issue_verification",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "issue_row_version",
                schema: "hub",
                table: "issue_location",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "issue_row_version",
                schema: "hub",
                table: "issue_document_reference",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "issue_row_version",
                schema: "hub",
                table: "issue_verification");

            migrationBuilder.DropColumn(
                name: "issue_row_version",
                schema: "hub",
                table: "issue_location");

            migrationBuilder.DropColumn(
                name: "issue_row_version",
                schema: "hub",
                table: "issue_document_reference");
        }
    }
}
