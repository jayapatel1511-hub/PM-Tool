using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProjectMemberWorkspaceDocumentLinkConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "row_version",
                schema: "hub",
                table: "workspace",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "row_version",
                schema: "hub",
                table: "project_member",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "row_version",
                schema: "hub",
                table: "document_link",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "row_version",
                schema: "hub",
                table: "workspace");

            migrationBuilder.DropColumn(
                name: "row_version",
                schema: "hub",
                table: "project_member");

            migrationBuilder.DropColumn(
                name: "row_version",
                schema: "hub",
                table: "document_link");
        }
    }
}
