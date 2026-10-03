using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class IssueReferenceProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "source_system",
                schema: "hub",
                table: "issue_document_reference",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stable_source_id",
                schema: "hub",
                table: "issue_document_reference",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "source_system",
                schema: "hub",
                table: "issue_document_reference");

            migrationBuilder.DropColumn(
                name: "stable_source_id",
                schema: "hub",
                table: "issue_document_reference");
        }
    }
}
