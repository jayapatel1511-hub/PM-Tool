using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReviewFindingIssueLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "issue_id",
                schema: "hub",
                table: "review_finding",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_finding_issue_id",
                schema: "hub",
                table: "review_finding",
                column: "issue_id");

            migrationBuilder.AddForeignKey(
                name: "fk_review_finding_issue_issue_id",
                schema: "hub",
                table: "review_finding",
                column: "issue_id",
                principalSchema: "hub",
                principalTable: "issue",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_review_finding_issue_issue_id",
                schema: "hub",
                table: "review_finding");

            migrationBuilder.DropIndex(
                name: "ix_review_finding_issue_id",
                schema: "hub",
                table: "review_finding");

            migrationBuilder.DropColumn(
                name: "issue_id",
                schema: "hub",
                table: "review_finding");
        }
    }
}
