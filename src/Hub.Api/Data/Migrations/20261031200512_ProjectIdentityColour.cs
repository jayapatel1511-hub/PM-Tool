using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProjectIdentityColour : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "identity_colour",
                schema: "hub",
                table: "project",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_project_identity_colour",
                schema: "hub",
                table: "project",
                sql: "identity_colour IS NULL OR lower(identity_colour) IN ('#ec6a3e','#20a36d','#d9467b','#1597cc','#84a32a')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_project_identity_colour",
                schema: "hub",
                table: "project");

            migrationBuilder.DropColumn(
                name: "identity_colour",
                schema: "hub",
                table: "project");
        }
    }
}
