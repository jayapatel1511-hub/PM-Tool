using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DeliverableBlockingCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "blocking_count",
                schema: "hub",
                table: "deliverable_state",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "blocking_count",
                schema: "hub",
                table: "deliverable_state");
        }
    }
}
