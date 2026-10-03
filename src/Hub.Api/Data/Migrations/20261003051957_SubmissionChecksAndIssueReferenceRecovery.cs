using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SubmissionChecksAndIssueReferenceRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_submission_check_status",
                schema: "hub",
                table: "submission_check");

            migrationBuilder.AddColumn<Guid>(
                name: "replaced_by_id",
                schema: "hub",
                table: "issue_document_reference",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_submission_check_status",
                schema: "hub",
                table: "submission_check",
                sql: "status IN ('Pending','Pass','Fail','Not Applicable')");

            migrationBuilder.CreateIndex(
                name: "ix_issue_document_reference_replaced_by_id",
                schema: "hub",
                table: "issue_document_reference",
                column: "replaced_by_id");

            migrationBuilder.AddForeignKey(
                name: "fk_issue_document_reference_issue_document_reference_replaced_~",
                schema: "hub",
                table: "issue_document_reference",
                column: "replaced_by_id",
                principalSchema: "hub",
                principalTable: "issue_document_reference",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_issue_document_reference_issue_document_reference_replaced_~",
                schema: "hub",
                table: "issue_document_reference");

            migrationBuilder.DropCheckConstraint(
                name: "ck_submission_check_status",
                schema: "hub",
                table: "submission_check");

            migrationBuilder.DropIndex(
                name: "ix_issue_document_reference_replaced_by_id",
                schema: "hub",
                table: "issue_document_reference");

            migrationBuilder.DropColumn(
                name: "replaced_by_id",
                schema: "hub",
                table: "issue_document_reference");

            migrationBuilder.AddCheckConstraint(
                name: "ck_submission_check_status",
                schema: "hub",
                table: "submission_check",
                sql: "status IN ('Pending','Pass','Not Applicable')");
        }
    }
}
