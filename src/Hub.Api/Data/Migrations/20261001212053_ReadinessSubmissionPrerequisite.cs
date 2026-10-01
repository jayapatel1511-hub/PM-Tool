using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReadinessSubmissionPrerequisite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "readiness_submission_prerequisite",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "text", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    removed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    removal_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_readiness_submission_prerequisite", x => x.id);
                    table.CheckConstraint("ck_readiness_prerequisite_target", "target_type IN ('Task', 'Deliverable')");
                    table.ForeignKey(
                        name: "fk_readiness_submission_prerequisite_app_user_removed_by",
                        column: x => x.removed_by,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_readiness_submission_prerequisite_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_readiness_submission_prerequisite_submission_packages_packag~",
                        column: x => x.package_id,
                        principalSchema: "hub",
                        principalTable: "submission_package",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_readiness_submission_prerequisite_package_id",
                schema: "hub",
                table: "readiness_submission_prerequisite",
                column: "package_id");

            migrationBuilder.CreateIndex(
                name: "ix_readiness_submission_prerequisite_project_id_target_type_ta~",
                schema: "hub",
                table: "readiness_submission_prerequisite",
                columns: new[] { "project_id", "target_type", "target_id", "package_id" },
                unique: true,
                filter: "removed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_readiness_submission_prerequisite_removed_by",
                schema: "hub",
                table: "readiness_submission_prerequisite",
                column: "removed_by");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "readiness_submission_prerequisite",
                schema: "hub");
        }
    }
}
