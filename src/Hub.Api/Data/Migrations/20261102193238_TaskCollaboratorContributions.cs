using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class TaskCollaboratorContributions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "ak_task_collaborator_id_task_id_user_id",
                schema: "hub",
                table: "task_collaborator",
                columns: new[] { "id", "task_id", "user_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_task_id_project_id",
                schema: "hub",
                table: "task",
                columns: new[] { "id", "project_id" });

            migrationBuilder.CreateTable(
                name: "task_contribution",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    collaborator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_key_snapshot = table.Column<string>(type: "text", nullable: false),
                    task_name_snapshot = table.Column<string>(type: "text", nullable: false),
                    marked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_contribution", x => x.id);
                    table.CheckConstraint("ck_task_contribution_note", "marked_at IS NOT NULL OR note IS NULL");
                    table.CheckConstraint("ck_task_contribution_version", "row_version >= 1");
                    table.ForeignKey(
                        name: "fk_task_contribution_task_collaborator_collaborator_id_task_id~",
                        columns: x => new { x.collaborator_id, x.task_id, x.user_id },
                        principalSchema: "hub",
                        principalTable: "task_collaborator",
                        principalColumns: new[] { "id", "task_id", "user_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_task_contribution_task_task_id_project_id",
                        columns: x => new { x.task_id, x.project_id },
                        principalSchema: "hub",
                        principalTable: "task",
                        principalColumns: new[] { "id", "project_id" });
                });

            migrationBuilder.CreateIndex(
                name: "ix_task_contribution_collaborator_id",
                schema: "hub",
                table: "task_contribution",
                column: "collaborator_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_task_contribution_collaborator_id_task_id_user_id",
                schema: "hub",
                table: "task_contribution",
                columns: new[] { "collaborator_id", "task_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_task_contribution_task_id_project_id",
                schema: "hub",
                table: "task_contribution",
                columns: new[] { "task_id", "project_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "task_contribution",
                schema: "hub");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_task_collaborator_id_task_id_user_id",
                schema: "hub",
                table: "task_collaborator");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_task_id_project_id",
                schema: "hub",
                table: "task");
        }
    }
}
