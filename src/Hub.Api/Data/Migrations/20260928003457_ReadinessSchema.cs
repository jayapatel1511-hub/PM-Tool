using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReadinessSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "readiness_assessment",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "text", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    intended_output = table.Column<string>(type: "text", nullable: false),
                    completion_criteria = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_readiness_assessment", x => x.id);
                    table.CheckConstraint("ck_readiness_state", "state IN ('Needs Assessment','Not Ready','Ready','Proceed under Assumption')");
                    table.CheckConstraint("ck_readiness_target", "target_type IN ('Task', 'Deliverable')");
                    table.ForeignKey(
                        name: "fk_readiness_assessment_app_user_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_readiness_assessment_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "weekly_plan_snapshot",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    week_start = table.Column<DateOnly>(type: "date", nullable: false),
                    captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    captured_by = table.Column<Guid>(type: "uuid", nullable: false),
                    committed_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_weekly_plan_snapshot", x => x.id);
                    table.ForeignKey(
                        name: "fk_weekly_plan_snapshot_app_user_captured_by",
                        column: x => x.captured_by,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_weekly_plan_snapshot_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_constraint",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "text", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    removal_owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    affected_owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    needed_by = table.Column<DateOnly>(type: "date", nullable: false),
                    source_url = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    resolution_evidence_url = table.Column<string>(type: "text", nullable: true),
                    verified_by = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_work_constraint", x => x.id);
                    table.CheckConstraint("ck_work_constraint_state", "state IN ('Open','Resolution Proposed','Verified Removed','Cancelled')");
                    table.CheckConstraint("ck_work_constraint_target", "target_type IN ('Task', 'Deliverable')");
                    table.ForeignKey(
                        name: "fk_work_constraint_app_user_affected_owner_id",
                        column: x => x.affected_owner_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_constraint_app_user_removal_owner_id",
                        column: x => x.removal_owner_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_constraint_app_user_verified_by",
                        column: x => x.verified_by,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_constraint_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "readiness_check_record",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assessment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    applies = table.Column<bool>(type: "boolean", nullable: true),
                    satisfied = table.Column<bool>(type: "boolean", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    evidence_url = table.Column<string>(type: "text", nullable: true),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_readiness_check_record", x => x.id);
                    table.CheckConstraint("ck_readiness_check_code", "code IN ('Handoff','Predecessor','Decision','Basis','Production Owner','Production Capacity','Review Capacity','Review Gate','Submission Gate')");
                    table.ForeignKey(
                        name: "fk_readiness_check_record_app_user_recorded_by",
                        column: x => x.recorded_by,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_readiness_check_record_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_readiness_check_record_readiness_assessment_assessment_id",
                        column: x => x.assessment_id,
                        principalSchema: "hub",
                        principalTable: "readiness_assessment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "readiness_exception",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assessment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    basis_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: false),
                    verifier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    limited_work = table.Column<string>(type: "text", nullable: false),
                    risk = table.Column<string>(type: "text", nullable: false),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_readiness_exception", x => x.id);
                    table.ForeignKey(
                        name: "fk_readiness_exception_app_user_approved_by",
                        column: x => x.approved_by,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_readiness_exception_app_user_verifier_id",
                        column: x => x.verifier_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_readiness_exception_design_basis_version_basis_version_id",
                        column: x => x.basis_version_id,
                        principalSchema: "hub",
                        principalTable: "design_basis_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_readiness_exception_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_readiness_exception_readiness_assessment_assessment_id",
                        column: x => x.assessment_id,
                        principalSchema: "hub",
                        principalTable: "readiness_assessment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "output_commitment",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_type = table.Column<string>(type: "text", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    performer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    intended_output = table.Column<string>(type: "text", nullable: false),
                    completion_criteria = table.Column<string>(type: "text", nullable: false),
                    target_date = table.Column<DateOnly>(type: "date", nullable: false),
                    week_start = table.Column<DateOnly>(type: "date", nullable: false),
                    readiness_at_commit = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    completion_evidence_url = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_output_commitment", x => x.id);
                    table.CheckConstraint("ck_commitment_readiness", "readiness_at_commit IN ('Needs Assessment','Not Ready','Ready','Proceed under Assumption')");
                    table.CheckConstraint("ck_commitment_state", "state IN ('Proposed','Committed','Met','Not Met','Withdrawn')");
                    table.CheckConstraint("ck_commitment_target", "target_type IN ('Task', 'Deliverable')");
                    table.ForeignKey(
                        name: "fk_output_commitment_app_user_performer_id",
                        column: x => x.performer_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_output_commitment_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_output_commitment_weekly_plan_snapshots_snapshot_id",
                        column: x => x.snapshot_id,
                        principalSchema: "hub",
                        principalTable: "weekly_plan_snapshot",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "output_commitment_event",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    commitment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_state = table.Column<string>(type: "text", nullable: false),
                    to_state = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    evidence_url = table.Column<string>(type: "text", nullable: true),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_output_commitment_event", x => x.id);
                    table.ForeignKey(
                        name: "fk_output_commitment_event_app_user_actor_id",
                        column: x => x.actor_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_output_commitment_event_output_commitment_commitment_id",
                        column: x => x.commitment_id,
                        principalSchema: "hub",
                        principalTable: "output_commitment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_output_commitment_event_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_output_commitment_performer_id",
                schema: "hub",
                table: "output_commitment",
                column: "performer_id");

            migrationBuilder.CreateIndex(
                name: "ix_output_commitment_project_id_week_start_performer_id",
                schema: "hub",
                table: "output_commitment",
                columns: new[] { "project_id", "week_start", "performer_id" });

            migrationBuilder.CreateIndex(
                name: "ix_output_commitment_snapshot_id",
                schema: "hub",
                table: "output_commitment",
                column: "snapshot_id");

            migrationBuilder.CreateIndex(
                name: "ix_output_commitment_event_actor_id",
                schema: "hub",
                table: "output_commitment_event",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_output_commitment_event_commitment_id_created_at",
                schema: "hub",
                table: "output_commitment_event",
                columns: new[] { "commitment_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_output_commitment_event_project_id",
                schema: "hub",
                table: "output_commitment_event",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_readiness_assessment_owner_id",
                schema: "hub",
                table: "readiness_assessment",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_readiness_assessment_project_id_target_type_target_id",
                schema: "hub",
                table: "readiness_assessment",
                columns: new[] { "project_id", "target_type", "target_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_readiness_check_record_assessment_id_code",
                schema: "hub",
                table: "readiness_check_record",
                columns: new[] { "assessment_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_readiness_check_record_project_id",
                schema: "hub",
                table: "readiness_check_record",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_readiness_check_record_recorded_by",
                schema: "hub",
                table: "readiness_check_record",
                column: "recorded_by");

            migrationBuilder.CreateIndex(
                name: "ix_readiness_exception_approved_by",
                schema: "hub",
                table: "readiness_exception",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "ix_readiness_exception_assessment_id",
                schema: "hub",
                table: "readiness_exception",
                column: "assessment_id");

            migrationBuilder.CreateIndex(
                name: "ix_readiness_exception_basis_version_id",
                schema: "hub",
                table: "readiness_exception",
                column: "basis_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_readiness_exception_project_id",
                schema: "hub",
                table: "readiness_exception",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_readiness_exception_verifier_id",
                schema: "hub",
                table: "readiness_exception",
                column: "verifier_id");

            migrationBuilder.CreateIndex(
                name: "ix_weekly_plan_snapshot_captured_by",
                schema: "hub",
                table: "weekly_plan_snapshot",
                column: "captured_by");

            migrationBuilder.CreateIndex(
                name: "ix_weekly_plan_snapshot_project_id_week_start",
                schema: "hub",
                table: "weekly_plan_snapshot",
                columns: new[] { "project_id", "week_start" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_constraint_affected_owner_id",
                schema: "hub",
                table: "work_constraint",
                column: "affected_owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_constraint_project_id_target_type_target_id_state",
                schema: "hub",
                table: "work_constraint",
                columns: new[] { "project_id", "target_type", "target_id", "state" });

            migrationBuilder.CreateIndex(
                name: "ix_work_constraint_removal_owner_id",
                schema: "hub",
                table: "work_constraint",
                column: "removal_owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_constraint_verified_by",
                schema: "hub",
                table: "work_constraint",
                column: "verified_by");

            migrationBuilder.Sql("""
                CREATE FUNCTION hub.guard_weekly_snapshot() RETURNS trigger LANGUAGE plpgsql AS $guard$
                BEGIN
                    RAISE EXCEPTION 'Weekly committed snapshot is immutable';
                END $guard$;
                CREATE TRIGGER guard_weekly_snapshot BEFORE UPDATE OR DELETE ON hub.weekly_plan_snapshot
                    FOR EACH ROW EXECUTE FUNCTION hub.guard_weekly_snapshot();

                CREATE FUNCTION hub.guard_commitment_history() RETURNS trigger LANGUAGE plpgsql AS $guard$
                DECLARE snapshot_project uuid; snapshot_week date;
                BEGIN
                    IF TG_TABLE_NAME = 'output_commitment_event' THEN
                        IF TG_OP <> 'INSERT' THEN RAISE EXCEPTION 'Commitment event history is immutable'; END IF;
                        RETURN NEW;
                    END IF;
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Commitment history cannot be deleted';
                    END IF;
                    IF NEW.snapshot_id IS NOT NULL THEN
                        SELECT project_id, week_start INTO snapshot_project, snapshot_week
                            FROM hub.weekly_plan_snapshot WHERE id = NEW.snapshot_id;
                        IF snapshot_project IS DISTINCT FROM NEW.project_id OR snapshot_week IS DISTINCT FROM NEW.week_start THEN
                            RAISE EXCEPTION 'Commitment snapshot must belong to the same project and week';
                        END IF;
                    END IF;
                    IF TG_OP = 'INSERT' THEN
                        IF NEW.state NOT IN ('Proposed', 'Committed') THEN
                            RAISE EXCEPTION 'New commitment must be proposed or signed';
                        END IF;
                        RETURN NEW;
                    END IF;
                    IF OLD.state = 'Proposed' AND NEW.state NOT IN ('Proposed', 'Committed', 'Withdrawn') THEN
                        RAISE EXCEPTION 'A proposed commitment cannot close without performer commitment';
                    END IF;
                    IF OLD.state <> 'Proposed' THEN
                        IF ROW(NEW.project_id, NEW.target_type, NEW.target_id, NEW.performer_id,
                               NEW.intended_output, NEW.completion_criteria, NEW.target_date,
                               NEW.week_start, NEW.readiness_at_commit)
                           IS DISTINCT FROM
                           ROW(OLD.project_id, OLD.target_type, OLD.target_id, OLD.performer_id,
                               OLD.intended_output, OLD.completion_criteria, OLD.target_date,
                               OLD.week_start, OLD.readiness_at_commit) THEN
                            RAISE EXCEPTION 'Committed promise content is immutable';
                        END IF;
                        IF OLD.snapshot_id IS NOT NULL AND NEW.snapshot_id IS DISTINCT FROM OLD.snapshot_id THEN
                            RAISE EXCEPTION 'Committed promise snapshot cannot change';
                        END IF;
                        IF OLD.snapshot_id IS NULL AND NEW.snapshot_id IS NOT NULL AND OLD.state <> 'Committed' THEN
                            RAISE EXCEPTION 'A closed commitment cannot enter a snapshot';
                        END IF;
                        IF OLD.state = 'Committed' AND NEW.state NOT IN ('Committed', 'Met', 'Not Met', 'Withdrawn') THEN
                            RAISE EXCEPTION 'Committed promise state cannot reverse';
                        END IF;
                        IF OLD.state <> 'Committed' AND NEW.state <> OLD.state THEN
                            RAISE EXCEPTION 'Closed commitment outcome cannot change';
                        END IF;
                    END IF;
                    RETURN NEW;
                END $guard$;
                CREATE TRIGGER guard_output_commitment BEFORE INSERT OR UPDATE OR DELETE ON hub.output_commitment
                    FOR EACH ROW EXECUTE FUNCTION hub.guard_commitment_history();
                CREATE TRIGGER guard_output_commitment_event BEFORE INSERT OR UPDATE OR DELETE ON hub.output_commitment_event
                    FOR EACH ROW EXECUTE FUNCTION hub.guard_commitment_history();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS guard_output_commitment_event ON hub.output_commitment_event;
                DROP TRIGGER IF EXISTS guard_output_commitment ON hub.output_commitment;
                DROP TRIGGER IF EXISTS guard_weekly_snapshot ON hub.weekly_plan_snapshot;
                DROP FUNCTION IF EXISTS hub.guard_commitment_history();
                DROP FUNCTION IF EXISTS hub.guard_weekly_snapshot();
                """);
            migrationBuilder.DropTable(
                name: "output_commitment_event",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "readiness_check_record",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "readiness_exception",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "work_constraint",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "output_commitment",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "readiness_assessment",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "weekly_plan_snapshot",
                schema: "hub");
        }
    }
}
