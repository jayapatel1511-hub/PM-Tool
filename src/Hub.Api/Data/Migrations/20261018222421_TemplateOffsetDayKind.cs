using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class TemplateOffsetDayKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "offset_day_kind",
                schema: "hub",
                table: "project_template",
                type: "text",
                nullable: false,
                defaultValue: "CalendarDays");

            migrationBuilder.AddCheckConstraint(
                name: "ck_template_offset_day_kind",
                schema: "hub",
                table: "project_template",
                sql: "offset_day_kind IN ('CalendarDays','WorkingDays')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_template_offset_day_kind",
                schema: "hub",
                table: "project_template");

            migrationBuilder.DropColumn(
                name: "offset_day_kind",
                schema: "hub",
                table: "project_template");
        }
    }
}
