using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DesignBasisSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "basis_assumption_disposition",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "text", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: false),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_basis_assumption_disposition", x => x.id);
                    table.ForeignKey(
                        name: "fk_basis_assumption_disposition_app_user_approved_by",
                        column: x => x.approved_by,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_basis_assumption_disposition_app_user_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_basis_assumption_disposition_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "basis_conflict",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    left_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    right_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resolved = table.Column<bool>(type: "boolean", nullable: false),
                    resolution_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_basis_conflict", x => x.id);
                    table.CheckConstraint("ck_basis_conflict_order", "left_version_id < right_version_id");
                    table.ForeignKey(
                        name: "fk_basis_conflict_app_user_resolved_by",
                        column: x => x.resolved_by,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_basis_conflict_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "basis_use",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "text", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    intended_use = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_basis_use", x => x.id);
                    table.CheckConstraint("ck_basis_use_target", "target_type IN ('Task', 'Deliverable')");
                    table.ForeignKey(
                        name: "fk_basis_use_app_user_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_basis_use_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "design_basis_entry",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_discipline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    independent_approver_id = table.Column<Guid>(type: "uuid", nullable: true),
                    current_version_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_design_basis_entry", x => x.id);
                    table.CheckConstraint("ck_basis_entry_kind", "kind IN ('Criterion','Assumption')");
                    table.ForeignKey(
                        name: "fk_design_basis_entry_app_user_independent_approver_id",
                        column: x => x.independent_approver_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_design_basis_entry_app_user_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_design_basis_entry_project_disciplines_project_discipline_id",
                        column: x => x.project_discipline_id,
                        principalSchema: "hub",
                        principalTable: "project_discipline",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_design_basis_entry_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "design_basis_version",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    supersedes_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    scope = table.Column<string>(type: "text", nullable: false),
                    statement = table.Column<string>(type: "text", nullable: false),
                    numeric_value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    units = table.Column<string>(type: "text", nullable: true),
                    source_system = table.Column<string>(type: "text", nullable: true),
                    stable_source_id = table.Column<string>(type: "text", nullable: true),
                    source_url = table.Column<string>(type: "text", nullable: true),
                    declared_revision = table.Column<string>(type: "text", nullable: true),
                    confirmation_due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    decision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmation_rationale = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_design_basis_version", x => x.id);
                    table.CheckConstraint("ck_basis_confirmed_evidence", "status <> 'Confirmed' OR (source_url IS NOT NULL AND confirmation_rationale IS NOT NULL AND confirmed_by IS NOT NULL AND confirmed_at IS NOT NULL)");
                    table.CheckConstraint("ck_basis_numeric_units", "status NOT IN ('Confirmed', 'Superseded') OR numeric_value IS NULL OR (units IS NOT NULL AND length(trim(units)) > 0)");
                    table.CheckConstraint("ck_basis_version_number", "number > 0");
                    table.CheckConstraint("ck_basis_version_status", "status IN ('Proposed','Confirmed','Superseded','Withdrawn')");
                    table.ForeignKey(
                        name: "fk_design_basis_version_app_user_confirmed_by",
                        column: x => x.confirmed_by,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_design_basis_version_decision_decision_id",
                        column: x => x.decision_id,
                        principalSchema: "hub",
                        principalTable: "decision",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_design_basis_version_design_basis_entry_entry_id",
                        column: x => x.entry_id,
                        principalSchema: "hub",
                        principalTable: "design_basis_entry",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_design_basis_version_design_basis_version_supersedes_versio~",
                        column: x => x.supersedes_version_id,
                        principalSchema: "hub",
                        principalTable: "design_basis_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_design_basis_version_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_basis_assumption_disposition_approved_by",
                schema: "hub",
                table: "basis_assumption_disposition",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "ix_basis_assumption_disposition_owner_id",
                schema: "hub",
                table: "basis_assumption_disposition",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_basis_assumption_disposition_project_id",
                schema: "hub",
                table: "basis_assumption_disposition",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_basis_assumption_disposition_version_id_expires_on",
                schema: "hub",
                table: "basis_assumption_disposition",
                columns: new[] { "version_id", "expires_on" });

            migrationBuilder.CreateIndex(
                name: "ix_basis_conflict_left_version_id_right_version_id",
                schema: "hub",
                table: "basis_conflict",
                columns: new[] { "left_version_id", "right_version_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_basis_conflict_project_id",
                schema: "hub",
                table: "basis_conflict",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_basis_conflict_resolution_version_id",
                schema: "hub",
                table: "basis_conflict",
                column: "resolution_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_basis_conflict_resolved_by",
                schema: "hub",
                table: "basis_conflict",
                column: "resolved_by");

            migrationBuilder.CreateIndex(
                name: "ix_basis_conflict_right_version_id",
                schema: "hub",
                table: "basis_conflict",
                column: "right_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_basis_use_owner_id",
                schema: "hub",
                table: "basis_use",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_basis_use_project_id",
                schema: "hub",
                table: "basis_use",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_basis_use_version_id_target_type_target_id",
                schema: "hub",
                table: "basis_use",
                columns: new[] { "version_id", "target_type", "target_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_design_basis_entry_current_version_id",
                schema: "hub",
                table: "design_basis_entry",
                column: "current_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_design_basis_entry_independent_approver_id",
                schema: "hub",
                table: "design_basis_entry",
                column: "independent_approver_id");

            migrationBuilder.CreateIndex(
                name: "ix_design_basis_entry_key",
                schema: "hub",
                table: "design_basis_entry",
                column: "key");

            migrationBuilder.CreateIndex(
                name: "ix_design_basis_entry_owner_id",
                schema: "hub",
                table: "design_basis_entry",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_design_basis_entry_project_discipline_id",
                schema: "hub",
                table: "design_basis_entry",
                column: "project_discipline_id");

            migrationBuilder.CreateIndex(
                name: "ix_design_basis_entry_project_id_project_discipline_id_kind_ti~",
                schema: "hub",
                table: "design_basis_entry",
                columns: new[] { "project_id", "project_discipline_id", "kind", "title" });

            migrationBuilder.CreateIndex(
                name: "ix_design_basis_entry_project_id_seq",
                schema: "hub",
                table: "design_basis_entry",
                columns: new[] { "project_id", "seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_design_basis_version_confirmed_by",
                schema: "hub",
                table: "design_basis_version",
                column: "confirmed_by");

            migrationBuilder.CreateIndex(
                name: "ix_design_basis_version_decision_id",
                schema: "hub",
                table: "design_basis_version",
                column: "decision_id");

            migrationBuilder.CreateIndex(
                name: "ix_design_basis_version_entry_id_number",
                schema: "hub",
                table: "design_basis_version",
                columns: new[] { "entry_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_design_basis_version_project_id_status",
                schema: "hub",
                table: "design_basis_version",
                columns: new[] { "project_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_design_basis_version_supersedes_version_id",
                schema: "hub",
                table: "design_basis_version",
                column: "supersedes_version_id");

            migrationBuilder.AddForeignKey(
                name: "fk_basis_assumption_disposition_design_basis_versions_version_id",
                schema: "hub",
                table: "basis_assumption_disposition",
                column: "version_id",
                principalSchema: "hub",
                principalTable: "design_basis_version",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_basis_conflict_design_basis_versions_left_version_id",
                schema: "hub",
                table: "basis_conflict",
                column: "left_version_id",
                principalSchema: "hub",
                principalTable: "design_basis_version",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_basis_conflict_design_basis_versions_resolution_version_id",
                schema: "hub",
                table: "basis_conflict",
                column: "resolution_version_id",
                principalSchema: "hub",
                principalTable: "design_basis_version",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_basis_conflict_design_basis_versions_right_version_id",
                schema: "hub",
                table: "basis_conflict",
                column: "right_version_id",
                principalSchema: "hub",
                principalTable: "design_basis_version",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_basis_use_design_basis_versions_version_id",
                schema: "hub",
                table: "basis_use",
                column: "version_id",
                principalSchema: "hub",
                principalTable: "design_basis_version",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_design_basis_entry_design_basis_versions_current_version_id",
                schema: "hub",
                table: "design_basis_entry",
                column: "current_version_id",
                principalSchema: "hub",
                principalTable: "design_basis_version",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                CREATE FUNCTION hub.guard_design_basis_version() RETURNS trigger LANGUAGE plpgsql AS $guard$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Design basis version history cannot be deleted';
                    END IF;
                    IF OLD.status IN ('Confirmed', 'Superseded', 'Withdrawn') THEN
                        IF ROW(NEW.project_id, NEW.entry_id, NEW.number, NEW.supersedes_version_id,
                               NEW.scope, NEW.statement, NEW.numeric_value, NEW.units, NEW.source_system,
                               NEW.stable_source_id, NEW.source_url, NEW.declared_revision,
                               NEW.confirmation_due_date, NEW.decision_id, NEW.confirmed_by,
                               NEW.confirmed_at, NEW.confirmation_rationale)
                           IS DISTINCT FROM
                           ROW(OLD.project_id, OLD.entry_id, OLD.number, OLD.supersedes_version_id,
                               OLD.scope, OLD.statement, OLD.numeric_value, OLD.units, OLD.source_system,
                               OLD.stable_source_id, OLD.source_url, OLD.declared_revision,
                               OLD.confirmation_due_date, OLD.decision_id, OLD.confirmed_by,
                               OLD.confirmed_at, OLD.confirmation_rationale) THEN
                            RAISE EXCEPTION 'Confirmed design basis version content is immutable';
                        END IF;
                        IF (OLD.status = 'Confirmed' AND NEW.status NOT IN ('Confirmed', 'Superseded', 'Withdrawn'))
                           OR (OLD.status <> 'Confirmed' AND NEW.status <> OLD.status) THEN
                            RAISE EXCEPTION 'Design basis version status cannot be reversed';
                        END IF;
                    END IF;
                    RETURN NEW;
                END $guard$;
                CREATE TRIGGER guard_design_basis_version BEFORE UPDATE OR DELETE ON hub.design_basis_version
                    FOR EACH ROW EXECUTE FUNCTION hub.guard_design_basis_version();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS guard_design_basis_version ON hub.design_basis_version; DROP FUNCTION IF EXISTS hub.guard_design_basis_version();");
            migrationBuilder.DropForeignKey(
                name: "fk_design_basis_entry_design_basis_versions_current_version_id",
                schema: "hub",
                table: "design_basis_entry");

            migrationBuilder.DropTable(
                name: "basis_assumption_disposition",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "basis_conflict",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "basis_use",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "design_basis_version",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "design_basis_entry",
                schema: "hub");
        }
    }
}
