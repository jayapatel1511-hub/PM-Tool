using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReviewResponseOutcomes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "response_outcome",
                schema: "hub",
                table: "review_finding",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "response_outcome",
                schema: "hub",
                table: "finding_event",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_review_finding_response_outcome",
                schema: "hub",
                table: "review_finding",
                sql: "response_outcome IS NULL OR (response_outcome IN ('Incorporated','Not incorporated','Clarified','Noted') AND response IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_finding_event_response_outcome",
                schema: "hub",
                table: "finding_event",
                sql: "response_outcome IS NULL OR (response_outcome IN ('Incorporated','Not incorporated','Clarified','Noted') AND action = 'Responded')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_review_finding_response_outcome",
                schema: "hub",
                table: "review_finding");

            migrationBuilder.DropCheckConstraint(
                name: "ck_finding_event_response_outcome",
                schema: "hub",
                table: "finding_event");

            migrationBuilder.DropColumn(
                name: "response_outcome",
                schema: "hub",
                table: "review_finding");

            migrationBuilder.DropColumn(
                name: "response_outcome",
                schema: "hub",
                table: "finding_event");
        }
    }
}
