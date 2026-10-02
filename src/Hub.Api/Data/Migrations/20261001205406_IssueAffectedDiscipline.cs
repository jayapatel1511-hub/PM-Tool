using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class IssueAffectedDiscipline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "issue_affected_discipline",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    issue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_discipline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_issue_affected_discipline", x => x.id);
                    table.ForeignKey(
                        name: "fk_issue_affected_discipline_issue_issue_id",
                        column: x => x.issue_id,
                        principalSchema: "hub",
                        principalTable: "issue",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_issue_affected_discipline_project_disciplines_project_discip~",
                        column: x => x.project_discipline_id,
                        principalSchema: "hub",
                        principalTable: "project_discipline",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_issue_affected_discipline_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_issue_affected_discipline_issue_id_project_discipline_id",
                schema: "hub",
                table: "issue_affected_discipline",
                columns: new[] { "issue_id", "project_discipline_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_issue_affected_discipline_project_discipline_id",
                schema: "hub",
                table: "issue_affected_discipline",
                column: "project_discipline_id");

            migrationBuilder.CreateIndex(
                name: "ix_issue_affected_discipline_project_id",
                schema: "hub",
                table: "issue_affected_discipline",
                column: "project_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "issue_affected_discipline",
                schema: "hub");
        }
    }
}
