using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class HomeOverviewSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "home_preset",
                schema: "hub",
                table: "user_setting",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "home_metrics",
                schema: "hub",
                table: "project_health_snapshot",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "home_preset",
                schema: "hub",
                table: "user_setting");

            migrationBuilder.DropColumn(
                name: "home_metrics",
                schema: "hub",
                table: "project_health_snapshot");
        }
    }
}
