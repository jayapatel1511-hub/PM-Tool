using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DesignBasisWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "next_basis_seq",
                schema: "hub",
                table: "project",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "basis_impact_assessment",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    basis_use_id = table.Column<Guid>(type: "uuid", nullable: false),
                    old_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    new_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    rationale = table.Column<string>(type: "text", nullable: true),
                    evidence_url = table.Column<string>(type: "text", nullable: true),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_basis_impact_assessment", x => x.id);
                    table.CheckConstraint("ck_basis_impact_status", "status IN ('Pending Assessment','Unaffected','Update Required','Clarification Needed','Resolved')");
                    table.ForeignKey(
                        name: "fk_basis_impact_assessment_app_user_decided_by",
                        column: x => x.decided_by,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_basis_impact_assessment_app_user_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_basis_impact_assessment_basis_uses_basis_use_id",
                        column: x => x.basis_use_id,
                        principalSchema: "hub",
                        principalTable: "basis_use",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_basis_impact_assessment_design_basis_versions_new_version_id",
                        column: x => x.new_version_id,
                        principalSchema: "hub",
                        principalTable: "design_basis_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_basis_impact_assessment_design_basis_versions_old_version_id",
                        column: x => x.old_version_id,
                        principalSchema: "hub",
                        principalTable: "design_basis_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_basis_impact_assessment_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_basis_impact_assessment_basis_use_id_new_version_id",
                schema: "hub",
                table: "basis_impact_assessment",
                columns: new[] { "basis_use_id", "new_version_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_basis_impact_assessment_decided_by",
                schema: "hub",
                table: "basis_impact_assessment",
                column: "decided_by");

            migrationBuilder.CreateIndex(
                name: "ix_basis_impact_assessment_new_version_id",
                schema: "hub",
                table: "basis_impact_assessment",
                column: "new_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_basis_impact_assessment_old_version_id",
                schema: "hub",
                table: "basis_impact_assessment",
                column: "old_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_basis_impact_assessment_owner_id",
                schema: "hub",
                table: "basis_impact_assessment",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_basis_impact_assessment_project_id_status",
                schema: "hub",
                table: "basis_impact_assessment",
                columns: new[] { "project_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "basis_impact_assessment",
                schema: "hub");

            migrationBuilder.DropColumn(
                name: "next_basis_seq",
                schema: "hub",
                table: "project");
        }
    }
}
