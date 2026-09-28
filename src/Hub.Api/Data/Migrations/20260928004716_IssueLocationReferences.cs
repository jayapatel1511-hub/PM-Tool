using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class IssueLocationReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "issue_document_reference",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    issue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    identifier = table.Column<string>(type: "text", nullable: false),
                    revision = table.Column<string>(type: "text", nullable: false),
                    source_url = table.Column<string>(type: "text", nullable: false),
                    external_topic_id = table.Column<string>(type: "text", nullable: true),
                    model_element_guid = table.Column<string>(type: "text", nullable: true),
                    viewpoint_url = table.Column<string>(type: "text", nullable: true),
                    is_available = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_issue_document_reference", x => x.id);
                    table.CheckConstraint("ck_issue_document_kind", "kind IN ('Drawing','Model','Markup','Screenshot')");
                    table.ForeignKey(
                        name: "fk_issue_document_reference_issue_issue_id",
                        column: x => x.issue_id,
                        principalSchema: "hub",
                        principalTable: "issue",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_issue_document_reference_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "issue_location",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    issue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    site_area = table.Column<string>(type: "text", nullable: true),
                    building = table.Column<string>(type: "text", nullable: true),
                    level = table.Column<string>(type: "text", nullable: true),
                    room = table.Column<string>(type: "text", nullable: true),
                    asset_system = table.Column<string>(type: "text", nullable: true),
                    alignment = table.Column<string>(type: "text", nullable: true),
                    start_station = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    end_station = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    station_units = table.Column<string>(type: "text", nullable: true),
                    coordinate_x = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    coordinate_y = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    coordinate_z = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    coordinate_reference_system = table.Column<string>(type: "text", nullable: true),
                    coordinate_units = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_issue_location", x => x.id);
                    table.CheckConstraint("ck_issue_location_kind", "kind IN ('SiteArea','Building','Alignment','Coordinate')");
                    table.CheckConstraint("ck_issue_location_station_order", "end_station IS NULL OR start_station IS NULL OR end_station >= start_station");
                    table.ForeignKey(
                        name: "fk_issue_location_issue_issue_id",
                        column: x => x.issue_id,
                        principalSchema: "hub",
                        principalTable: "issue",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_issue_location_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "issue_verification",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    issue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    verifier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    evidence_url = table.Column<string>(type: "text", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_issue_verification", x => x.id);
                    table.CheckConstraint("ck_issue_verification_status", "status IN ('Proposed','Verified','Rejected')");
                    table.ForeignKey(
                        name: "fk_issue_verification_app_user_verifier_id",
                        column: x => x.verifier_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_issue_verification_issue_issue_id",
                        column: x => x.issue_id,
                        principalSchema: "hub",
                        principalTable: "issue",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_issue_verification_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_issue_document_reference_issue_id_identifier_revision",
                schema: "hub",
                table: "issue_document_reference",
                columns: new[] { "issue_id", "identifier", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_issue_document_reference_project_id",
                schema: "hub",
                table: "issue_document_reference",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_issue_location_issue_id_created_at",
                schema: "hub",
                table: "issue_location",
                columns: new[] { "issue_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_issue_location_project_id",
                schema: "hub",
                table: "issue_location",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_issue_verification_issue_id_created_at",
                schema: "hub",
                table: "issue_verification",
                columns: new[] { "issue_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_issue_verification_project_id",
                schema: "hub",
                table: "issue_verification",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_issue_verification_verifier_id",
                schema: "hub",
                table: "issue_verification",
                column: "verifier_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "issue_document_reference",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "issue_location",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "issue_verification",
                schema: "hub");
        }
    }
}
