using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ScreenPersonAndIssueMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "responsibility_note",
                schema: "hub",
                table: "project_member",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "client_drawing_number_pattern",
                schema: "hub",
                table: "project",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "purpose_label_snapshot",
                schema: "hub",
                table: "deliverable_issue",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "purpose_of_issue_id",
                schema: "hub",
                table: "deliverable_issue",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "response_date",
                schema: "hub",
                table: "deliverable_issue",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "response_recorded_at",
                schema: "hub",
                table: "deliverable_issue",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "response_recorded_by",
                schema: "hub",
                table: "deliverable_issue",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "return_code_id",
                schema: "hub",
                table: "deliverable_issue",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "return_code_snapshot",
                schema: "hub",
                table: "deliverable_issue",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "return_label_snapshot",
                schema: "hub",
                table: "deliverable_issue",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "row_version",
                schema: "hub",
                table: "deliverable_issue",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "client_drawing_number",
                schema: "hub",
                table: "deliverable",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "current_issue_id",
                schema: "hub",
                table: "deliverable",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "issue_purpose",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_issue_purpose", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "issue_return_code",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "citext", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_issue_return_code", x => x.id);
                    table.CheckConstraint("ck_issue_return_code", "code = btrim(code) AND char_length(code) BETWEEN 1 AND 10");
                });

            migrationBuilder.CreateIndex(
                name: "ix_deliverable_issue_project_id_deliverable_id_created_at",
                schema: "hub",
                table: "deliverable_issue",
                columns: new[] { "project_id", "deliverable_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_deliverable_issue_purpose_of_issue_id",
                schema: "hub",
                table: "deliverable_issue",
                column: "purpose_of_issue_id");

            migrationBuilder.CreateIndex(
                name: "ix_deliverable_issue_response_recorded_by",
                schema: "hub",
                table: "deliverable_issue",
                column: "response_recorded_by");

            migrationBuilder.CreateIndex(
                name: "ix_deliverable_issue_return_code_id",
                schema: "hub",
                table: "deliverable_issue",
                column: "return_code_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_deliverable_issue_response_pair",
                schema: "hub",
                table: "deliverable_issue",
                sql: "(return_code_id IS NULL) = (response_date IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_issue_purpose_name",
                schema: "hub",
                table: "issue_purpose",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_issue_return_code_code",
                schema: "hub",
                table: "issue_return_code",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_issue_return_code_name",
                schema: "hub",
                table: "issue_return_code",
                column: "name");

            migrationBuilder.AddForeignKey(
                name: "fk_deliverable_issue_app_user_response_recorded_by",
                schema: "hub",
                table: "deliverable_issue",
                column: "response_recorded_by",
                principalSchema: "hub",
                principalTable: "app_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_deliverable_issue_issue_purposes_purpose_of_issue_id",
                schema: "hub",
                table: "deliverable_issue",
                column: "purpose_of_issue_id",
                principalSchema: "hub",
                principalTable: "issue_purpose",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_deliverable_issue_issue_return_codes_return_code_id",
                schema: "hub",
                table: "deliverable_issue",
                column: "return_code_id",
                principalSchema: "hub",
                principalTable: "issue_return_code",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_deliverable_issue_app_user_response_recorded_by",
                schema: "hub",
                table: "deliverable_issue");

            migrationBuilder.DropForeignKey(
                name: "fk_deliverable_issue_issue_purposes_purpose_of_issue_id",
                schema: "hub",
                table: "deliverable_issue");

            migrationBuilder.DropForeignKey(
                name: "fk_deliverable_issue_issue_return_codes_return_code_id",
                schema: "hub",
                table: "deliverable_issue");

            migrationBuilder.DropTable(
                name: "issue_purpose",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "issue_return_code",
                schema: "hub");

            migrationBuilder.DropIndex(
                name: "ix_deliverable_issue_project_id_deliverable_id_created_at",
                schema: "hub",
                table: "deliverable_issue");

            migrationBuilder.DropIndex(
                name: "ix_deliverable_issue_purpose_of_issue_id",
                schema: "hub",
                table: "deliverable_issue");

            migrationBuilder.DropIndex(
                name: "ix_deliverable_issue_response_recorded_by",
                schema: "hub",
                table: "deliverable_issue");

            migrationBuilder.DropIndex(
                name: "ix_deliverable_issue_return_code_id",
                schema: "hub",
                table: "deliverable_issue");

            migrationBuilder.DropCheckConstraint(
                name: "ck_deliverable_issue_response_pair",
                schema: "hub",
                table: "deliverable_issue");

            migrationBuilder.DropColumn(
                name: "responsibility_note",
                schema: "hub",
                table: "project_member");

            migrationBuilder.DropColumn(
                name: "client_drawing_number_pattern",
                schema: "hub",
                table: "project");

            migrationBuilder.DropColumn(
                name: "purpose_label_snapshot",
                schema: "hub",
                table: "deliverable_issue");

            migrationBuilder.DropColumn(
                name: "purpose_of_issue_id",
                schema: "hub",
                table: "deliverable_issue");

            migrationBuilder.DropColumn(
                name: "response_date",
                schema: "hub",
                table: "deliverable_issue");

            migrationBuilder.DropColumn(
                name: "response_recorded_at",
                schema: "hub",
                table: "deliverable_issue");

            migrationBuilder.DropColumn(
                name: "response_recorded_by",
                schema: "hub",
                table: "deliverable_issue");

            migrationBuilder.DropColumn(
                name: "return_code_id",
                schema: "hub",
                table: "deliverable_issue");

            migrationBuilder.DropColumn(
                name: "return_code_snapshot",
                schema: "hub",
                table: "deliverable_issue");

            migrationBuilder.DropColumn(
                name: "return_label_snapshot",
                schema: "hub",
                table: "deliverable_issue");

            migrationBuilder.DropColumn(
                name: "row_version",
                schema: "hub",
                table: "deliverable_issue");

            migrationBuilder.DropColumn(
                name: "client_drawing_number",
                schema: "hub",
                table: "deliverable");

            migrationBuilder.DropColumn(
                name: "current_issue_id",
                schema: "hub",
                table: "deliverable");
        }
    }
}
