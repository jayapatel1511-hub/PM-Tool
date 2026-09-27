using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SubmissionReadinessSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "next_submission_seq",
                schema: "hub",
                table: "project",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "submission_package",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    purpose = table.Column<string>(type: "text", nullable: false),
                    recipient_reference = table.Column<string>(type: "text", nullable: false),
                    coordinator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    milestone_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    manifest_version = table.Column<int>(type: "integer", nullable: false),
                    supersedes_package_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false),
                    seq = table.Column<int>(type: "integer", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submission_package", x => x.id);
                    table.CheckConstraint("ck_submission_package_status", "status IN ('Draft','Checking','Ready','Issued','Superseded','Cancelled')");
                    table.ForeignKey(
                        name: "fk_submission_package_app_user_coordinator_id",
                        column: x => x.coordinator_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_submission_package_milestone_milestone_id",
                        column: x => x.milestone_id,
                        principalSchema: "hub",
                        principalTable: "milestone",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_submission_package_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_submission_package_submission_package_supersedes_package_id",
                        column: x => x.supersedes_package_id,
                        principalSchema: "hub",
                        principalTable: "submission_package",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "submission_check",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    manifest_version = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    project_discipline_id = table.Column<Guid>(type: "uuid", nullable: true),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    required = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    evidence_rule = table.Column<string>(type: "text", nullable: true),
                    evidence_url = table.Column<string>(type: "text", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submission_check", x => x.id);
                    table.CheckConstraint("ck_submission_check_kind", "kind IN ('Deliverable','Current Revision','Independent Review','Blocking Findings','Handoff','Change Assessment','Access','Applicability')");
                    table.CheckConstraint("ck_submission_check_status", "status IN ('Pending','Pass','Not Applicable')");
                    table.CheckConstraint("ck_submission_check_waiver", "status <> 'Not Applicable' OR (kind = 'Applicability' AND required = false AND reason IS NOT NULL AND evidence_url IS NOT NULL AND approved_by IS NOT NULL AND approved_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_submission_check_app_user_approved_by",
                        column: x => x.approved_by,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_submission_check_app_user_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_submission_check_project_discipline_project_discipline_id",
                        column: x => x.project_discipline_id,
                        principalSchema: "hub",
                        principalTable: "project_discipline",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_submission_check_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_submission_check_submission_packages_package_id",
                        column: x => x.package_id,
                        principalSchema: "hub",
                        principalTable: "submission_package",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "submission_issue",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    manifest_version = table.Column<int>(type: "integer", nullable: false),
                    manifest_snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    check_snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    authorised_by = table.Column<Guid>(type: "uuid", nullable: false),
                    authorised_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    destination = table.Column<string>(type: "text", nullable: false),
                    transmittal_url = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submission_issue", x => x.id);
                    table.ForeignKey(
                        name: "fk_submission_issue_app_user_authorised_by",
                        column: x => x.authorised_by,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_submission_issue_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_submission_issue_submission_packages_package_id",
                        column: x => x.package_id,
                        principalSchema: "hub",
                        principalTable: "submission_package",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "submission_manifest_item",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    manifest_version = table.Column<int>(type: "integer", nullable: false),
                    deliverable_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_round_id = table.Column<Guid>(type: "uuid", nullable: true),
                    required = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submission_manifest_item", x => x.id);
                    table.ForeignKey(
                        name: "fk_submission_manifest_item_deliverable_deliverable_id",
                        column: x => x.deliverable_id,
                        principalSchema: "hub",
                        principalTable: "deliverable",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_submission_manifest_item_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_submission_manifest_item_review_round_review_round_id",
                        column: x => x.review_round_id,
                        principalSchema: "hub",
                        principalTable: "review_round",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_submission_manifest_item_source_revision_source_revision_id",
                        column: x => x.source_revision_id,
                        principalSchema: "hub",
                        principalTable: "source_revision",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_submission_manifest_item_submission_packages_package_id",
                        column: x => x.package_id,
                        principalSchema: "hub",
                        principalTable: "submission_package",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "check_evidence",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    check_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evidence_url = table.Column<string>(type: "text", nullable: false),
                    note = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_check_evidence", x => x.id);
                    table.ForeignKey(
                        name: "fk_check_evidence_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_check_evidence_submission_checks_check_id",
                        column: x => x.check_id,
                        principalSchema: "hub",
                        principalTable: "submission_check",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_check_evidence_check_id",
                schema: "hub",
                table: "check_evidence",
                column: "check_id");

            migrationBuilder.CreateIndex(
                name: "ix_check_evidence_project_id",
                schema: "hub",
                table: "check_evidence",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_submission_check_approved_by",
                schema: "hub",
                table: "submission_check",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "ix_submission_check_owner_id",
                schema: "hub",
                table: "submission_check",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_submission_check_package_id_manifest_version_kind",
                schema: "hub",
                table: "submission_check",
                columns: new[] { "package_id", "manifest_version", "kind" },
                unique: true,
                filter: "source_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_submission_check_package_id_manifest_version_kind_source_id",
                schema: "hub",
                table: "submission_check",
                columns: new[] { "package_id", "manifest_version", "kind", "source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_submission_check_project_discipline_id",
                schema: "hub",
                table: "submission_check",
                column: "project_discipline_id");

            migrationBuilder.CreateIndex(
                name: "ix_submission_check_project_id",
                schema: "hub",
                table: "submission_check",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_submission_issue_authorised_by",
                schema: "hub",
                table: "submission_issue",
                column: "authorised_by");

            migrationBuilder.CreateIndex(
                name: "ix_submission_issue_package_id",
                schema: "hub",
                table: "submission_issue",
                column: "package_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_submission_issue_project_id",
                schema: "hub",
                table: "submission_issue",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_submission_manifest_item_deliverable_id",
                schema: "hub",
                table: "submission_manifest_item",
                column: "deliverable_id");

            migrationBuilder.CreateIndex(
                name: "ix_submission_manifest_item_package_id_manifest_version_delive~",
                schema: "hub",
                table: "submission_manifest_item",
                columns: new[] { "package_id", "manifest_version", "deliverable_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_submission_manifest_item_project_id",
                schema: "hub",
                table: "submission_manifest_item",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_submission_manifest_item_review_round_id",
                schema: "hub",
                table: "submission_manifest_item",
                column: "review_round_id");

            migrationBuilder.CreateIndex(
                name: "ix_submission_manifest_item_source_revision_id",
                schema: "hub",
                table: "submission_manifest_item",
                column: "source_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_submission_package_coordinator_id",
                schema: "hub",
                table: "submission_package",
                column: "coordinator_id");

            migrationBuilder.CreateIndex(
                name: "ix_submission_package_key",
                schema: "hub",
                table: "submission_package",
                column: "key");

            migrationBuilder.CreateIndex(
                name: "ix_submission_package_milestone_id",
                schema: "hub",
                table: "submission_package",
                column: "milestone_id");

            migrationBuilder.CreateIndex(
                name: "ix_submission_package_project_id_seq",
                schema: "hub",
                table: "submission_package",
                columns: new[] { "project_id", "seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_submission_package_project_id_status",
                schema: "hub",
                table: "submission_package",
                columns: new[] { "project_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_submission_package_supersedes_package_id",
                schema: "hub",
                table: "submission_package",
                column: "supersedes_package_id");

            migrationBuilder.Sql(@"
CREATE FUNCTION hub.submission_issue_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'submission_issue is append-only'; END $$;
CREATE TRIGGER submission_issue_no_update BEFORE UPDATE OR DELETE ON hub.submission_issue FOR EACH ROW EXECUTE FUNCTION hub.submission_issue_immutable();
CREATE TRIGGER submission_issue_no_truncate BEFORE TRUNCATE ON hub.submission_issue FOR EACH STATEMENT EXECUTE FUNCTION hub.submission_issue_immutable();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER submission_issue_no_truncate ON hub.submission_issue;
DROP TRIGGER submission_issue_no_update ON hub.submission_issue;
DROP FUNCTION hub.submission_issue_immutable();");
            migrationBuilder.DropTable(
                name: "check_evidence",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "submission_issue",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "submission_manifest_item",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "submission_check",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "submission_package",
                schema: "hub");

            migrationBuilder.DropColumn(
                name: "next_submission_seq",
                schema: "hub",
                table: "project");
        }
    }
}
