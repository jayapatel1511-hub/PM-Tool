using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AllocationReviewEffort : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "review_hours",
                schema: "hub",
                table: "allocation_work_link",
                type: "numeric(9,3)",
                precision: 9,
                scale: 3,
                nullable: true);

            // Earlier proposal commands allowed review links without a dated estimate.
            // Retain those rows but remove them from active demand until a reviewer supplies hours.
            migrationBuilder.Sql("UPDATE hub.allocation_work_link SET released_at = CURRENT_TIMESTAMP WHERE work_type = 'Review' AND review_hours IS NULL AND released_at IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_allocation_review_hours",
                schema: "hub",
                table: "allocation_work_link",
                sql: "(work_type = 'Task' AND review_hours IS NULL) OR (work_type = 'Review' AND ((review_hours IS NOT NULL AND review_hours > 0) OR (released_at IS NOT NULL AND review_hours IS NULL)))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_allocation_review_hours",
                schema: "hub",
                table: "allocation_work_link");

            migrationBuilder.DropColumn(
                name: "review_hours",
                schema: "hub",
                table: "allocation_work_link");
        }
    }
}
